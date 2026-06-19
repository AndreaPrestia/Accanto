namespace Accanto.Application.Auth;

/// <summary>
/// Configurazione del flusso di reset password. La <see cref="PublicUrl"/>
/// e' la base URL della SPA usata per costruire il link di reset
/// (https://app.example.com/reset-password?token=...). Se vuota in setup,
/// si fa fallback al primo origin di <c>Cors:AllowedOrigins</c>.
/// </summary>
public class PasswordResetOptions
{
    /// <summary>Base URL della SPA, senza slash finale.</summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Path della pagina reset (default <c>/reset-password</c>).</summary>
    public string ResetPath { get; set; } = "/reset-password";

    /// <summary>Validita' del token in minuti (default 60).</summary>
    public int TokenLifetimeMinutes { get; set; } = 60;
}
