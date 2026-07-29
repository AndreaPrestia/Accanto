using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Api.Common;

/// <summary>
/// Seed di sviluppo: crea i ruoli admin canonici e, SOLO se non esiste alcun
/// admin, un utente Owner iniziale. Eseguito solo in ambiente Development.
/// La password NON viene mai loggata. In produzione gli admin vanno creati
/// tramite procedura operativa documentata (non seed automatico).
/// </summary>
public static class AdminSeed
{
    public static async Task EnsureSeedAsync(IServiceProvider services, IConfiguration config, ILogger logger, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoAdminDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IAdminPasswordHasher>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        // Ruoli canonici (idempotente).
        var existingRoles = await db.AdminRoles.Select(r => r.Name).ToListAsync(ct);
        foreach (var roleName in AdminRoles.All)
        {
            if (!existingRoles.Contains(roleName))
                db.AdminRoles.Add(new AdminRole { Id = Guid.NewGuid(), Name = roleName });
        }
        await db.SaveChangesAsync(ct);

        // Nessun admin seed se ne esiste gia' almeno uno.
        if (await db.AdminUsers.AnyAsync(ct))
            return;

        var email = config["AdminSeed:Email"];
        var password = config["AdminSeed:Password"];
        var displayName = config["AdminSeed:DisplayName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Nessun admin presente e AdminSeed:Email/Password non configurati: seed admin saltato.");
            return;
        }

        var ownerRole = await db.AdminRoles.FirstAsync(r => r.Name == AdminRoles.Owner, ct);
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName,
            PasswordHash = hasher.Hash(password),
            MfaEnabled = false,
            IsActive = true,
            CreatedAt = time.GetUtcNow()
        };
        admin.Roles.Add(new AdminUserRole { Id = Guid.NewGuid(), AdminUserId = admin.Id, AdminRoleId = ownerRole.Id });

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seed admin creato per {Email} (ruolo Owner). Password non loggata.", admin.Email);
    }
}
