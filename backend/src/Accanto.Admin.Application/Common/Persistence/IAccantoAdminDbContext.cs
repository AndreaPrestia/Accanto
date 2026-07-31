using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Application.Common.Persistence;

/// <summary>
/// Contratto di accesso al DB admin. Espone SOLO entita' amministrative:
/// nessun DbSet del dominio pubblico (users, timeline, documenti, ecc.).
/// </summary>
public interface IAccantoAdminDbContext
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AdminRole> AdminRoles { get; }
    DbSet<AdminUserRole> AdminUserRoles { get; }
    DbSet<AdminSession> AdminSessions { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }
    DbSet<AdminOperation> AdminOperations { get; }
    DbSet<AdminPasswordResetToken> AdminPasswordResetTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
