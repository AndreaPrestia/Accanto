using Accanto.Domain.Enums;

namespace Accanto.Application.Push;

/// <summary>
/// Notifier per push notification verso i device mobili (Expo).
/// È il gemello "push" di <see cref="Accanto.Application.Email.ICircleEmailNotifier"/>:
/// stessi triggers (timeline, shared update, doctor question, invite),
/// stesse preferences (<see cref="Accanto.Domain.Entities.UserNotificationPreference.PushEnabled"/>),
/// ma payload diverso (title + body brevi, no HTML).
///
/// Tutte le chiamate sono fire-and-forget: i servizi che le invocano non
/// devono bloccarsi su errori di rete verso Expo. L'implementazione
/// logga gli errori e prosegue.
/// </summary>
public interface ICircleMobilePushNotifier
{
    /// <summary>
    /// Invia il messaggio a tutti i membri del cerchio tranne
    /// <paramref name="excludeUserId"/> (di solito l'autore dell'azione).
    /// </summary>
    Task NotifyCircleAsync(
        Guid careCircleId,
        Guid excludeUserId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invia un messaggio a un singolo utente (tutti i suoi device).</summary>
    Task NotifyUserAsync(
        Guid userId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
