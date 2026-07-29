namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Sessione admin (refresh token). Persiste SOLO l'hash del refresh token:
/// il token in chiaro non viene mai salvato. La revoca imposta <see cref="RevokedAt"/>.
/// </summary>
public sealed class AdminSession
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }

    /// <summary>SHA-256 esadecimale del refresh token in chiaro: mai salvare il token raw.</summary>
    public string RefreshTokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public AdminUser AdminUser { get; set; } = default!;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
