namespace Accanto.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; set; } = "accanto";
    public string Audience { get; set; } = "accanto";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}
