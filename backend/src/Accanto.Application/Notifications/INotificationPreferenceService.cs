namespace Accanto.Application.Notifications;

public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationPreferenceDto>> UpdateAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default);
}
