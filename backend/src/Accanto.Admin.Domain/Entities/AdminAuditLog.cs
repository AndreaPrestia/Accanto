namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Audit log delle azioni amministrative. Append-only a livello DB.
/// NON deve mai contenere: payload sensibili, request/response body,
/// contenuti utente (timeline, documenti, domande, aggiornamenti),
/// nomi care circle, nomi file originali. Solo metadata tecnici.
/// </summary>
public sealed class AdminAuditLog
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }

    /// <summary>Azione tecnica (es. "user.disable", "session.revoke").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Tipo di target (es. "user", "session", "operation").</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Identificativo opaco del target (GUID). Mai contenuto utente.</summary>
    public string? TargetId { get; set; }

    /// <summary>Motivazione obbligatoria per azioni mutative. Lunghezza limitata.</summary>
    public string? Reason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AdminUser AdminUser { get; set; } = default!;
}
