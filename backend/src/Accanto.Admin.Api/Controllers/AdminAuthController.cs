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
    private readonly IAdminPasswordResetService _reset;
    private readonly ICurrentAdmin _currentAdmin;

    public AdminAuthController(IAdminAuthService auth, IAdminPasswordResetService reset, ICurrentAdmin currentAdmin)
    {
        _auth = auth;
        _reset = reset;
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

    // Logout richiede un Admin JWT valido ([Authorize] ereditato dal controller
    // non e' presente qui, quindi lo dichiariamo esplicito): l'admin deve essere
    // autenticato per terminare la propria sessione. Solo login/refresh restano
    // anonimi. Soddisfa anche il controllo CodeQL di access-control a livello di
    // funzione (nessun endpoint autenticato marcato AllowAnonymous per errore).
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request, ct);
        return Ok(new { ok = true });
    }

    // Reset password admin. Anonimo + rate-limited. forgot-password risponde
    // sempre 204 (anti-enumerazione). Nessun account viene creato qui: il flusso
    // agisce solo su admin gia' esistenti (seedati).
    // forgot/reset DEVONO essere anonimi: chi ha dimenticato la password (o fa il
    // primo accesso dopo il seed senza password) non e' autenticato. Sono protetti
    // da rate limit, anti-enumerazione e token monouso. Il warning CodeQL
    // "missing function level access control" e' un falso positivo qui.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("admin-auth-login")]
    public async Task<IActionResult> ForgotPassword([FromBody] AdminForgotPasswordRequest request, CancellationToken ct)
    {
        await _reset.RequestResetAsync(request, BuildClientInfo(), ct);
        return NoContent();
    }

    // codeql[cs/web/missing-function-level-access-control]: endpoint pubblico
    // per intento (reset password con token monouso); autenticazione qui sarebbe
    // impossibile per l'utente che deve reimpostare la password.
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("admin-auth-login")]
    public async Task<IActionResult> ResetPassword([FromBody] AdminResetPasswordRequest request, CancellationToken ct)
    {
        await _reset.ResetAsync(request, BuildClientInfo(), ct);
        return NoContent();
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
