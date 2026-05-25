using Accanto.Api.Common;
using Accanto.Application.Account;
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
    private readonly ICurrentUser _currentUser;

    public AccountController(IAccountService svc, INotificationPreferenceService prefs, IGdprExportService export, ICurrentUser currentUser)
    {
        _svc = svc;
        _prefs = prefs;
        _export = export;
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
}
