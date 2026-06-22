using Accanto.Api.Common;
using Accanto.Application.Account;
using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.Notifications;
using Accanto.Application.Push;
using Accanto.Application.Security;
using Accanto.Application.Wellbeing;
using Accanto.Domain.Enums;
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
    private readonly ISecurityAuditLog _audit;
    private readonly ISecurityAuditQueryService _auditQuery;
    private readonly ICheckInService _checkIns;
    private readonly IDevicePushTokenService _devicePushTokens;
    private readonly ICircleMobilePushNotifier _mobilePush;
    private readonly ICurrentUser _currentUser;

    public AccountController(
        IAccountService svc,
        INotificationPreferenceService prefs,
        IGdprExportService export,
        IRefreshTokenService sessions,
        ITwoFactorService twoFactor,
        ISecurityAuditLog audit,
        ISecurityAuditQueryService auditQuery,
        ICheckInService checkIns,
        IDevicePushTokenService devicePushTokens,
        ICircleMobilePushNotifier mobilePush,
        ICurrentUser currentUser)
    {
        _svc = svc;
        _prefs = prefs;
        _export = export;
        _sessions = sessions;
        _twoFactor = twoFactor;
        _audit = audit;
        _auditQuery = auditQuery;
        _checkIns = checkIns;
        _devicePushTokens = devicePushTokens;
        _mobilePush = mobilePush;
        _currentUser = currentUser;
    }
    [HttpPost("change-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _svc.ChangePasswordAsync(_currentUser.RequireUserId(), request, BuildClientInfo(), ct);
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
        var userId = _currentUser.RequireUserId();
        await _sessions.RevokeByIdAsync(userId, id, ct);
        await _audit.LogAsync(userId, SecurityAuditEventType.SessionRevoked, $"Sessione {id}", client: BuildClientInfo(), cancellationToken: ct);
        return NoContent();
    }

    [HttpGet("security-audit")]
    public async Task<ActionResult<Application.Common.PagedResult<SecurityAuditEntryDto>>> SecurityAudit(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await _auditQuery.ListForUserAsync(_currentUser.RequireUserId(), skip, take, ct);
        return Ok(result);
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

    // ---------- Wellbeing check-in ----------

    [HttpGet("check-ins")]
    public async Task<ActionResult<IReadOnlyList<CaregiverCheckInDto>>> ListCheckIns(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int take = 60,
        CancellationToken ct = default)
        => Ok(await _checkIns.ListAsync(_currentUser.RequireUserId(), from, to, take, ct));

    [HttpPost("check-ins")]
    public async Task<ActionResult<CaregiverCheckInDto>> CreateCheckIn([FromBody] CreateCheckInRequest request, CancellationToken ct)
    {
        var created = await _checkIns.CreateAsync(_currentUser.RequireUserId(), request, ct);
        return Created($"/api/account/check-ins/{created.Id}", created);
    }

    [HttpDelete("check-ins/{id:guid}")]
    public async Task<IActionResult> DeleteCheckIn(Guid id, CancellationToken ct)
    {
        await _checkIns.DeleteAsync(_currentUser.RequireUserId(), id, ct);
        return NoContent();
    }

    // ---------- Mobile push devices (Expo) ----------

    [HttpGet("push-devices")]
    public async Task<ActionResult<IReadOnlyList<DevicePushTokenDto>>> ListPushDevices(CancellationToken ct)
        => Ok(await _devicePushTokens.ListAsync(_currentUser.RequireUserId(), ct));

    [HttpPost("push-devices")]
    public async Task<ActionResult<DevicePushTokenDto>> RegisterPushDevice([FromBody] RegisterDevicePushTokenRequest request, CancellationToken ct)
    {
        var dto = await _devicePushTokens.RegisterAsync(_currentUser.RequireUserId(), request, ct);
        return Ok(dto);
    }

    [HttpDelete("push-devices/{id:guid}")]
    public async Task<IActionResult> DeletePushDeviceById(Guid id, CancellationToken ct)
    {
        await _devicePushTokens.RemoveByIdAsync(_currentUser.RequireUserId(), id, ct);
        return NoContent();
    }

    /// <summary>
    /// Cancellazione "by token" usata dal client mobile in fase di
    /// logout: il device conosce il proprio token Expo ma non il GUID
    /// del record DB.
    /// </summary>
    [HttpDelete("push-devices")]
    public async Task<IActionResult> DeletePushDeviceByToken([FromBody] DeletePushDeviceRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token)) return BadRequest();
        await _devicePushTokens.RemoveByTokenAsync(_currentUser.RequireUserId(), request.Token, ct);
        return NoContent();
    }

    /// <summary>
    /// Invia una notifica push di prova a tutti i device dell'utente
    /// corrente. Bypassa le preferenze (testare la connessione non deve
    /// essere silenziato dai topic flags). Utile per validare end-to-end
    /// la pipeline (token registrato, Expo push service, APNs/FCM).
    /// </summary>
    [HttpPost("push-devices/test")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> SendTestPush(CancellationToken ct)
    {
        await _mobilePush.SendTestAsync(
            _currentUser.RequireUserId(),
            "Accanto",
            "Notifica di prova: tutto funziona \ud83c\udf89",
            ct);
        return Accepted();
    }

    private ClientInfo BuildClientInfo()
    {
        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return new ClientInfo(string.IsNullOrWhiteSpace(ua) ? null : ua, ip);
    }
}
