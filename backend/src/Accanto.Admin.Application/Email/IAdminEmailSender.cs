namespace Accanto.Admin.Application.Email;

/// <summary>
/// Invio email del control plane admin. Self-contained: non riusa
/// l'IEmailService pubblico. Implementazioni "safe" non propagano eccezioni.
/// </summary>
public interface IAdminEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(
        string recipientEmail,
        string? recipientDisplayName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
