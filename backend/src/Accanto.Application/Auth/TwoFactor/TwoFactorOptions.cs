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
}
