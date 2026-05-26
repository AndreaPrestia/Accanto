namespace Accanto.Application.Ai;

/// <summary>
/// Astrazione del provider AI. Stateless. Tutte le implementazioni devono:
/// - rispettare il timeout configurato in <see cref="AiOptions"/>
/// - non loggare in chiaro il prompt completo (può contenere dati personali)
/// - restituire sempre un <see cref="AiResponse"/> con disclaimer non vuoto
/// </summary>
public interface IAiAssistant
{
    Task<AiResponse> SummarizeTimelineAsync(string prompt, string language, CancellationToken cancellationToken = default);

    Task<AiResponse> DraftDoctorQuestionAsync(string prompt, string language, CancellationToken cancellationToken = default);

    Task<AiResponse> RephraseSharedUpdateAsync(string prompt, string language, CancellationToken cancellationToken = default);

    Task<AiResponse> ReflectCheckInAsync(string prompt, string language, CancellationToken cancellationToken = default);
}
