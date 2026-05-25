namespace Accanto.Application.Auth;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string DisplayName, string? Language, DateTimeOffset CreatedAt);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

/// <summary>Metadati del client per tracciare la sessione (User-Agent, IP).</summary>
public sealed record ClientInfo(string? UserAgent, string? IpAddress);

public sealed record ActiveSessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? IpAddress,
    bool Current);
