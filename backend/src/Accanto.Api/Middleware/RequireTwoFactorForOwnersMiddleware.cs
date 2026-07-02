using System.Security.Claims;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Common.Persistence;
using Accanto.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Api.Middleware;

/// <summary>
/// Middleware che blocca (403) gli utenti Owner di almeno un care circle che
/// non hanno configurato 2FA oltre la grace window. Esposto come servizio
/// applicativo perche' fa accesso al DB; registrato in pipeline dopo
/// UseAuthentication/UseAuthorization.
///
/// La whitelist e' volutamente piccola: tutto cio' che serve per configurare
/// 2FA, fare logout, leggere account/me. Senza la whitelist l'Owner che e'
/// gia' oltre la grace non potrebbe MAI raggiungere /api/account/2fa/setup
/// per uscire dal blocco -> deadlock.
/// </summary>
public sealed class RequireTwoFactorForOwnersMiddleware
{
    // Path che restano accessibili anche con 2FA-required scaduto. Match per
    // prefisso, case-insensitive. Tenere il minimo indispensabile.
    //
    // NB: sia il path esatto (`/api/account/2fa` per la GET dello status)
    // sia il subtree (`/api/account/2fa/setup`, `/enable`, `/disable`, ecc.)
    // devono essere in bypass; altrimenti l'Owner scaduto non pu\u00f2 nemmeno
    // leggere lo stato per uscire dal blocco (deadlock UI).
    private static readonly string[] BypassPrefixes =
    {
        "/api/account/2fa",       // stato (GET) + tutti i sub-path (setup, enable, ...)
        "/api/account/me",        // serve per leggere lo stato e fare logout dal frontend
        "/api/auth/logout",
        "/api/auth/refresh",
        "/api/auth/login",        // gia' coperto da AllowAnonymous ma esplicito
        "/api/auth/2fa-login",    // completamento challenge 2FA
        "/api/security/csp-report",
        "/swagger",
        "/health"
    };

    private readonly RequestDelegate _next;

    public RequireTwoFactorForOwnersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext ctx,
        IAccantoDbContext db,
        IOptions<TwoFactorOptions> opt)
    {
        var o = opt.Value;
        if (!o.RequireForOwners)
        {
            await _next(ctx);
            return;
        }

        // Anonimi / health / setup-2fa: lascia passare.
        if (!(ctx.User?.Identity?.IsAuthenticated ?? false))
        {
            await _next(ctx);
            return;
        }
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (IsBypassed(path))
        {
            await _next(ctx);
            return;
        }

        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            await _next(ctx);
            return;
        }

        // Una sola query: stato 2fa, deadline obbligo, e se l'utente e'
        // Owner di almeno un cerchio. Indice (UserId, Role) gia' coperto
        // dall'index UNIQUE (CareCircleId, UserId) — la scan e' su pochi
        // record per utente, ok come costo per-request finche' non emerge
        // un hotspot reale (allora cache via claim).
        var snapshot = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.TwoFactorEnabled,
                u.TwoFactorRequiredFromUtc,
                IsOwner = db.CareCircleMembers.Any(m => m.UserId == userId && m.Role == CareCircleRole.Owner)
            })
            .FirstOrDefaultAsync(ctx.RequestAborted);

        if (snapshot is null || snapshot.TwoFactorEnabled || !snapshot.IsOwner)
        {
            await _next(ctx);
            return;
        }

        // L'utente e' Owner senza 2FA. Setto/leggo la deadline.
        var deadline = snapshot.TwoFactorRequiredFromUtc;
        if (deadline is null)
        {
            // Lazy backfill: utente mai stato Owner prima del rollout +
            // mai promosso post-rollout (es. seed/test). Inizio il timer ora.
            deadline = DateTimeOffset.UtcNow.AddHours(o.OwnerGraceHours);
            var u = await db.Users.FirstAsync(x => x.Id == userId, ctx.RequestAborted);
            if (u.TwoFactorRequiredFromUtc is null)
            {
                u.TwoFactorRequiredFromUtc = deadline;
                await db.SaveChangesAsync(ctx.RequestAborted);
            }
            else
            {
                deadline = u.TwoFactorRequiredFromUtc;
            }
        }

        if (DateTimeOffset.UtcNow < deadline.Value)
        {
            // Ancora dentro la grace: passa, ma esponi header informativo per
            // il frontend (banner countdown).
            ctx.Response.Headers["X-2FA-Required-By"] = deadline.Value.ToString("o");
            await _next(ctx);
            return;
        }

        // Oltre la grace -> blocca.
        await WriteForbiddenAsync(ctx, deadline.Value);
    }

    private static bool IsBypassed(string path)
    {
        foreach (var p in BypassPrefixes)
        {
            if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static async Task WriteForbiddenAsync(HttpContext ctx, DateTimeOffset deadline)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/problem+json";
        var pd = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Verifica in due passaggi obbligatoria.",
            Detail = $"In quanto Owner di almeno un care circle devi attivare la verifica in due passaggi. " +
                     $"Grace scaduta il {deadline:o}. Visita /account/security/2fa per attivarla.",
            Type = "https://accanto.care/errors/two-factor-required"
        };
        pd.Extensions["code"] = "two_factor_required_for_owner";
        await ctx.Response.WriteAsJsonAsync(pd);
    }
}
