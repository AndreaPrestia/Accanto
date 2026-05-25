namespace Accanto.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
}
