namespace Accanto.Api.Configuration;

public class RateLimitOptions
{
    public RateLimitPolicyOptions Login { get; set; } = new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) };
    public RateLimitPolicyOptions Register { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromHours(1) };
    public RateLimitPolicyOptions Sensitive { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
    public RateLimitPolicyOptions InviteCreate { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromHours(1) };
    public RateLimitPolicyOptions Ai { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromHours(1) };
    // CSP report endpoint: ricevuto dal browser senza interazione utente.
    // I browser possono inviare report in burst (uno per violazione). Cap
    // generoso per non perdere dati legittimi su una pagina con piu' bug
    // CSP, ma stretto abbastanza da contenere DoS via flood (curl/bot).
    public RateLimitPolicyOptions CspReport { get; set; } = new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) };
}

public class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; }
    public TimeSpan Window { get; set; }
}
