using Accanto.Api.Common;
using Accanto.Application.Ai;
using Accanto.Domain.Enums;
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
        var resp = await _svc.TimelineSummaryAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct);
        SetCacheHeader(resp);
        return Ok(resp);
    }

    [HttpPost("api/care-circles/{careCircleId:guid}/ai/doctor-question-draft")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> DoctorQuestionDraft(Guid careCircleId, [FromBody] DoctorQuestionDraftRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        var resp = await _svc.DoctorQuestionDraftAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct);
        SetCacheHeader(resp);
        return Ok(resp);
    }

    [HttpPost("api/care-circles/{careCircleId:guid}/ai/rephrase")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> Rephrase(Guid careCircleId, [FromBody] RephraseRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        var resp = await _svc.RephraseAsync(_currentUser.RequireUserId(), careCircleId, request, lang, ct);
        SetCacheHeader(resp);
        return Ok(resp);
    }

    [HttpPost("api/me/ai/checkin-reflection")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<AiResponse>> CheckInReflection([FromBody] CheckInReflectionRequest request, CancellationToken ct)
    {
        var lang = Request.Headers.AcceptLanguage.ToString();
        var resp = await _svc.CheckInReflectionAsync(_currentUser.RequireUserId(), request, lang, ct);
        SetCacheHeader(resp);
        return Ok(resp);
    }

    // ---------------- Cronologia / feedback ----------------

    /// <summary>
    /// Lista paginata delle interazioni AI visibili all'utente.
    /// - Senza <c>circleId</c>: solo le proprie interazioni
    /// - Con <c>circleId</c>: richiesto essere membro. Owner del cerchio vede anche quelle degli altri membri
    ///   per quel cerchio (CheckInReflection esclusa: è sempre personale)
    /// </summary>
    [HttpGet("api/ai/interactions")]
    public async Task<ActionResult<AiInteractionListResponse>> List(
        [FromQuery] Guid? circleId,
        [FromQuery] string? function,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        AiInteractionFunction? fn = null;
        if (!string.IsNullOrWhiteSpace(function))
        {
            if (!Enum.TryParse<AiInteractionFunction>(function, ignoreCase: true, out var parsed))
                return BadRequest(new { error = "invalid_function" });
            fn = parsed;
        }
        var result = await _svc.ListInteractionsAsync(_currentUser.RequireUserId(), circleId, fn, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Dettaglio (input + output decifrati) di una singola interazione.</summary>
    [HttpGet("api/ai/interactions/{id:guid}")]
    public async Task<ActionResult<AiInteractionDetail>> GetById(Guid id, CancellationToken ct)
    {
        var detail = await _svc.GetInteractionAsync(_currentUser.RequireUserId(), id, ct);
        return Ok(detail);
    }

    /// <summary>Invia feedback (up/down/flag) su una interazione. Solo l'autore.</summary>
    [HttpPost("api/ai/interactions/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, [FromBody] SubmitAiFeedbackRequest request, CancellationToken ct)
    {
        await _svc.SubmitFeedbackAsync(_currentUser.RequireUserId(), id, request.Value, ct);
        return NoContent();
    }

    private void SetCacheHeader(AiResponse resp)
    {
        Response.Headers["X-AI-Cache"] = resp.CacheHit ? "hit" : "miss";
        Response.Headers["X-AI-Verdict"] = resp.Verdict;
    }
}
