using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Infrastructure.Persistence;

/// <summary>
/// DbContext del database admin (AccantoAdminDb). Completamente separato da
/// <c>AccantoDbContext</c> pubblico: non lo estende, non lo riusa, non condivide
/// tabelle. Contiene SOLO entita' amministrative; nessun dato utente sensibile.
/// </summary>
public class AccantoAdminDbContext : DbContext
{
    public AccantoAdminDbContext(DbContextOptions<AccantoAdminDbContext> options) : base(options)
    {
    }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AdminOperation> AdminOperations => Set<AdminOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccantoAdminDbContext).Assembly);
    }
}
