using Accanto.Admin.Domain.Enums;

namespace Accanto.Admin.Application.Users;

/// <summary>
/// Metadata utente proxy dagli endpoint interni della app pubblica.
/// SOLO metadata + aggregati: MAI contenuti utente (nomi cerchi, titoli/
/// contenuti timeline, filename, path, domande, aggiornamenti).
/// </summary>
public sealed record AdminUserMetadataDto(
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

public sealed record AdminUserListResponse(
    IReadOnlyList<AdminUserMetadataDto> Items,
    int Page,
    int PageSize,
    int Total);

/// <summary>Richiesta operazione su utente: reason OBBLIGATORIA.</summary>
public sealed record AdminUserOperationRequest(string Reason);

public sealed record AdminOperationResultDto(Guid OperationId, string Status);

public sealed record AdminOperationDto(
    Guid Id,
    Guid RequestedByAdminUserId,
    AdminOperationType OperationType,
    Guid? TargetUserId,
    AdminOperationStatus Status,
    string Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);

public sealed record AdminOperationListResponse(
    IReadOnlyList<AdminOperationDto> Items,
    int Page,
    int PageSize,
    int Total);
