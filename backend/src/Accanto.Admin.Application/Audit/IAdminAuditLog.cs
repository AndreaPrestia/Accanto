namespace Accanto.Admin.Application.Audit;

/// <summary>
/// Scrittore dell'audit log admin (append-only). Registra SOLO metadata tecnici:
/// mai body, mai contenuti utente, mai nomi file/care circle.
/// </summary>
public interface IAdminAuditLog
{
    Task WriteAsync(
        Guid adminUserId,
        string action,
        string targetType,
        string? targetId = null,
        string? reason = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}
