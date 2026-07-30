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

    /// <summary>
    /// Disabilitazione amministrativa dell'account (operazione del control plane
    /// admin). Quando true il login e' impossibile. NON e' un flag di ruolo:
    /// resta vietato User.IsAdmin. La cancellazione GDPR resta separata (IsErased).
    /// </summary>
    public bool IsDisabled { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    /// <summary>Motivazione testuale della disabilitazione (metadata, non contenuto).</summary>
    public string? DisabledReason { get; set; }

    // 2FA TOTP. Segreti cifrati con IFieldProtector prima di persistere.
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? TwoFactorPendingSecret { get; set; }
    /// <summary>JSON array di hash SHA-256 (hex) dei codici di recupero non ancora usati.</summary>
    public string? TwoFactorRecoveryCodesJson { get; set; }

    /// <summary>
    /// Scadenza della grace window dopo cui 2FA diventa obbligatorio per questo
    /// utente. Settato al momento della promozione a Owner (o backfill alla
    /// migration di rollout). NULL = nessun obbligo (utente mai stato Owner).
    /// </summary>
    public DateTimeOffset? TwoFactorRequiredFromUtc { get; set; }

    /// <summary>
    /// Tombstone GDPR. Quando true l'utente e' stato cancellato
    /// (right-to-erasure): email/displayname sostituiti con placeholder,
    /// password e 2FA azzerati, login impossibile. Lo storico audit
    /// resta intatto per compliance/forensics.
    /// </summary>
    public bool IsErased { get; set; }
    public DateTimeOffset? ErasedAt { get; set; }
    /// <summary>Motivazione testuale (richiesta utente, comando admin, ...).</summary>
    public string? ErasureReason { get; set; }
}
