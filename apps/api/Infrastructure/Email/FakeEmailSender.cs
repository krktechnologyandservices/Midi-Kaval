namespace MidiKaval.Api.Infrastructure.Email;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly object _lock = new();
    private readonly List<EmailMessage> _messages = [];

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    public EmailMessage? LastMessage
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count == 0 ? null : _messages[^1];
            }
        }
    }

    public bool FailNextSend { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            throw new EmailDeliveryException("Simulated SMTP failure.");
        }

        lock (_lock)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}
