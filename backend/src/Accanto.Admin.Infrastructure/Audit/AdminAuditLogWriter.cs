using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Domain.Entities;

namespace Accanto.Admin.Infrastructure.Audit;

/// <summary>
/// Scrive entry di audit admin. Append-only a livello DB (il ruolo runtime
/// ha UPDATE/DELETE revocati via migration/startup). Registra solo metadata
/// tecnici: mai body, mai contenuti utente.
/// </summary>
public class AdminAuditLogWriter : IAdminAuditLog
{
    private readonly IAccantoAdminDbContext _db;
    private readonly TimeProvider _time;

    public AdminAuditLogWriter(IAccantoAdminDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task WriteAsync(
        Guid adminUserId,
        string action,
        string targetType,
        string? targetId = null,
        string? reason = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            Action = Truncate(action, 128) ?? string.Empty,
            TargetType = Truncate(targetType, 64) ?? string.Empty,
            TargetId = Truncate(targetId, 128),
            Reason = Truncate(reason, 500),
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 500),
            CreatedAt = _time.GetUtcNow()
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
