namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed record PushDeviceTarget(string PushToken, string Platform, string DeviceInstallId);

public sealed record PushSendRequest(
    PushDeviceTarget Device,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

public sealed record PushSendResult(bool Success, bool IsStaleToken, string? ErrorCode, string? ErrorMessage);

public interface IPushNotificationSender
{
    Task<PushSendResult> SendAsync(PushSendRequest request, CancellationToken cancellationToken = default);
}
