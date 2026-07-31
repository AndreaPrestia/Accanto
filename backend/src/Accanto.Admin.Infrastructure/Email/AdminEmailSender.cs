using Accanto.Admin.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Accanto.Admin.Infrastructure.Email;

/// <summary>
/// Sender SMTP (MailKit) per l'Admin API. Copia self-contained del sender
/// pubblico: nessun riferimento ad Accanto.Infrastructure. Se non configurato
/// (SmtpHost/FromAddress vuoti) e' un no-op.
/// </summary>
public class AdminEmailSender : IAdminEmailSender
{
    private readonly AdminEmailOptions _options;
    private readonly ILogger<AdminEmailSender> _logger;

    public AdminEmailSender(IOptions<AdminEmailOptions> options, ILogger<AdminEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SmtpHost)
        && !string.IsNullOrWhiteSpace(_options.FromAddress);

    public async Task SendAsync(string recipientEmail, string? recipientDisplayName, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            // Non logghiamo l'indirizzo (PII): solo il fatto che il sender e' off.
            _logger.LogDebug("AdminEmailSender non configurato: email admin ignorata.");
            return;
        }
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogDebug("Email admin senza destinatario, ignorata.");
            return;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            msg.To.Add(new MailboxAddress(recipientDisplayName ?? string.Empty, recipientEmail));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var secure = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_options.SmtpHost!, _options.SmtpPort, secure, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(msg, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            // Non logghiamo l'indirizzo del destinatario (PII).
            _logger.LogWarning(ex, "Errore invio email admin.");
        }
    }
}
