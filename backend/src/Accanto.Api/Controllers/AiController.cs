using Accanto.Api.Common;
using Accanto.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Accanto.Api.Controllers;

/// <summary>
/// Endpoint per le funzioni AI. Tutte le funzioni sono opt-in:
/// - <c>GET /api/ai/status</c> riporta se il modulo è abilitato a livello di sistema
/// - Le funzioni per cerchio richiedono <c>CareCircle.AiEnabled = true</c> (gate 403 <c>ai_disabled_for_circle</c>)
/// - Se il provider non è configurato (<c>AiOptions.Provider == "none"</c>) viene restituito 503 <c>ai_not_configured</c>
/// </summary>
[ApiController]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _svc;
    private readonly ICurrentUser _currentUser;

    public AiController(IAiService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet("api/ai/status")]
    public ActionResult<AiStatusResponse> Status() => Ok(_svc.GetStatus());

    public record SetAiSettingsRequest(bool Enabled);

    [HttpPut("api/care-circles/{careCircleId:guid}/ai/settings")]
    public async Task<IActionResult> SetCircleSettings(Guid careCircleId, [FromBody] SetAiSettingsRequest request, CancellationToken ct)
    {
        await _svc.SetCircleAiEnabledAsync(_currentUser.RequireUserId(), careCircleId, request.Enabled, ct);
        return NoContent();
    }

    [HttpPost("api/care-circles/{careCircleId:guid}/ai/timeline-summary")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> TimelineSummary(Guid careCircleId, [FromBody] TimelineSummaryRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        return Ok(await _svc.TimelineSummaryAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct));
    }

    [HttpPost("api/care-circles/{careCircleId:guid}/ai/doctor-question-draft")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> DoctorQuestionDraft(Guid careCircleId, [FromBody] DoctorQuestionDraftRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        return Ok(await _svc.DoctorQuestionDraftAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct));
    }

    [HttpPost("api/care-circles/{careCircleId:guid}/ai/rephrase")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> Rephrase(Guid careCircleId, [FromBody] RephraseRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        return Ok(await _svc.RephraseAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct));
    }

    [HttpPost("api/me/ai/checkin-reflection")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> CheckInReflection([FromBody] CheckInReflectionRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        return Ok(await _svc.CheckInReflectionAsync(_currentUser.RequireUserId(), request, lang, ct));
    }
}
