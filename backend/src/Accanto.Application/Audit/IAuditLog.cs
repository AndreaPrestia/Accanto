using Accanto.Domain.Enums;

namespace Accanto.Application.Audit;

/// <summary>
/// Registra azioni significative sui cerchi di cura. Fire-and-forget: le implementazioni
/// non devono propagare eccezioni che blocchino l'operazione principale.
/// </summary>
public interface IAuditLog
{
    Task LogAsync(
        Guid careCircleId,
        Guid performedByUserId,
        AuditActionType actionType,
        AuditResourceType resourceType,
        Guid? resourceId = null,
        string? summary = null,
        CancellationToken cancellationToken = default);
}
