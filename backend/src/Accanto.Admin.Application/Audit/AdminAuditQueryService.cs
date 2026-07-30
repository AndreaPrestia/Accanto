using Accanto.Admin.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Admin.Application.Audit;

public class AdminAuditQueryService : IAdminAuditQueryService
{
    private readonly IAccantoAdminDbContext _db;

    public AdminAuditQueryService(IAccantoAdminDbContext db)
    {
        _db = db;
    }

    public async Task<AdminAuditLogListResponse> ListAsync(
        Guid? adminUserId,
        string? action,
        string? targetType,
        string? targetId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (adminUserId.HasValue) q = q.Where(a => a.AdminUserId == adminUserId.Value);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(targetType)) q = q.Where(a => a.TargetType == targetType);
        if (!string.IsNullOrWhiteSpace(targetId)) q = q.Where(a => a.TargetId == targetId);
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt <= to.Value);

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminAuditLogDto(
                a.Id,
                a.AdminUserId,
                a.AdminUser.Email,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Reason,
                a.IpAddress,
                a.UserAgent,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminAuditLogListResponse(items, page, pageSize, total);
    }
}
