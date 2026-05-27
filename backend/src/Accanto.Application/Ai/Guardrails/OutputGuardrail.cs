using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Ai.Guardrails;

public enum OutputGuardrailDecision
{
    Passed,
    OutOfScope,
    SelfCheckFailed
}

public sealed class OutputGuardrailResult
{
    public OutputGuardrailDecision Decision { get; init; }
    /// <summary>Testo finale da restituire all'utente (post-cap + post-redaction).</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Layer C — validazione della risposta del modello prima di restituirla all'utente.
/// Esegue, in ordine:
///   1) detect sentinella "fuori_scopo" → messaggio canonico localizzato
///   2) length cap (config <see cref="AiOptions.MaxOutputChars"/>) con ellipsis
///   3) PII redaction (riusa <see cref="AiPromptBuilder.RedactPii"/>)
///   4) self-check LLM (config <see cref="AiOptions.SelfCheckEnabled"/>): seconda chiamata
///      minima al modello, "SI/NO è pertinente al caregiving?". Su NO → messaggio canonico.
/// </summary>
public sealed class OutputGuardrail
{
    private readonly AiOptions _options;
    private readonly AiPromptBuilder _prompt;
    private readonly ILogger<OutputGuardrail> _logger;

    public OutputGuardrail(IOptions<AiOptions> options, AiPromptBuilder prompt, ILogger<OutputGuardrail> logger)
    {
        _options = options.Value;
        _prompt = prompt;
        _logger = logger;
    }

    public async Task<OutputGuardrailResult> InspectAsync(string modelText, string language, IAiAssistant assistant, CancellationToken ct)
    {
        var trimmed = (modelText ?? string.Empty).Trim();

        // 1) sentinella fuori_scopo (case-insensitive, anche con punteggiatura accidentale)
        if (LooksLikeOutOfScope(trimmed))
        {
            return new OutputGuardrailResult
            {
                Decision = OutputGuardrailDecision.OutOfScope,
                Text = OutOfScopeMessage(language)
            };
        }

        // 2) length cap
        var capped = Cap(trimmed, _options.MaxOutputChars);

        // 3) PII redaction sull'output (parità rispetto all'input)
        var redacted = _prompt.RedactPii(capped);

        // 4) self-check LLM
        if (_options.SelfCheckEnabled)
        {
            var ok = await SelfCheckAsync(redacted, language, assistant, ct);
            if (!ok)
            {
                return new OutputGuardrailResult
                {
                    Decision = OutputGuardrailDecision.SelfCheckFailed,
                    Text = OutOfScopeMessage(language)
                };
            }
        }

        return new OutputGuardrailResult
        {
            Decision = OutputGuardrailDecision.Passed,
            Text = redacted
        };
    }

    private static bool LooksLikeOutOfScope(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var first = text.Split('\n', 2)[0].Trim().Trim('.', ',', '!', '?', '"', '\'', '`').ToLowerInvariant();
        return first == AiPromptBuilder.OutOfScopeSentinel;
    }

    private static string Cap(string text, int max)
    {
        if (max <= 0 || string.IsNullOrEmpty(text)) return text ?? string.Empty;
        return text.Length <= max ? text : text[..max] + "…";
    }

    private async Task<bool> SelfCheckAsync(string responseText, string language, IAiAssistant assistant, CancellationToken ct)
    {
        var isEn = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var prompt = isEn
            ? "You review whether the ASSISTANT REPLY below is appropriate for a family-caregiving app. " +
              "It is APPROPRIATE if it talks about a sick or fragile relative, the caregiver's feelings, " +
              "updates to share with family, messages to or from doctors, daily care, logistics, emotional support. " +
              "It is INAPPROPRIATE only if it is clearly off-topic (politics, finance, programming, sexual content) " +
              "or contains explicit medical/legal/financial advice with prescriptions. " +
              "Reply with exactly one token: YES (appropriate) or NO (inappropriate). Default to YES if unsure.\n\n" +
              "ASSISTANT REPLY:\n" + responseText
            : "Devi valutare se la RISPOSTA DELL'ASSISTENTE qui sotto è adeguata per un'app di caregiving familiare. " +
              "È ADEGUATA se parla di un familiare malato o fragile, dei sentimenti del caregiver, " +
              "di aggiornamenti da condividere con la famiglia, messaggi da/per i medici, cura quotidiana, logistica, supporto emotivo. " +
              "È INADEGUATA solo se è chiaramente fuori tema (politica, finanza, programmazione, contenuti sessuali) " +
              "o contiene pareri medici/legali/finanziari espliciti con prescrizioni. " +
              "Rispondi con un solo token: SI (adeguata) oppure NO (inadeguata). Se hai dubbi, rispondi SI.\n\n" +
              "RISPOSTA DELL'ASSISTENTE:\n" + responseText;

        try
        {
            var verdict = await assistant.GenerateAsync(prompt, language, maxTokens: 8, cancellationToken: ct);
            var raw = (verdict.Text ?? string.Empty).Trim();
            // Estrai il primo "token alfabetico" (massimo 6 lettere) della risposta del reviewer.
            // Accetta come PASS qualsiasi inizio con sì/si/yes; come FAIL solo "no"/"non".
            // Default → PASS (i 3B sbagliano spesso il formato; meglio non bloccare l'utente).
            var first = new string(raw.TakeWhile(c => char.IsLetter(c) || c == 'ì' || c == 'Ì').Take(6).ToArray())
                              .ToLowerInvariant();
            var passed = first.StartsWith("si") || first.StartsWith("sì") || first.StartsWith("yes");
            var failed = first == "no" || first == "non";
            if (failed)
            {
                _logger.LogInformation("AI self-check verdict NO (raw='{Raw}')", Truncate(raw, 60));
                return false;
            }
            if (!passed)
            {
                _logger.LogDebug("AI self-check ambiguous verdict (raw='{Raw}') → default PASS", Truncate(raw, 60));
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Self-check LLM call failed; treating as pass to avoid blocking legitimate replies.");
            return true;
        }
    }

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s ?? string.Empty : s[..max] + "…";

    public static string OutOfScopeMessage(string language)
        => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? "I can only help with family caregiving topics. Try reformulating your request around caregiving for a relative."
            : "Posso aiutarti solo su temi legati al caregiving familiare. Prova a riformulare la richiesta concentrandoti sull'assistenza a un familiare.";
}
