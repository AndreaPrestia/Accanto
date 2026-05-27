namespace Accanto.Application.Ai;

/// <summary>
/// Assistente "null": utilizzato come fallback quando il provider AI non è configurato
/// o nei test. Restituisce sempre una risposta segnaposto deterministica.
/// Il gate 503 (ai_not_configured) viene applicato dal controller PRIMA di chiamare l'assistant,
/// quindi questa implementazione viene invocata solo quando il sistema è configurato
/// (es. nei test con Provider="ollama" ma assistant sostituito da Null).
/// </summary>
public sealed class NullAiAssistant : IAiAssistant
{
    private const string Placeholder = "[risposta non disponibile in questa configurazione]";

    public Task<AiResponse> SummarizeTimelineAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(language));

    public Task<AiResponse> DraftDoctorQuestionAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(language));

    public Task<AiResponse> RephraseSharedUpdateAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(language));

    public Task<AiResponse> ReflectCheckInAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(language));

    public Task<AiResponse> GenerateAsync(string prompt, string language, int? maxTokens = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(language));

    private static AiResponse Build(string language)
    {
        var disclaimer = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? "AI-generated text. Not medical, legal, or financial advice."
            : "Testo generato da AI. Non sostituisce un parere medico o professionale.";
        return new AiResponse(Placeholder, "null", 0, disclaimer);
    }
}
