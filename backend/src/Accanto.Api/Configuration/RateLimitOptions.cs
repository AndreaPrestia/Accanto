namespace Accanto.Api.Configuration;

public class RateLimitOptions
{
    public RateLimitPolicyOptions Login { get; set; } = new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) };
    public RateLimitPolicyOptions Register { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromHours(1) };
    public RateLimitPolicyOptions Sensitive { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
    public RateLimitPolicyOptions InviteCreate { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromHours(1) };
    public RateLimitPolicyOptions Ai { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromHours(1) };
}

public class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; }
    public TimeSpan Window { get; set; }
}
