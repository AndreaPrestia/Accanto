using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class UserNotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationTopic Topic { get; set; }
    public bool EmailEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
