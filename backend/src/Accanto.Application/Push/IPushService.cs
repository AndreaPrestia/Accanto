namespace Accanto.Application.Push;

public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth, string? UserAgent);

public sealed record PushUnsubscribeRequest(string Endpoint);

public sealed record VapidPublicKeyDto(string PublicKey);

public sealed record PushNotificationPayload(string Title, string Body, string? Url);

public interface IPushService
{
    string? GetVapidPublicKey();
    Task SubscribeAsync(Guid userId, PushSubscriptionRequest request, CancellationToken ct = default);
    Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default);
    Task NotifyUsersAsync(IEnumerable<Guid> userIds, PushNotificationPayload payload, CancellationToken ct = default);
    Task NotifyCircleAsync(Guid careCircleId, Guid excludeUserId, PushNotificationPayload payload, CancellationToken ct = default);
}
