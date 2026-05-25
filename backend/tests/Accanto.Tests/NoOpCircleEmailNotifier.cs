using Accanto.Application.Email;
using Accanto.Domain.Enums;

namespace Accanto.Tests;

public class NoOpCircleEmailNotifier : ICircleEmailNotifier
{
    public List<(Guid CircleId, Guid ExcludeUserId, NotificationTopic Topic, string Subject)> CircleNotifications { get; } = new();
    public List<(Guid UserId, NotificationTopic Topic, string Subject)> UserNotifications { get; } = new();
    public List<(Guid UserId, string Subject)> SecurityEmails { get; } = new();

    public Task NotifyCircleAsync(Guid careCircleId, Guid excludeUserId, NotificationTopic topic, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        CircleNotifications.Add((careCircleId, excludeUserId, topic, subject));
        return Task.CompletedTask;
    }

    public Task NotifyUserAsync(Guid userId, NotificationTopic topic, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        UserNotifications.Add((userId, topic, subject));
        return Task.CompletedTask;
    }

    public Task SendSecurityEmailAsync(Guid userId, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        SecurityEmails.Add((userId, subject));
        return Task.CompletedTask;
    }
}
