using System.Text.Json;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Push;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;
using DomainPushSubscription = Accanto.Domain.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace Accanto.Infrastructure.Push;

public class PushService : IPushService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PushOptions _options;
    private readonly ILogger<PushService> _logger;
    private readonly WebPushClient? _client;
    private readonly VapidDetails? _vapid;

    public PushService(IServiceScopeFactory scopeFactory, IOptions<PushOptions> options, ILogger<PushService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_options.VapidPublicKey) && !string.IsNullOrWhiteSpace(_options.VapidPrivateKey))
        {
            _client = new WebPushClient();
            _vapid = new VapidDetails(_options.VapidSubject, _options.VapidPublicKey, _options.VapidPrivateKey);
        }
    }

    public string? GetVapidPublicKey() => string.IsNullOrWhiteSpace(_options.VapidPublicKey) ? null : _options.VapidPublicKey;

    public async Task SubscribeAsync(Guid userId, PushSubscriptionRequest request, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();

        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == request.Endpoint, ct);
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            db.PushSubscriptions.Add(new DomainPushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = Truncate(request.UserAgent, 500),
                CreatedAt = now,
                LastUsedAt = now
            });
        }
        else
        {
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserAgent = Truncate(request.UserAgent, 500);
            existing.LastUsedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
        var rows = await db.PushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == endpoint)
            .ToListAsync(ct);
        foreach (var r in rows) db.PushSubscriptions.Remove(r);
        if (rows.Count > 0) await db.SaveChangesAsync(ct);
    }

    public async Task NotifyUsersAsync(IEnumerable<Guid> userIds, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (_client is null || _vapid is null) return;
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
        var subs = await db.PushSubscriptions.Where(s => ids.Contains(s.UserId)).ToListAsync(ct);
        if (subs.Count == 0) return;

        var json = JsonSerializer.Serialize(new { title = payload.Title, body = payload.Body, url = payload.Url });
        var gone = new List<DomainPushSubscription>();
        foreach (var s in subs)
        {
            var webPushSub = new WebPushSubscription(s.Endpoint, s.P256dh, s.Auth);
            try
            {
                await _client.SendNotificationAsync(webPushSub, json, _vapid, ct);
                s.LastUsedAt = DateTimeOffset.UtcNow;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                gone.Add(s);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore invio push a {Endpoint}", s.Endpoint);
            }
        }
        foreach (var g in gone) db.PushSubscriptions.Remove(g);
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyCircleAsync(Guid careCircleId, Guid excludeUserId, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (_client is null || _vapid is null) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
        var memberIds = await db.CareCircleMembers
            .Where(m => m.CareCircleId == careCircleId && m.UserId != excludeUserId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        if (memberIds.Count == 0) return;
        await NotifyUsersAsync(memberIds, payload, ct);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
