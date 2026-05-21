namespace Accanto.Application.Auth;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
