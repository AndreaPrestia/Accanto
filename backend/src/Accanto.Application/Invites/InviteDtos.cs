using Accanto.Domain.Enums;

namespace Accanto.Application.Invites;

public sealed record CreateInviteRequest(CareCircleRole Role, int? ExpiresInDays, int? MaxUses);

public sealed record InviteDto(
    Guid Id,
    Guid CareCircleId,
    CareCircleRole Role,
    string Token,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UsedCount,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt,
    bool IsActive
);

/// <summary>
/// Anteprima pubblica di un invito: serve a mostrare a chi clicca il link
/// di cosa stanno per entrare a far parte, prima del login/registrazione.
/// Non espone nulla di sensibile (niente descrizione cifrata, niente lista membri).
/// </summary>
public sealed record InvitePreviewDto(
    string CircleName,
    CareCircleRole Role,
    DateTimeOffset ExpiresAt,
    string InvitedByDisplayName
);
