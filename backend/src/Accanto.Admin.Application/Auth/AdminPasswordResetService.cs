using System.Security.Cryptography;
using System.Text;
using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Application.Email;
using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Admin.Application.Auth;

public class AdminPasswordResetService : IAdminPasswordResetService
{
    private const int MinPasswordLength = 8;

    private readonly IAccantoAdminDbContext _db;
    private readonly IAdminPasswordHasher _hasher;
    private readonly IAdminEmailSender _email;
    private readonly IAdminAuditLog _audit;
    private readonly AdminPasswordResetOptions _opt;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminPasswordResetService> _logger;

    public AdminPasswordResetService(
        IAccantoAdminDbContext db,
        IAdminPasswordHasher hasher,
        IAdminEmailSender email,
        IAdminAuditLog audit,
        IOptions<AdminPasswordResetOptions> opt,
        TimeProvider time,
        ILogger<AdminPasswordResetService> logger)
    {
        _db = db;
        _hasher = hasher;
        _email = email;
        _audit = audit;
        _opt = opt.Value;
        _time = time;
        _logger = logger;
    }

    public async Task RequestResetAsync(AdminForgotPasswordRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new AdminValidationException("Email obbligatoria.");

        var email = request.Email.Trim().ToLowerInvariant();
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Anti-enumerazione: risposta identica a prescindere dall'esistenza.
        // Non emettiamo token per admin inattivi.
        if (admin is null || !admin.IsActive)
        {
            await _audit.WriteAsync(admin?.Id ?? Guid.Empty, "Admin.PasswordResetRequested", "AdminUser",
                admin?.Id.ToString(), "Email sconosciuta o inattiva", client?.IpAddress, client?.UserAgent, cancellationToken);
            return;
        }

        var rawToken = GenerateUrlSafeToken();
        var now = _time.GetUtcNow();
        var lifetime = Math.Max(5, _opt.TokenLifetimeMinutes);

        _db.AdminPasswordResetTokens.Add(new AdminPasswordResetToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = admin.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(lifetime),
            IpAddress = client?.IpAddress,
            UserAgent = client?.UserAgent
        });
        await _db.SaveChangesAsync(cancellationToken);

        var link = BuildResetLink(rawToken);
        var html = BuildEmailHtml(admin.DisplayName, link, lifetime);
        try
        {
            await _email.SendAsync(admin.Email, admin.DisplayName, "Imposta la password admin Accanto", html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio email reset password admin a {AdminUserId}", admin.Id);
        }

        await _audit.WriteAsync(admin.Id, "Admin.PasswordResetRequested", "AdminUser", admin.Id.ToString(),
            null, client?.IpAddress, client?.UserAgent, cancellationToken);
    }

    public async Task ResetAsync(AdminResetPasswordRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new AdminForbiddenException("Token non valido o scaduto.");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            throw new AdminValidationException($"La password deve avere almeno {MinPasswordLength} caratteri.");

        var hash = HashToken(request.Token);
        var now = _time.GetUtcNow();

        var entry = await _db.AdminPasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (entry is null || entry.UsedAt is not null || entry.ExpiresAt <= now)
            throw new AdminForbiddenException("Token non valido o scaduto.");

        var admin = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == entry.AdminUserId, cancellationToken);
        if (admin is null || !admin.IsActive)
            throw new AdminForbiddenException("Token non valido o scaduto.");

        admin.PasswordHash = _hasher.Hash(request.NewPassword);
        entry.UsedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        // Revoca tutte le sessioni admin: chi possedeva la vecchia password e' fuori.
        var sessions = await _db.AdminSessions
            .Where(s => s.AdminUserId == admin.Id && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var s in sessions) s.RevokedAt = now;
        if (sessions.Count > 0) await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(admin.Id, "Admin.PasswordResetCompleted", "AdminUser", admin.Id.ToString(),
            null, client?.IpAddress, client?.UserAgent, cancellationToken);
    }

    private string BuildResetLink(string token)
    {
        var baseUrl = (_opt.PublicUrl ?? string.Empty).TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(_opt.ResetPath) ? "/reset-password" : _opt.ResetPath;
        if (!path.StartsWith('/')) path = "/" + path;
        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }

    private static string BuildEmailHtml(string displayName, string link, int lifetimeMinutes)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(displayName);
        var safeLink = System.Net.WebUtility.HtmlEncode(link);
        return $$"""
            <!DOCTYPE html>
            <html>
              <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #2b2b2b; max-width: 540px; margin: 0 auto; padding: 24px;">
                <p>Ciao {{safeName}},</p>
                <p>Per il tuo account amministrativo Accanto, imposta o reimposta la password usando il link qui sotto:</p>
                <p style="margin: 24px 0;">
                  <a href="{{safeLink}}" style="display: inline-block; padding: 12px 18px; background: #334155; color: #fff; text-decoration: none; border-radius: 6px;">
                    Imposta password
                  </a>
                </p>
                <p style="font-size: 13px; color: #555;">
                  Il link scade tra <strong>{{lifetimeMinutes}} minuti</strong>. Se non hai richiesto tu questa operazione, ignora questa email.
                </p>
                <p style="font-size: 12px; color: #888; margin-top: 32px; word-break: break-all;">
                  Se il pulsante non funziona, copia e incolla questo URL nel browser:<br/>
                  {{safeLink}}
                </p>
              </body>
            </html>
            """;
    }

    private static string GenerateUrlSafeToken()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string rawToken)
    {
        Span<byte> dest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken), dest);
        return Convert.ToHexString(dest).ToLowerInvariant();
    }
}
