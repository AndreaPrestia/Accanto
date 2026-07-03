using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Notifications;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private static readonly NotificationTopic[] AllTopics =
        Enum.GetValues<NotificationTopic>();

    private readonly IAccantoDbContext _db;

    public NotificationPreferenceService(IAccantoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var saved = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        var byTopic = saved.ToDictionary(p => p.Topic);
        return AllTopics
            .Select(t =>
            {
                if (byTopic.TryGetValue(t, out var pref))
                {
                    return new NotificationPreferenceDto(t, pref.EmailEnabled, pref.PushEnabled);
                }
                // Default-enabled per topic e canale: chi non ha mai toccato
                // le impostazioni riceve sia email sia push.
                return new NotificationPreferenceDto(t, true, true);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> UpdateAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        // Last-write-wins se il client manda lo stesso topic due volte
        // nella stessa request (difensivo).
        var incoming = request.Preferences.GroupBy(p => p.Topic).ToDictionary(g => g.Key, g => g.Last());
        var saved = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        var byTopic = saved.ToDictionary(p => p.Topic);

        var now = DateTimeOffset.UtcNow;
        foreach (var topic in AllTopics)
        {
            if (!incoming.TryGetValue(topic, out var dto)) continue;
            if (byTopic.TryGetValue(topic, out var existing))
            {
                var changed = false;
                if (existing.EmailEnabled != dto.EmailEnabled)
                {
                    existing.EmailEnabled = dto.EmailEnabled;
                    changed = true;
                }
                // PushEnabled è opzionale nelle richieste per backward
                // compat col client web: applichiamo solo se presente.
                if (dto.PushEnabled is { } pushWanted && existing.PushEnabled != pushWanted)
                {
                    existing.PushEnabled = pushWanted;
                    changed = true;
                }
                if (changed) existing.UpdatedAt = now;
            }
            else
            {
                _db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Topic = topic,
                    EmailEnabled = dto.EmailEnabled,
                    PushEnabled = dto.PushEnabled ?? true,
                    UpdatedAt = now
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }
}
