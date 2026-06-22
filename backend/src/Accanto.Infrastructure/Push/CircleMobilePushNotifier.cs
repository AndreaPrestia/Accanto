using Accanto.Application.Common.Persistence;
using Accanto.Application.Push;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accanto.Infrastructure.Push;

/// <summary>
/// Implementazione di <see cref="ICircleMobilePushNotifier"/>: mirrora
/// <see cref="Accanto.Infrastructure.Email.CircleEmailNotifier"/> ma
/// sfrutta il flag <c>PushEnabled</c> delle preferences e l'Expo client.
///
/// Singleton: usa <see cref="IServiceScopeFactory"/> per aprire scope DB
/// per ogni chiamata (cfr. user memory <c>aspnetcore-gotchas.md</c>:
/// scoped DbContext + fire-and-forget non vanno d'accordo).
/// </summary>
public class CircleMobilePushNotifier : ICircleMobilePushNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExpoPushClient _expo;
    private readonly ILogger<CircleMobilePushNotifier> _logger;

    public CircleMobilePushNotifier(
        IServiceScopeFactory scopeFactory,
        IExpoPushClient expo,
        ILogger<CircleMobilePushNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _expo = expo;
        _logger = logger;
    }

    public async Task NotifyCircleAsync(
        Guid careCircleId,
        Guid excludeUserId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IDevicePushTokenService>();

            var memberIds = await db.CareCircleMembers
                .Where(m => m.CareCircleId == careCircleId && m.UserId != excludeUserId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
            if (memberIds.Count == 0) return;

            await SendToUsersAsync(db, tokenService, memberIds, topic, title, body, data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio push notifica topic {Topic} cerchio {Circle}", topic, careCircleId);
        }
    }

    public async Task NotifyUserAsync(
        Guid userId,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IDevicePushTokenService>();

            await SendToUsersAsync(db, tokenService, new[] { userId }, topic, title, body, data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio push a utente {User} topic {Topic}", userId, topic);
        }
    }

    private async Task SendToUsersAsync(
        IAccantoDbContext db,
        IDevicePushTokenService tokenService,
        IReadOnlyList<Guid> userIds,
        NotificationTopic topic,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct)
    {
        // Preferences: assenza riga = default-enabled. Stesso pattern di
        // CircleEmailNotifier per coerenza UX (l'utente che non ha mai
        // toccato le impostazioni riceve tutto).
        var prefs = await db.UserNotificationPreferences
            .Where(p => userIds.Contains(p.UserId) && p.Topic == topic)
            .ToDictionaryAsync(p => p.UserId, p => p.PushEnabled, ct);

        var optedInUserIds = userIds
            .Where(uid => !prefs.TryGetValue(uid, out var enabled) || enabled)
            .ToList();
        if (optedInUserIds.Count == 0) return;

        var tokens = await db.DevicePushTokens
            .Where(t => optedInUserIds.Contains(t.UserId))
            .Select(t => t.Token)
            .ToListAsync(ct);
        if (tokens.Count == 0) return;

        var message = new ExpoPushMessage(title, body, data, topic);
        var invalid = await _expo.SendAsync(tokens, message, ct);
        if (invalid.Count > 0)
        {
            await tokenService.RemoveInvalidTokensAsync(invalid, ct);
        }
    }
}
