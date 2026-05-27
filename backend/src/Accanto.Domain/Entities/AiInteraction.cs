using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

/// <summary>
/// Interazione AI persistita. Contiene input e output cifrati at-rest tramite
/// <see cref="Application.Common.Security.IFieldProtector"/> (AES-256-GCM).
/// L'autore (<see cref="UserId"/>) può sempre leggere; l'Owner del cerchio
/// (<see cref="CareCircleId"/>) può leggere tutte le interazioni del cerchio,
/// tranne le riflessioni personali sui check-in (<see cref="CareCircleId"/> = null).
/// </summary>
public class AiInteraction
{
    public Guid Id { get; set; }

    /// <summary>Autore della richiesta.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Cerchio collegato (null per check-in-reflection, sempre personale).
    /// Quando valorizzato l'Owner del cerchio può accedere all'interazione.
    /// </summary>
    public Guid? CareCircleId { get; set; }

    public AiInteractionFunction Function { get; set; }

    /// <summary>JSON del DTO di richiesta dopo redazione PII, cifrato AES-GCM.</summary>
    public string InputJsonEncrypted { get; set; } = string.Empty;

    /// <summary>Testo della risposta finale (post-guardrail) cifrato AES-GCM.</summary>
    public string OutputEncrypted { get; set; } = string.Empty;

    /// <summary>Modello effettivamente usato (es. "llama3.2:3b" o "null").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Versione del prompt builder, per audit storico.</summary>
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>Latenza totale della pipeline (incl. self-check).</summary>
    public int TookMs { get; set; }

    public AiGuardrailVerdict Verdict { get; set; }

    /// <summary>Lingua restituita all'utente (it/en).</summary>
    public string Language { get; set; } = "it";

    /// <summary>Hit/miss della cache idempotency. Hit non viene mai salvata: campo per analisi del miss.</summary>
    public bool CacheHit { get; set; }

    public AiFeedback? Feedback { get; set; }

    public DateTimeOffset? FeedbackAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
