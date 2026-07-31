using System.Text.Json;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Api.Common;

/// <summary>
/// Seed degli admin: garantisce sempre i ruoli canonici e provisiona gli admin
/// dichiarati in config **senza password** (email + display name + ruolo). Ogni
/// admin imposta poi la propria password tramite il flusso di reset.
///
/// Idempotente ed eseguibile anche in produzione: i ruoli sono garantiti sempre,
/// gli admin vengono creati solo se l'email non esiste gia'. NON introduce
/// credenziali long-lived in configurazione (nessuna password nel seed).
/// </summary>
public static class AdminSeed
{
    private sealed record SeedAdmin(string Email, string? DisplayName, string? Role);

    public static async Task EnsureSeedAsync(IServiceProvider services, IConfiguration config, ILogger logger, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoAdminDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        // 1) Ruoli canonici (idempotente).
        var existingRoles = await db.AdminRoles.ToListAsync(ct);
        foreach (var roleName in AdminRoles.All)
        {
            if (!existingRoles.Any(r => r.Name == roleName))
            {
                var role = new AdminRole { Id = Guid.NewGuid(), Name = roleName };
                db.AdminRoles.Add(role);
                existingRoles.Add(role);
            }
        }
        await db.SaveChangesAsync(ct);

        // 2) Admin da config (senza password).
        var seedAdmins = ParseSeedAdmins(config, logger);
        if (seedAdmins.Count == 0)
        {
            logger.LogInformation("AdminSeed: nessun admin da seedare (AdminSeed:Admins / AdminSeed:Email non impostati).");
            return;
        }

        var createdCount = 0;
        foreach (var seed in seedAdmins)
        {
            var email = seed.Email.Trim().ToLowerInvariant();
            if (await db.AdminUsers.AnyAsync(u => u.Email == email, ct))
                continue; // idempotente: non duplicare

            var roleName = ResolveRole(seed.Role);
            var role = existingRoles.First(r => r.Name == roleName);
            var displayName = string.IsNullOrWhiteSpace(seed.DisplayName)
                ? email.Split('@')[0]
                : seed.DisplayName!.Trim();

            var admin = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName,
                PasswordHash = string.Empty, // login bloccato finche' non completa il reset
                MfaEnabled = false,
                IsActive = true,
                CreatedAt = time.GetUtcNow()
            };
            admin.Roles.Add(new AdminUserRole { Id = Guid.NewGuid(), AdminUserId = admin.Id, AdminRoleId = role.Id });
            db.AdminUsers.Add(admin);
            createdCount++;
            // L'email admin nel log di seed e' intenzionale (diagnostica ops su
            // account amministrativi, non utenti finali) e finisce solo nei log server.
            // codeql[cs/exposure-of-sensitive-information]
            logger.LogInformation("AdminSeed: creato admin {Email} (ruolo {Role}), senza password. Usare forgot-password per impostarla.", email, roleName);
        }

        if (createdCount > 0)
            await db.SaveChangesAsync(ct);
        else
            logger.LogInformation("AdminSeed: tutti gli admin richiesti esistono gia' (nessuna creazione).");
    }

    private static string ResolveRole(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            var match = AdminRoles.All.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return AdminRoles.Owner;
    }

    private static List<SeedAdmin> ParseSeedAdmins(IConfiguration config, ILogger logger)
    {
        var result = new List<SeedAdmin>();

        // Formato primario: AdminSeed:Admins = JSON array.
        var json = config["AdminSeed:Admins"];
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<SeedAdmin>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed is not null)
                    result.AddRange(parsed.Where(a => !string.IsNullOrWhiteSpace(a.Email)));
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "AdminSeed:Admins non e' un JSON array valido; verra' ignorato.");
            }
        }

        // Retro-compat: AdminSeed:Email singolo (senza password).
        var singleEmail = config["AdminSeed:Email"];
        if (!string.IsNullOrWhiteSpace(singleEmail) &&
            !result.Any(a => string.Equals(a.Email, singleEmail, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new SeedAdmin(singleEmail, config["AdminSeed:DisplayName"], config["AdminSeed:Role"]));
        }

        return result;
    }
}
