namespace Accanto.Admin.Application.Audit;

/// <summary>
/// Entry di audit log admin esposta in lettura. Contiene SOLO metadata tecnici:
/// MAI body request/response, MAI contenuti utente, MAI nomi file/care circle.
/// </summary>
public sealed record AdminAuditLogDto(
    Guid Id,
    Guid AdminUserId,
    string? AdminEmail,
    string Action,
    string TargetType,
    string? TargetId,
    string? Reason,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

public sealed record AdminAuditLogListResponse(
    IReadOnlyList<AdminAuditLogDto> Items,
    int Page,
    int PageSize,
    int Total);
