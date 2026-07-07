using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MidiKaval.Api.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = string.Empty;
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new EmailDeliveryException("SMTP host is not configured.");
        }

        // Deliberately fail fast rather than fall back to a made-up domain: a From address
        // that doesn't match the authenticated mailbox (e.g. a fake domain with no SPF/DKIM)
        // is a common cause of mail landing in spam or being silently dropped downstream —
        // the send itself succeeds against the relay, so no exception would otherwise surface.
        if (string.IsNullOrWhiteSpace(_options.From))
        {
            throw new EmailDeliveryException("SMTP From address is not configured.");
        }

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(_options.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
