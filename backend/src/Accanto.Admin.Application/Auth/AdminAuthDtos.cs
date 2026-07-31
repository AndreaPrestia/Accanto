namespace Accanto.Admin.Application.Auth;

public sealed record AdminLoginRequest(string Email, string Password);
public sealed record AdminRefreshRequest(string RefreshToken);
public sealed record AdminLogoutRequest(string RefreshToken);
public sealed record AdminForgotPasswordRequest(string Email);
public sealed record AdminResetPasswordRequest(string Token, string NewPassword);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record AdminAuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AdminUserDto AdminUser);

/// <summary>Metadati client per tracciare la sessione admin (User-Agent, IP).</summary>
public sealed record AdminClientInfo(string? UserAgent, string? IpAddress);
