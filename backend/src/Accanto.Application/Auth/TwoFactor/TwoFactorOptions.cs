namespace Accanto.Application.Auth.TwoFactor;

/// <summary>
/// Configurazione del 2FA TOTP.
/// </summary>
public class TwoFactorOptions
{
    /// <summary>Nome dell'issuer mostrato nelle app TOTP (Google Authenticator, Authy, ecc.).</summary>
    public string Issuer { get; set; } = "Accanto";

    /// <summary>Durata in minuti del challenge token emesso dopo la prima fase di login.</summary>
    public int ChallengeLifetimeMinutes { get; set; } = 5;

    /// <summary>Numero di codici di recupero generati ogni volta.</summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>
    /// Se true, gli utenti che sono Owner di almeno un care circle DEVONO
    /// configurare 2FA. Oltre la grace window ricevono 403 su tutti gli
    /// endpoint (eccetto la setup whitelist). False disattiva l'enforcement
    /// (dev/test, o backout di emergenza).
    /// </summary>
    public bool RequireForOwners { get; set; } = true;

    /// <summary>
    /// Ore di grace dal momento in cui un utente diventa Owner (o dal
    /// backfill di rollout) prima che l'enforcement scatti. Default 168 = 7gg.
    /// </summary>
    public int OwnerGraceHours { get; set; } = 168;
}
