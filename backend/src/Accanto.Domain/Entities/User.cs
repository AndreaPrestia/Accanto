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
}
