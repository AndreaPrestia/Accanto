using Accanto.Application.Push;
using Accanto.Domain.Enums;

namespace Accanto.Tests;

/// <summary>
/// Spy in-memory di <see cref="ICircleMobilePushNotifier"/> per i test
/// dei service di dominio. Mantiene la stessa firma di <see cref="NoOpCircleEmailNotifier"/>.
/// </summary>
public class NoOpCircleMobilePushNotifier : ICircleMobilePushNotifier
{
    public List<(Guid CircleId, Guid ExcludeUserId, NotificationTopic Topic, string Title, string Body)> CircleNotifications { get; } = new();
    public List<(Guid UserId, NotificationTopic Topic, string Title, string Body)> UserNotifications { get; } = new();

    public Task NotifyCircleAsync(
        Guid careCircleId,
        Guid excludeUserId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        CircleNotifications.Add((careCircleId, excludeUserId, topic, title, body));
        return Task.CompletedTask;
    }

    public Task NotifyUserAsync(
        Guid userId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        UserNotifications.Add((userId, topic, title, body));
        return Task.CompletedTask;
    }

    public List<(Guid UserId, string Title, string Body)> TestNotifications { get; } = new();

    public Task SendTestAsync(
        Guid userId,
        string title,
        string body,
        CancellationToken cancellationToken = default)
    {
        TestNotifications.Add((userId, title, body));
        return Task.CompletedTask;
    }
}
