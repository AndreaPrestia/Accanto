using System.Text.Json;
using Accanto.Application.Ai.Guardrails;
using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Security;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Ai;

public interface IAiService
{
    AiStatusResponse GetStatus();

    Task SetCircleAiEnabledAsync(Guid userId, Guid careCircleId, bool enabled, CancellationToken cancellationToken = default);

    Task<AiResponse> TimelineSummaryAsync(Guid userId, Guid careCircleId, TimelineSummaryRequest request, string? acceptLanguage, CancellationToken cancellationToken = default);

    Task<AiResponse> DoctorQuestionDraftAsync(Guid userId, Guid careCircleId, DoctorQuestionDraftRequest request, string? acceptLanguage, CancellationToken cancellationToken = default);

    Task<AiResponse> RephraseAsync(Guid userId, Guid careCircleId, RephraseRequest request, string? acceptLanguage, CancellationToken cancellationToken = default);

    Task<AiResponse> CheckInReflectionAsync(Guid userId, CheckInReflectionRequest request, string? acceptLanguage, CancellationToken cancellationToken = default);

    Task<AiInteractionListResponse> ListInteractionsAsync(Guid userId, Guid? circleId, AiInteractionFunction? function, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AiInteractionDetail> GetInteractionAsync(Guid userId, Guid interactionId, CancellationToken cancellationToken = default);

    Task SubmitFeedbackAsync(Guid userId, Guid interactionId, string value, CancellationToken cancellationToken = default);
}

public sealed class AiService : IAiService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IAuditLog _audit;
    private readonly ISecurityAuditLog _securityAudit;
    private readonly IAiAssistant _assistant;
    private readonly AiPromptBuilder _prompt;
    private readonly InputGuardrail _inputGuard;
    private readonly OutputGuardrail _outputGuard;
    private readonly AiIdempotencyCache _cache;
    private readonly IAiInteractionStore _store;
    private readonly AiOptions _options;

    public AiService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IAuditLog audit,
        ISecurityAuditLog securityAudit,
        IAiAssistant assistant,
        AiPromptBuilder prompt,
        InputGuardrail inputGuard,
        OutputGuardrail outputGuard,
        AiIdempotencyCache cache,
        IAiInteractionStore store,
        IOptions<AiOptions> options)
    {
        _db = db;
        _auth = auth;
        _audit = audit;
        _securityAudit = securityAudit;
        _assistant = assistant;
        _prompt = prompt;
        _inputGuard = inputGuard;
        _outputGuard = outputGuard;
        _cache = cache;
        _store = store;
        _options = options.Value;
    }

    public AiStatusResponse GetStatus()
        => new(_options.IsConfigured, _options.Provider, _options.Model);

    public async Task SetCircleAiEnabledAsync(Guid userId, Guid careCircleId, bool enabled, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Owner, cancellationToken);
        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Cerchio non trovato.");

        if (circle.AiEnabled == enabled) return;

        circle.AiEnabled = enabled;
        circle.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.AiSettingsUpdated, AuditResourceType.Ai, null,
            enabled ? "ai_enabled" : "ai_disabled", CancellationToken.None);
    }

    // ---------------- Funzioni AI ----------------

    public async Task<AiResponse> TimelineSummaryAsync(Guid userId, Guid careCircleId, TimelineSummaryRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        await EnsureCircleAiEnabled(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        var days = Math.Clamp(request.Days, 1, 60);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var entries = await _db.TimelineEntries
            .Where(e => e.CareCircleId == careCircleId && e.OccurredAt >= since)
            .OrderByDescending(e => e.OccurredAt)
            .Take(50)
            .Select(e => new { e.OccurredAt, e.Type, e.Title, e.Content })
            .ToListAsync(cancellationToken);

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? $"Summarize the following caregiving timeline of the last {days} days in 4-6 bullet points. Highlight recurring themes."
            : $"Riassumi la seguente timeline di assistenza degli ultimi {days} giorni in 4-6 punti. Evidenzia i temi ricorrenti.";

        var context = string.Join("\n", entries.Select(e =>
            $"- {e.OccurredAt:yyyy-MM-dd} [{e.Type}] {e.Title}: {Truncate(e.Content, 240)}"));

        return await RunPipelineAsync(
            userId, careCircleId, AiInteractionFunction.TimelineSummary, lang,
            inputSnapshot: new { request.Days, entriesCount = entries.Count },
            userTexts: Array.Empty<string>(),
            cacheInputKey: $"days={days}|entries={entries.Count}",
            buildPrompt: () => _prompt.BuildSystemPrompt(lang, "Aiuti a riassumere eventi di assistenza familiare.") + "\n\n" +
                              _prompt.BuildUserPrompt(instruction, context),
            callModel: (p, ct) => _assistant.SummarizeTimelineAsync(p, lang, ct),
            auditSummary: $"timeline-summary days={days}",
            cancellationToken);
    }

    public async Task<AiResponse> DoctorQuestionDraftAsync(Guid userId, Guid careCircleId, DoctorQuestionDraftRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        await EnsureCircleAiEnabled(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? "Draft 3 concise, respectful questions a family caregiver could ask a doctor about the topic below. Avoid medical jargon."
            : "Proponi 3 domande concise e rispettose che un caregiver familiare potrebbe rivolgere al medico sull'argomento sotto. Evita gergo medico.";

        var context = $"Argomento: {request.Topic}";
        if (!string.IsNullOrWhiteSpace(request.Notes)) context += $"\nNote: {request.Notes}";

        return await RunPipelineAsync(
            userId, careCircleId, AiInteractionFunction.DoctorQuestionDraft, lang,
            inputSnapshot: request,
            userTexts: new[] { request.Topic, request.Notes ?? string.Empty },
            cacheInputKey: $"topic={request.Topic}|notes={request.Notes}",
            buildPrompt: () => _prompt.BuildSystemPrompt(lang, "Aiuti il caregiver a preparare domande per il medico.") + "\n\n" +
                              _prompt.BuildUserPrompt(instruction, context),
            callModel: (p, ct) => _assistant.DraftDoctorQuestionAsync(p, lang, ct),
            auditSummary: "doctor-question-draft",
            cancellationToken);
    }

    public async Task<AiResponse> RephraseAsync(Guid userId, Guid careCircleId, RephraseRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        await EnsureCircleAiEnabled(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var tone = string.IsNullOrWhiteSpace(request.Tone) ? "neutro" : request.Tone.Trim();
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? $"Rephrase the following short update for sharing with relatives. Keep the meaning, use a {tone} tone, max 4 sentences."
            : $"Riformula il seguente aggiornamento destinato ai familiari. Mantieni il significato, usa un tono {tone}, massimo 4 frasi.";

        return await RunPipelineAsync(
            userId, careCircleId, AiInteractionFunction.Rephrase, lang,
            inputSnapshot: request,
            userTexts: new[] { request.Text, request.Tone ?? string.Empty },
            cacheInputKey: $"text={request.Text}|tone={tone}",
            buildPrompt: () => _prompt.BuildSystemPrompt(lang, "Aiuti a riformulare aggiornamenti familiari.") + "\n\n" +
                              _prompt.BuildUserPrompt(instruction, request.Text),
            callModel: (p, ct) => _assistant.RephraseSharedUpdateAsync(p, lang, ct),
            auditSummary: "rephrase",
            cancellationToken);
    }

    public async Task<AiResponse> CheckInReflectionAsync(Guid userId, CheckInReflectionRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();

        var days = Math.Clamp(request.Days, 1, 60);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var checkIns = await _db.CaregiverCheckIns
            .Where(c => c.UserId == userId && c.CreatedAt >= since)
            .OrderByDescending(c => c.CreatedAt)
            .Take(30)
            .Select(c => new { c.CreatedAt, c.Mood, c.Energy, c.Stress, c.Note })
            .ToListAsync(cancellationToken);

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? $"Reflect briefly (3-5 sentences) on the caregiver's well-being over the last {days} days based on the check-ins below. Be supportive, non-clinical."
            : $"Rifletti brevemente (3-5 frasi) sul benessere del caregiver negli ultimi {days} giorni, basandoti sui check-in sotto. Tono di supporto, non clinico.";

        var context = string.Join("\n", checkIns.Select(c =>
            $"- {c.CreatedAt:yyyy-MM-dd} umore={c.Mood} energia={c.Energy} stress={c.Stress}{(string.IsNullOrWhiteSpace(c.Note) ? "" : $" note=\"{Truncate(c.Note!, 160)}\"")}"));

        return await RunPipelineAsync(
            userId, careCircleId: null, AiInteractionFunction.CheckInReflection, lang,
            inputSnapshot: new { request.Days, checkInsCount = checkIns.Count },
            userTexts: Array.Empty<string>(),
            cacheInputKey: $"days={days}|count={checkIns.Count}",
            buildPrompt: () => _prompt.BuildSystemPrompt(lang, "Offri una breve riflessione sul benessere del caregiver.") + "\n\n" +
                              _prompt.BuildUserPrompt(instruction, context),
            callModel: (p, ct) => _assistant.ReflectCheckInAsync(p, lang, ct),
            auditSummary: "checkin-reflection",
            cancellationToken,
            securityAudit: true);
    }

    // ---------------- Storia / feedback ----------------

    public Task<AiInteractionListResponse> ListInteractionsAsync(Guid userId, Guid? circleId, AiInteractionFunction? function, int page, int pageSize, CancellationToken cancellationToken = default)
        => _store.ListAsync(userId, circleId, function, page, pageSize, cancellationToken);

    public Task<AiInteractionDetail> GetInteractionAsync(Guid userId, Guid interactionId, CancellationToken cancellationToken = default)
        => _store.GetAsync(userId, interactionId, cancellationToken);

    public Task SubmitFeedbackAsync(Guid userId, Guid interactionId, string value, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AiFeedback>(value, ignoreCase: true, out var parsed))
            throw new AppValidationException("feedback value must be one of: up, down, flag");
        return _store.SubmitFeedbackAsync(userId, interactionId, parsed, cancellationToken);
    }

    // ---------------- Pipeline interna ----------------

    private async Task<AiResponse> RunPipelineAsync(
        Guid userId,
        Guid? careCircleId,
        AiInteractionFunction function,
        string language,
        object inputSnapshot,
        string[] userTexts,
        string cacheInputKey,
        Func<string> buildPrompt,
        Func<string, CancellationToken, Task<AiResponse>> callModel,
        string auditSummary,
        CancellationToken ct,
        bool securityAudit = false)
    {
        var fnName = function.ToString();
        var inputJson = JsonSerializer.Serialize(inputSnapshot);
        var cacheKey = _cache.BuildKey(userId, careCircleId, fnName, cacheInputKey);

        // 1. Input guardrail
        var inputDecision = _inputGuard.Inspect(userTexts);
        if (inputDecision.Decision != InputGuardrailDecision.Allow)
        {
            return await HandleBlockedInputAsync(userId, careCircleId, function, language, inputJson, inputDecision, ct);
        }

        // 2. Cache hit
        if (_cache.TryGet(cacheKey, out var cached))
        {
            _ = LogAudit(careCircleId, userId, function, auditSummary + " cache=hit", securityAudit);
            return cached.Response with { CacheHit = true };
        }

        // 3. Model call
        var modelResp = await callModel(buildPrompt(), ct);

        // 4. Output guardrail
        var outputResult = await _outputGuard.InspectAsync(modelResp.Text, language, _assistant, ct);
        var verdict = outputResult.Decision switch
        {
            OutputGuardrailDecision.Passed => AiGuardrailVerdict.Passed,
            OutputGuardrailDecision.OutOfScope => AiGuardrailVerdict.OutOfScope,
            OutputGuardrailDecision.SelfCheckFailed => AiGuardrailVerdict.SelfCheckFailed,
            _ => AiGuardrailVerdict.Passed
        };

        var disclaimer = _prompt.GetDisclaimer(language);

        // 5. Persist
        var entity = await _store.AddAsync(new AiInteraction
        {
            UserId = userId,
            CareCircleId = careCircleId,
            Function = function,
            InputJsonEncrypted = inputJson,
            OutputEncrypted = outputResult.Text,
            Model = modelResp.Model,
            PromptVersion = AiPromptBuilder.PromptVersion,
            TookMs = (int)Math.Min(int.MaxValue, modelResp.TookMs),
            Verdict = verdict,
            Language = language,
            CacheHit = false,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        var finalResp = new AiResponse(outputResult.Text, modelResp.Model, modelResp.TookMs, disclaimer,
            entity.Id, verdict.ToString().ToLowerInvariant(), false);

        // 6. Cache only on Passed
        if (verdict == AiGuardrailVerdict.Passed)
        {
            _cache.Set(cacheKey, new CachedAiResponse(finalResp, entity.Id, language));
        }

        _ = LogAudit(careCircleId, userId, function, $"{auditSummary} verdict={verdict}", securityAudit);

        return finalResp;
    }

    private async Task<AiResponse> HandleBlockedInputAsync(
        Guid userId, Guid? careCircleId, AiInteractionFunction function, string language,
        string inputJson, InputGuardrailResult decision, CancellationToken ct)
    {
        var verdict = decision.Decision switch
        {
            InputGuardrailDecision.BlockInjection => AiGuardrailVerdict.BlockedInput,
            InputGuardrailDecision.OffTopic => AiGuardrailVerdict.OutOfScope,
            InputGuardrailDecision.SelfHarm => AiGuardrailVerdict.SafetyRedirect,
            _ => AiGuardrailVerdict.BlockedInput
        };

        if (decision.Decision == InputGuardrailDecision.BlockInjection)
        {
            await _store.AddAsync(new AiInteraction
            {
                UserId = userId,
                CareCircleId = careCircleId,
                Function = function,
                InputJsonEncrypted = inputJson,
                OutputEncrypted = string.Empty,
                Model = "blocked",
                PromptVersion = AiPromptBuilder.PromptVersion,
                TookMs = 0,
                Verdict = verdict,
                Language = language,
                CacheHit = false,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
            throw new AppValidationException("ai_input_rejected");
        }

        var text = decision.Decision == InputGuardrailDecision.SelfHarm
            ? BuildSafetyMessage(language)
            : OutputGuardrail.OutOfScopeMessage(language);

        var disclaimer = _prompt.GetDisclaimer(language);
        var entity = await _store.AddAsync(new AiInteraction
        {
            UserId = userId,
            CareCircleId = careCircleId,
            Function = function,
            InputJsonEncrypted = inputJson,
            OutputEncrypted = text,
            Model = "guardrail",
            PromptVersion = AiPromptBuilder.PromptVersion,
            TookMs = 0,
            Verdict = verdict,
            Language = language,
            CacheHit = false,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        return new AiResponse(text, "guardrail", 0, disclaimer, entity.Id, verdict.ToString().ToLowerInvariant(), false);
    }

    private string BuildSafetyMessage(string language)
    {
        var isEn = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var contacts = string.Join("\n", _options.SupportContacts.Select(c => $"• {c.Label}: {c.Number}"));
        var intro = isEn
            ? "If you are having thoughts of harming yourself, please reach out to one of the following services right away. You are not alone."
            : "Se hai pensieri di farti del male, contatta subito uno dei seguenti servizi. Non sei solo/a.";
        return intro + "\n\n" + contacts;
    }

    private Task LogAudit(Guid? careCircleId, Guid userId, AiInteractionFunction function, string summary, bool securityAudit)
    {
        if (securityAudit)
        {
            return _securityAudit.LogAsync(userId, SecurityAuditEventType.AiCall, summary,
                cancellationToken: CancellationToken.None);
        }
        return _audit.LogAsync(careCircleId ?? Guid.Empty, userId, AuditActionType.AiCall, AuditResourceType.Ai, null,
            summary, CancellationToken.None);
    }

    private Task EnsureAvailable()
    {
        if (!_options.IsConfigured)
            throw new ServiceUnavailableException("ai_not_configured");
        return Task.CompletedTask;
    }

    private async Task EnsureCircleAiEnabled(Guid userId, Guid careCircleId, CareCircleRole minRole, CancellationToken ct)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, minRole, ct);
        var enabled = await _db.CareCircles
            .Where(c => c.Id == careCircleId)
            .Select(c => (bool?)c.AiEnabled)
            .FirstOrDefaultAsync(ct);
        if (enabled is null) throw new NotFoundException("Cerchio non trovato.");
        if (enabled == false) throw new ForbiddenException("ai_disabled_for_circle");
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");
}
