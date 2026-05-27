namespace Accanto.Application.Ai;

/// <summary>
/// Risposta standard delle funzioni AI. Contiene sempre un disclaimer
/// non rimovibile che ricorda all'utente che il testo è generato da un modello
/// e non sostituisce un parere professionale.
/// </summary>
public record AiResponse(
    string Text,
    string Model,
    long TookMs,
    string Disclaimer,
    Guid InteractionId = default,
    string Verdict = "passed",
    bool CacheHit = false
);

/// <summary>Richiesta per generare un riassunto della timeline di un cerchio.</summary>
public record TimelineSummaryRequest(int Days = 7);

/// <summary>Richiesta per generare una bozza di domanda al medico.</summary>
public record DoctorQuestionDraftRequest(string Topic, string? Notes = null);

/// <summary>Richiesta per riformulare un testo destinato a un familiare.</summary>
public record RephraseRequest(string Text, string? Tone = null);

/// <summary>Richiesta per ottenere una riflessione personale sui propri check-in.</summary>
public record CheckInReflectionRequest(int Days = 14);

/// <summary>Stato del modulo AI lato server.</summary>
public record AiStatusResponse(bool Available, string Provider, string Model);

// ------------------------ Cronologia ------------------------

/// <summary>Riepilogo di una interazione AI (lista cronologia).</summary>
public record AiInteractionSummary(
    Guid Id,
    Guid UserId,
    Guid? CareCircleId,
    string Function,
    string Verdict,
    string? Feedback,
    string Model,
    string Language,
    int TookMs,
    DateTimeOffset CreatedAt
);

/// <summary>Dettaglio completo di una interazione AI (input + output in chiaro).</summary>
public record AiInteractionDetail(
    Guid Id,
    Guid UserId,
    Guid? CareCircleId,
    string Function,
    string Verdict,
    string? Feedback,
    string Model,
    string PromptVersion,
    string Language,
    int TookMs,
    bool CacheHit,
    DateTimeOffset CreatedAt,
    string Input,
    string Output
);

public record AiInteractionListResponse(
    IReadOnlyList<AiInteractionSummary> Items,
    int Page,
    int PageSize,
    int Total
);

public record SubmitAiFeedbackRequest(string Value);
