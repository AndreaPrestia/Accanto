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

    // ---------------- Guardrails ----------------

    /// <summary>Lunghezza massima del campo Topic.</summary>
    public int MaxTopicLength { get; set; } = 300;

    /// <summary>Lunghezza massima del campo Notes (DoctorQuestion).</summary>
    public int MaxNotesLength { get; set; } = 1000;

    /// <summary>Lunghezza massima del testo da riformulare.</summary>
    public int MaxRephraseTextLength { get; set; } = 4000;

    /// <summary>Lunghezza massima della stringa "tone".</summary>
    public int MaxRephraseToneLength { get; set; } = 80;

    /// <summary>Cap finale sul testo della risposta (post-self-check). Taglio + ellipsis.</summary>
    public int MaxOutputChars { get; set; } = 2000;

    /// <summary>Abilita il secondo passaggio LLM di verifica on-topic + safety.</summary>
    public bool SelfCheckEnabled { get; set; } = true;

    /// <summary>TTL (in minuti) della cache idempotency. 0 = disabilitata.</summary>
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>Pattern regex (case-insensitive) considerati tentativi di prompt-injection.</summary>
    public List<string> InjectionPatterns { get; set; } = new()
    {
        @"ignora\s+(le\s+)?istruzioni",
        @"ignore\s+(the\s+)?(previous\s+|prior\s+)?instructions",
        @"system\s*:",
        @"you\s+are\s+now",
        @"act\s+as",
        @"\bDAN\b",
        @"rispondi\s+come\s+se",
        @"```",
        @"traduci\s+(in|a)\s+\w+",
        @"translate\s+to\s+\w+",
        @"scrivi\s+(il\s+)?codice",
        @"write\s+(some\s+)?code",
        @"jailbreak"
    };

    /// <summary>Pattern (regex, case-insensitive) che marcano argomenti fuori dallo scopo della piattaforma.</summary>
    public List<string> OffTopicPatterns { get; set; } = new()
    {
        @"\bpolitic[ao]\b",
        @"\b(borsa|trading|crypto|bitcoin)\b",
        @"\b(sex|porno|hard)\b",
        @"\b(bomba|attentato|esplosiv)\w*",
        @"\b(programmazione|programming|python|javascript|c\+\+)\b"
    };

    /// <summary>Pattern (regex, case-insensitive) che indicano possibile autolesionismo: triggerano risposta di supporto, non blocco.</summary>
    public List<string> SelfHarmPatterns { get; set; } = new()
    {
        @"\bsuicid\w*",
        @"\bfarmi\s+del\s+male\b",
        @"\bnon\s+voglio\s+(più\s+)?vivere\b",
        @"\bkill\s+myself\b",
        @"\bend\s+(my\s+)?life\b"
    };

    /// <summary>Numeri di supporto restituiti nel messaggio di safety-redirect.</summary>
    public List<SupportContact> SupportContacts { get; set; } = new()
    {
        new SupportContact { Label = "Telefono Amico",   Number = "02 2327 2327" },
        new SupportContact { Label = "Emergenza",        Number = "112" },
        new SupportContact { Label = "Telefono Azzurro (minori)", Number = "19696" }
    };

    public bool IsConfigured => !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(Provider);
}

public class SupportContact
{
    public string Label { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
}
