using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Email;

public sealed class BrevoOptions
{
    public const string SectionName = "Email:Brevo";

    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Midi Kaval";
}

// Sends via Brevo's HTTPS transactional email API instead of raw SMTP — some hosts
// throttle or block outbound SMTP ports (25/587) on free-tier plans, but a plain HTTPS
// POST is never restricted the same way. Swap back to SmtpEmailSender via the
// Email:Provider config switch once on a plan where SMTP is reliable.
public sealed class BrevoEmailSender(IHttpClientFactory httpClientFactory, IOptions<BrevoOptions> options) : IEmailSender
{
    private const string SendEndpoint = "https://api.brevo.com/v3/smtp/email";
    private readonly BrevoOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new EmailDeliveryException("Brevo API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new EmailDeliveryException("Brevo sender email is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint);
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Add("accept", "application/json");
        request.Content = JsonContent.Create(new
        {
            sender = new { email = _options.SenderEmail, name = _options.SenderName },
            to = new[] { new { email = message.To } },
            subject = message.Subject,
            textContent = message.Body,
        });

        var client = httpClientFactory.CreateClient(nameof(BrevoEmailSender));
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EmailDeliveryException($"Brevo API returned {(int)response.StatusCode}: {body}");
        }
    }
}
