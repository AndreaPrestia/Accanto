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
            ? "You are a strict reviewer. Decide if the ASSISTANT REPLY below is strictly about family caregiving " +
              "and does NOT contain direct medical, legal or financial advice, instructions to perform code, off-topic content, " +
              "or anything unrelated to caregiving. Answer with ONE word: YES or NO. Nothing else.\n\n" +
              "ASSISTANT REPLY:\n" + responseText
            : "Sei un revisore severo. Decidi se la RISPOSTA DELL'ASSISTENTE qui sotto è strettamente sul caregiving familiare " +
              "e NON contiene pareri medici/legali/finanziari diretti, istruzioni di codice, contenuti fuori tema o non pertinenti. " +
              "Rispondi con UNA sola parola: SI oppure NO. Niente altro.\n\n" +
              "RISPOSTA DELL'ASSISTENTE:\n" + responseText;

        try
        {
            var verdict = await assistant.GenerateAsync(prompt, language, maxTokens: 4, cancellationToken: ct);
            var token = (verdict.Text ?? string.Empty).Trim().Trim('.', ',', '!', '?', '"', '\'', '`').ToLowerInvariant();
            // accetta "si"/"sì"/"yes" come pass; tutto il resto = fail (default deny)
            return token == "si" || token == "sì" || token == "yes";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Self-check LLM call failed; treating as pass to avoid blocking legitimate replies.");
            // policy: se il self-check è rotto (timeout/network), NON bloccare la risposta dell'utente
            return true;
        }
    }

    public static string OutOfScopeMessage(string language)
        => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? "I can only help with family caregiving topics. Try reformulating your request around caregiving for a relative."
            : "Posso aiutarti solo su temi legati al caregiving familiare. Prova a riformulare la richiesta concentrandoti sull'assistenza a un familiare.";
}
