namespace Accanto.Admin.Application.Auth;

public interface IAdminAuthService
{
    Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<AdminAuthResponse> RefreshAsync(AdminRefreshRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(AdminLogoutRequest request, CancellationToken cancellationToken = default);
    Task<AdminUserDto> GetMeAsync(Guid adminUserId, CancellationToken cancellationToken = default);
}
