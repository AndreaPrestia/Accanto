using Accanto.Domain.Enums;

namespace Accanto.Application.Notifications;

/// <summary>
/// Stato delle preferenze di notifica per un topic.
/// <para>
/// <see cref="PushEnabled"/> è nullable nelle richieste di update per
/// backward compatibility: il client web esistente invia solo
/// <c>EmailEnabled</c> e non vogliamo che PushEnabled venga
/// silenziosamente resettato a <c>false</c>.
/// </para>
/// </summary>
public sealed record NotificationPreferenceDto(
    NotificationTopic Topic,
    bool EmailEnabled,
    bool? PushEnabled = null);

public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceDto> Preferences);
