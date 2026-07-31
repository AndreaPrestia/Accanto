using Accanto.Admin.Domain.Enums;

namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Operazione tecnica richiesta da un admin (disable/enable/revoke/start-deletion).
/// Traccia il ciclo di vita (pending → completed/failed/cancelled) e richiede
/// sempre una <see cref="Reason"/>. <see cref="TargetUserId"/> e' un riferimento
/// opaco all'utente pubblico: nessun dato sensibile viene copiato qui.
/// </summary>
public sealed class AdminOperation
{
    public Guid Id { get; set; }
    public Guid RequestedByAdminUserId { get; set; }
    public AdminOperationType OperationType { get; set; }

    /// <summary>Id opaco dell'utente pubblico target (nessun contenuto copiato).</summary>
    public Guid? TargetUserId { get; set; }

    public AdminOperationStatus Status { get; set; } = AdminOperationStatus.Pending;

    /// <summary>Motivazione obbligatoria dell'operazione.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public AdminUser RequestedByAdminUser { get; set; } = default!;
}
