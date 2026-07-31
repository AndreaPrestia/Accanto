namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Token monouso per il reset della password admin. Persiste SOLO l'hash SHA-256
/// del valore in chiaro: il token esiste fuori dal DB solo nel link inviato via
/// email. Vive in AccantoAdminDb, mai nel DB pubblico. <see cref="UsedAt"/>
/// valorizzato al primo consumo.
/// </summary>
public sealed class AdminPasswordResetToken
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }

    /// <summary>SHA-256 esadecimale del token in chiaro. Mai salvare il token raw.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Quando il token e' stato consumato. NULL = ancora valido.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public AdminUser AdminUser { get; set; } = default!;

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;
}
