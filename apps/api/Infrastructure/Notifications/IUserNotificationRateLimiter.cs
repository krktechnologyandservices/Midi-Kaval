namespace MidiKaval.Api.Infrastructure.Notifications;

public interface IUserNotificationRateLimiter
{
    Task<bool> CanSendAsync(Guid userId, string notificationType, CancellationToken ct = default);
    Task RecordSendAsync(Guid userId, string notificationType, CancellationToken ct = default);
}
