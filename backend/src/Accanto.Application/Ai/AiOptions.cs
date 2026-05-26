namespace Accanto.Application.Ai;

/// <summary>
/// Configurazione del modulo AI. Tutte le funzioni AI sono opt-in:
/// Provider="none" disabilita le funzioni a livello di sistema (HTTP 503 ai_not_configured).
/// </summary>
public class AiOptions
{
    /// <summary>"none" (default) | "ollama". In Fase 1 solo "none" / placeholder.</summary>
    public string Provider { get; set; } = "none";

    /// <summary>Endpoint del provider (es. http://ollama:11434).</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Modello (es. llama3.2:3b).</summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>Timeout chiamata in secondi.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Limite token in output (best-effort, dipende dal provider).</summary>
    public int MaxOutputTokens { get; set; } = 512;

    public bool IsConfigured => !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(Provider);
}
