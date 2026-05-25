using Accanto.Domain.Enums;

namespace Accanto.Application.Email;

/// <summary>
/// Recapita notifiche per un determinato topic ai membri di un cerchio, rispettando
/// le preferenze utente. Implementazioni "best effort": non bloccano l'operazione chiamante.
/// </summary>
public interface ICircleEmailNotifier
{
    Task NotifyCircleAsync(
        Guid careCircleId,
        Guid excludeUserId,
        NotificationTopic topic,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    Task NotifyUserAsync(
        Guid userId,
        NotificationTopic topic,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Email di sicurezza: ignora le preferenze (l'utente deve sempre essere avvisato).
    /// </summary>
    Task SendSecurityEmailAsync(
        Guid userId,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
