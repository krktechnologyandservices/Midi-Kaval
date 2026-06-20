using Microsoft.Extensions.Logging;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class FakePushNotificationSender(ILogger<FakePushNotificationSender> logger) : IPushNotificationSender
{
    private readonly object _lock = new();
    private readonly List<PushSendRequest> _messages = [];

    public IReadOnlyList<PushSendRequest> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    public PushSendRequest? LastMessage
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

    public bool FailNextSendAsStaleToken { get; set; }

    public Task<PushSendResult> SendAsync(PushSendRequest request, CancellationToken cancellationToken = default)
    {
        if (FailNextSendAsStaleToken)
        {
            FailNextSendAsStaleToken = false;
            logger.LogInformation(
                "Fake push simulated stale token for device {DeviceInstallId}",
                request.Device.DeviceInstallId);
            return Task.FromResult(new PushSendResult(false, true, "NotRegistered", "Simulated stale token."));
        }

        if (FailNextSend)
        {
            FailNextSend = false;
            logger.LogInformation(
                "Fake push simulated failure for device {DeviceInstallId}",
                request.Device.DeviceInstallId);
            return Task.FromResult(new PushSendResult(false, false, "Unavailable", "Simulated push failure."));
        }

        lock (_lock)
        {
            _messages.Add(request);
        }

        logger.LogInformation(
            "Fake push sent to device {DeviceInstallId} eventType {EventType}",
            request.Device.DeviceInstallId,
            request.Data.GetValueOrDefault("eventType"));

        return Task.FromResult(new PushSendResult(true, false, null, null));
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}
