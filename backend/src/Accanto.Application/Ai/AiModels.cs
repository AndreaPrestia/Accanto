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
    string Disclaimer
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
