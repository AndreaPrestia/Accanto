using Accanto.Application.Push;

namespace Accanto.Tests;

public class NoOpPushService : IPushService
{
    public List<(IEnumerable<Guid> Users, PushNotificationPayload Payload)> Calls { get; } = new();
    public string? GetVapidPublicKey() => null;
    public Task SubscribeAsync(Guid userId, PushSubscriptionRequest request, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default) => Task.CompletedTask;
    public Task NotifyUsersAsync(IEnumerable<Guid> userIds, PushNotificationPayload payload, CancellationToken ct = default)
    {
        Calls.Add((userIds.ToList(), payload));
        return Task.CompletedTask;
    }
    public Task NotifyCircleAsync(Guid careCircleId, Guid excludeUserId, PushNotificationPayload payload, CancellationToken ct = default)
    {
        Calls.Add((new[] { careCircleId }, payload));
        return Task.CompletedTask;
    }
}
