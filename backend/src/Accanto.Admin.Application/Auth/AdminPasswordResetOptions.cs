namespace Accanto.Admin.Application.Auth;

/// <summary>
/// Config del flusso di reset password admin. <see cref="PublicUrl"/> e' la base
/// URL dell'admin-web usata per costruire il link
/// (https://admin.example.com/reset-password?token=...). Se vuota, fallback al
/// primo origin di AdminCors:AllowedOrigins.
/// </summary>
public class AdminPasswordResetOptions
{
    public string PublicUrl { get; set; } = string.Empty;
    public string ResetPath { get; set; } = "/reset-password";
    public int TokenLifetimeMinutes { get; set; } = 60;
}
