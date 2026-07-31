namespace Accanto.Application.Internal;

/// <summary>
/// Metadata utente esposti agli endpoint interni service-to-service per il
/// control plane admin. Contiene SOLO metadata tecnici e aggregati.
///
/// PRIVACY BOUNDARY — NON aggiungere MAI qui:
/// CareCircle.Name/Description, TimelineEntry.Title/Content/Tags,
/// MedicalDocument.OriginalFileName/StoragePath/Notes/Tags, contenuto file,
/// DoctorQuestion.Question/AnswerNotes, SharedUpdate.Content.
/// Regola (03-privacy-boundary): se un campo aiuterebbe un admin a leggere la
/// situazione clinica/familiare/emotiva dell'utente, il campo e' vietato.
/// </summary>
public sealed record InternalUserMetadataDto(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    bool IsDisabled,
    string AccountStatus,
    DateTimeOffset? DisabledAt,
    string? DisabledReason,
    int CareCircleCount,
    int DocumentsCount,
    long StorageUsedBytes,
    int TimelineEntryCount);

public sealed record InternalUserListResponse(
    IReadOnlyList<InternalUserMetadataDto> Items,
    int Page,
    int PageSize,
    int Total);

/// <summary>Richiesta di disabilitazione/riabilitazione account (comando interno).</summary>
public sealed record InternalSetDisabledRequest(string? Reason);

/// <summary>Richiesta di avvio cancellazione (comando interno, reason obbligatoria).</summary>
public sealed record InternalStartDeletionRequest(string Reason);
