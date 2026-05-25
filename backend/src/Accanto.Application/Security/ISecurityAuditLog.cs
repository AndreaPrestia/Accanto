using Accanto.Application.Auth;
using Accanto.Domain.Enums;

namespace Accanto.Application.Security;

public interface ISecurityAuditLog
{
    /// <summary>
    /// Registra un evento di sicurezza. Fire-and-forget interno: non propaga eccezioni.
    /// </summary>
    Task LogAsync(
        Guid? userId,
        SecurityAuditEventType eventType,
        string? summary = null,
        string? emailAttempted = null,
        ClientInfo? client = null,
        CancellationToken cancellationToken = default);
}
