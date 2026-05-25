namespace Accanto.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Language { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Lockout dopo N tentativi di login falliti.
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }
    public DateTimeOffset? LastFailedLoginAt { get; set; }

    // 2FA TOTP. Segreti cifrati con IFieldProtector prima di persistere.
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? TwoFactorPendingSecret { get; set; }
    /// <summary>JSON array di hash SHA-256 (hex) dei codici di recupero non ancora usati.</summary>
    public string? TwoFactorRecoveryCodesJson { get; set; }
}
