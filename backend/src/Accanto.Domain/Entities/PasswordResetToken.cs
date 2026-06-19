namespace Accanto.Domain.Entities;

/// <summary>
/// Token monouso emesso quando un utente richiede il reset password
/// (POST /api/auth/forgot-password). Persistiamo solo l'hash SHA-256 del
/// valore in chiaro, che esiste fuori dal DB solo nel link mandato per
/// email. Scadenza tipica 60 min; UsedAt valorizzato al primo consumo
/// (POST /api/auth/reset-password).
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) del token in chiaro.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Quando il token e' stato consumato. NULL = ancora valido.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
