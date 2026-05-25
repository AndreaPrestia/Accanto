using Accanto.Domain.Enums;

namespace Accanto.Application.Notifications;

public sealed record NotificationPreferenceDto(NotificationTopic Topic, bool EmailEnabled);

public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceDto> Preferences);
