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
        var byTopic = saved.ToDictionary(p => p.Topic, p => p.EmailEnabled);
        return AllTopics
            .Select(t => new NotificationPreferenceDto(t, byTopic.TryGetValue(t, out var v) ? v : true))
            .ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> UpdateAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var incoming = request.Preferences.GroupBy(p => p.Topic).ToDictionary(g => g.Key, g => g.Last().EmailEnabled);
        var saved = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        var byTopic = saved.ToDictionary(p => p.Topic);

        var now = DateTimeOffset.UtcNow;
        foreach (var topic in AllTopics)
        {
            if (!incoming.TryGetValue(topic, out var enabled)) continue;
            if (byTopic.TryGetValue(topic, out var existing))
            {
                if (existing.EmailEnabled != enabled)
                {
                    existing.EmailEnabled = enabled;
                    existing.UpdatedAt = now;
                }
            }
            else
            {
                _db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Topic = topic,
                    EmailEnabled = enabled,
                    UpdatedAt = now
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }
}
