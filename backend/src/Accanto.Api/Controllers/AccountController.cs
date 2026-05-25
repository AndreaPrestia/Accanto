using Accanto.Api.Common;
using Accanto.Application.Account;
using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _svc;
    private readonly INotificationPreferenceService _prefs;
    private readonly IGdprExportService _export;
    private readonly IRefreshTokenService _sessions;
    private readonly ITwoFactorService _twoFactor;
    private readonly ICurrentUser _currentUser;

    public AccountController(
        IAccountService svc,
        INotificationPreferenceService prefs,
        IGdprExportService export,
        IRefreshTokenService sessions,
        ITwoFactorService twoFactor,
        ICurrentUser currentUser)
    {
        _svc = svc;
        _prefs = prefs;
        _export = export;
        _sessions = sessions;
        _twoFactor = twoFactor;
        _currentUser = currentUser;
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _svc.ChangePasswordAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpDelete]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var prefs = await _prefs.GetAsync(_currentUser.RequireUserId(), ct);
        return Ok(prefs);
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        var prefs = await _prefs.UpdateAsync(_currentUser.RequireUserId(), request, ct);
        return Ok(prefs);
    }

    [HttpPut("language")]
    public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request, CancellationToken ct)
    {
        await _svc.UpdateLanguageAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var result = await _export.ExportAsync(_currentUser.RequireUserId(), ct);
        return File(result.Content, "application/zip", result.FileName);
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<ActiveSessionDto>>> ListSessions(
        [FromQuery(Name = "current")] string? currentRefreshToken,
        CancellationToken ct)
    {
        var list = await _sessions.ListActiveAsync(_currentUser.RequireUserId(), currentRefreshToken, ct);
        return Ok(list);
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        await _sessions.RevokeByIdAsync(_currentUser.RequireUserId(), id, ct);
        return NoContent();
    }

    // ---------- 2FA TOTP ----------

    [HttpGet("2fa")]
    public async Task<ActionResult<TwoFactorStatusDto>> TwoFactorStatus(CancellationToken ct)
        => Ok(await _twoFactor.GetStatusAsync(_currentUser.RequireUserId(), ct));

    [HttpPost("2fa/setup")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<TwoFactorSetupResponse>> TwoFactorSetup(CancellationToken ct)
        => Ok(await _twoFactor.SetupAsync(_currentUser.RequireUserId(), ct));

    [HttpPost("2fa/enable")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<EnableTwoFactorResponse>> TwoFactorEnable([FromBody] EnableTwoFactorRequest request, CancellationToken ct)
        => Ok(await _twoFactor.EnableAsync(_currentUser.RequireUserId(), request, ct));

    [HttpPost("2fa/disable")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> TwoFactorDisable([FromBody] DisableTwoFactorRequest request, CancellationToken ct)
    {
        await _twoFactor.DisableAsync(_currentUser.RequireUserId(), request, ct);
        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<EnableTwoFactorResponse>> TwoFactorRegenerateCodes([FromBody] RegenerateRecoveryCodesRequest request, CancellationToken ct)
        => Ok(await _twoFactor.RegenerateRecoveryCodesAsync(_currentUser.RequireUserId(), request, ct));
}
