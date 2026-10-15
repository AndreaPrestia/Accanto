using Accanto.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Accanto.Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
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
            _logger.LogDebug("EmailService non configurato: email a {Recipient} ignorata.", MaskEmail(recipientEmail));
            return;
        }
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogDebug("Email senza destinatario, ignorata.");
            return;
        }

        try
        {
            using var msg = new MimeMessage();
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
            _logger.LogWarning(ex, "Errore invio email a {Recipient}", MaskEmail(recipientEmail));
        }
    }

    /// <summary>
    /// Maschera un indirizzo email per il logging (PII utente finale): tiene la
    /// prima lettera della parte locale e il dominio, oscurando il resto.
    /// Es. "mario.rossi@example.com" -> "m***@example.com".
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(vuota)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[at..]; // include '@'
        var head = local.Length > 0 ? local[0].ToString() : string.Empty;
        return $"{head}***{domain}";
    }
}
