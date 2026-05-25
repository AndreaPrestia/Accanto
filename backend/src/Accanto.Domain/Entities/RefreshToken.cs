namespace Accanto.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 esadecimale del token in chiaro: il token non viene mai salvato in DB.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Quando una sessione viene rinnovata (rotazione) il vecchio token punta al nuovo.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
