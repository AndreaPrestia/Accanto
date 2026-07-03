using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class UserNotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationTopic Topic { get; set; }
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Abilita le notifiche push mobile per questo topic. Default true
    /// (storicamente le righe esistenti non hanno questa colonna: la
    /// migration la inizializza a <c>true</c> per preservare il comportamento).
    /// </summary>
    public bool PushEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
