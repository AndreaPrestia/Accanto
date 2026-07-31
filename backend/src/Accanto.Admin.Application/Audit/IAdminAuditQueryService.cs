namespace Accanto.Admin.Application.Audit;

/// <summary>Query di lettura sull'audit log admin (append-only, metadata-only).</summary>
public interface IAdminAuditQueryService
{
    Task<AdminAuditLogListResponse> ListAsync(
        Guid? adminUserId,
        string? action,
        string? targetType,
        string? targetId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
