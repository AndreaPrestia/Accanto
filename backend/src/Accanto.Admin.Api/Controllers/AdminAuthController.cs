using Accanto.Admin.Api.Common;
using Accanto.Admin.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Accanto.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _auth;
    private readonly ICurrentAdmin _currentAdmin;

    public AdminAuthController(IAdminAuthService auth, ICurrentAdmin currentAdmin)
    {
        _auth = auth;
        _currentAdmin = currentAdmin;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("admin-auth-login")]
    public async Task<ActionResult<AdminAuthResponse>> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, BuildClientInfo(), ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("admin-auth-login")]
    public async Task<ActionResult<AdminAuthResponse>> Refresh([FromBody] AdminRefreshRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request, BuildClientInfo(), ct));

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request, ct);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AdminUserDto>> Me(CancellationToken ct)
        => Ok(await _auth.GetMeAsync(_currentAdmin.RequireAdminUserId(), ct));

    private AdminClientInfo BuildClientInfo()
    {
        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return new AdminClientInfo(string.IsNullOrWhiteSpace(ua) ? null : ua, ip);
    }
}
