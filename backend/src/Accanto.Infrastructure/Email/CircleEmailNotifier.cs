using Accanto.Application.Common.Persistence;
using Accanto.Application.Email;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accanto.Infrastructure.Email;

public class CircleEmailNotifier : ICircleEmailNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailService _email;
    private readonly ILogger<CircleEmailNotifier> _logger;

    public CircleEmailNotifier(IServiceScopeFactory scopeFactory, IEmailService email, ILogger<CircleEmailNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _email = email;
        _logger = logger;
    }

    public async Task NotifyCircleAsync(Guid careCircleId, Guid excludeUserId, NotificationTopic topic, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();

            var memberIds = await db.CareCircleMembers
                .Where(m => m.CareCircleId == careCircleId && m.UserId != excludeUserId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
            if (memberIds.Count == 0) return;

            await SendToUsersAsync(db, memberIds, topic, subject, htmlBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio email notifica topic {Topic} cerchio {Circle}", topic, careCircleId);
        }
    }

    public async Task NotifyUserAsync(Guid userId, NotificationTopic topic, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            await SendToUsersAsync(db, new[] { userId }, topic, subject, htmlBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio email a utente {User} topic {Topic}", userId, topic);
        }
    }

    public async Task SendSecurityEmailAsync(Guid userId, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAccantoDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null) return;
            await _email.SendAsync(user.Email, user.DisplayName, subject, htmlBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Errore invio email sicurezza a utente {User}", userId);
        }
    }

    private async Task SendToUsersAsync(IAccantoDbContext db, IReadOnlyList<Guid> userIds, NotificationTopic topic, string subject, string htmlBody, CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(ct);
        if (users.Count == 0) return;

        var prefs = await db.UserNotificationPreferences
            .Where(p => userIds.Contains(p.UserId) && p.Topic == topic)
            .ToDictionaryAsync(p => p.UserId, p => p.EmailEnabled, ct);

        foreach (var u in users)
        {
            var enabled = !prefs.TryGetValue(u.Id, out var v) || v; // default: abilitato
            if (!enabled) continue;
            await _email.SendAsync(u.Email, u.DisplayName, subject, htmlBody, ct);
        }
    }
}
