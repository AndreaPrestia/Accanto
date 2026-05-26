using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Security;
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
}

public sealed class AiService : IAiService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IAuditLog _audit;
    private readonly ISecurityAuditLog _securityAudit;
    private readonly IAiAssistant _assistant;
    private readonly AiPromptBuilder _prompt;
    private readonly AiOptions _options;

    public AiService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IAuditLog audit,
        ISecurityAuditLog securityAudit,
        IAiAssistant assistant,
        AiPromptBuilder prompt,
        IOptions<AiOptions> options)
    {
        _db = db;
        _auth = auth;
        _audit = audit;
        _securityAudit = securityAudit;
        _assistant = assistant;
        _prompt = prompt;
        _options = options.Value;
    }

    public AiStatusResponse GetStatus()
        => new(_options.IsConfigured, _options.Provider, _options.Model);

    public async Task SetCircleAiEnabledAsync(Guid userId, Guid careCircleId, bool enabled, CancellationToken cancellationToken = default)
    {
        // Solo l'owner del cerchio può cambiare il flag.
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

        var system = _prompt.BuildSystemPrompt(lang, "Aiuti a riassumere eventi di assistenza familiare.");
        var user = _prompt.BuildUserPrompt(instruction, context);
        var prompt = system + "\n\n" + user;

        var response = await _assistant.SummarizeTimelineAsync(prompt, lang, cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.AiCall, AuditResourceType.Ai, null,
            $"timeline-summary days={days}", CancellationToken.None);

        return response with { Disclaimer = _prompt.GetDisclaimer(lang) };
    }

    public async Task<AiResponse> DoctorQuestionDraftAsync(Guid userId, Guid careCircleId, DoctorQuestionDraftRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        await EnsureCircleAiEnabled(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Topic))
            throw new AppValidationException("Argomento richiesto.");

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? "Draft 3 concise, respectful questions a family caregiver could ask a doctor about the topic below. Avoid medical jargon."
            : "Proponi 3 domande concise e rispettose che un caregiver familiare potrebbe rivolgere al medico sull'argomento sotto. Evita gergo medico.";

        var context = $"Argomento: {request.Topic}";
        if (!string.IsNullOrWhiteSpace(request.Notes)) context += $"\nNote: {request.Notes}";

        var system = _prompt.BuildSystemPrompt(lang, "Aiuti il caregiver a preparare domande per il medico.");
        var user = _prompt.BuildUserPrompt(instruction, context);
        var prompt = system + "\n\n" + user;

        var response = await _assistant.DraftDoctorQuestionAsync(prompt, lang, cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.AiCall, AuditResourceType.Ai, null,
            "doctor-question-draft", CancellationToken.None);

        return response with { Disclaimer = _prompt.GetDisclaimer(lang) };
    }

    public async Task<AiResponse> RephraseAsync(Guid userId, Guid careCircleId, RephraseRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        await EnsureCircleAiEnabled(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new AppValidationException("Testo richiesto.");

        var lang = _prompt.ResolveLanguage(acceptLanguage);
        var tone = string.IsNullOrWhiteSpace(request.Tone) ? "neutro" : request.Tone.Trim();
        var instruction = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)
            ? $"Rephrase the following short update for sharing with relatives. Keep the meaning, use a {tone} tone, max 4 sentences."
            : $"Riformula il seguente aggiornamento destinato ai familiari. Mantieni il significato, usa un tono {tone}, massimo 4 frasi.";

        var system = _prompt.BuildSystemPrompt(lang, "Aiuti a riformulare aggiornamenti familiari.");
        var user = _prompt.BuildUserPrompt(instruction, request.Text);
        var prompt = system + "\n\n" + user;

        var response = await _assistant.RephraseSharedUpdateAsync(prompt, lang, cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.AiCall, AuditResourceType.Ai, null,
            "rephrase", CancellationToken.None);

        return response with { Disclaimer = _prompt.GetDisclaimer(lang) };
    }

    public async Task<AiResponse> CheckInReflectionAsync(Guid userId, CheckInReflectionRequest request, string? acceptLanguage, CancellationToken cancellationToken = default)
    {
        await EnsureAvailable();
        // Funzione personale: non richiede un cerchio, ma richiede solo essere autenticati.
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

        var system = _prompt.BuildSystemPrompt(lang, "Offri una breve riflessione sul benessere del caregiver.");
        var user = _prompt.BuildUserPrompt(instruction, context);
        var prompt = system + "\n\n" + user;

        var response = await _assistant.ReflectCheckInAsync(prompt, lang, cancellationToken);

        _ = _securityAudit.LogAsync(userId, SecurityAuditEventType.AiCall, "checkin-reflection",
            cancellationToken: CancellationToken.None);

        return response with { Disclaimer = _prompt.GetDisclaimer(lang) };
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
