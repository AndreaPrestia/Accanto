namespace Accanto.Domain.Enums;

/// <summary>
/// Verdetto del pipeline guardrail su una chiamata AI.
/// Determina se la risposta è stata effettivamente elaborata o sostituita
/// da un messaggio canonico.
/// </summary>
public enum AiGuardrailVerdict
{
    /// <summary>Input + output validati con successo, risposta del modello restituita.</summary>
    Passed = 0,

    /// <summary>Input bloccato dal layer A (regex injection o blocklist topic).</summary>
    BlockedInput = 1,

    /// <summary>Modello ha risposto con la sentinella "fuori_scopo".</summary>
    OutOfScope = 2,

    /// <summary>Self-check LLM ha valutato la risposta come non pertinente o non sicura.</summary>
    SelfCheckFailed = 3,

    /// <summary>Input rilevato come segnale di autolesionismo, restituita risposta di supporto.</summary>
    SafetyRedirect = 4
}
