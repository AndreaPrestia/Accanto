using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class CareCircleInvite
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Token opaco usato nell'URL di invito. Generato come 32 byte casuali in base64url.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Ruolo che verrà assegnato a chi accetta l'invito.
    /// Solo Caregiver o Viewer: non si possono creare altri Owner via invito.
    /// </summary>
    public CareCircleRole Role { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
