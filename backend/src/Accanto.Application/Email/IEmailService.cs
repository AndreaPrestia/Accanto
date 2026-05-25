namespace Accanto.Application.Email;

/// <summary>
/// Invio di email. Implementazioni "fire-and-forget" non devono propagare eccezioni.
/// </summary>
public interface IEmailService
{
    bool IsConfigured { get; }

    Task SendAsync(
        string recipientEmail,
        string? recipientDisplayName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
