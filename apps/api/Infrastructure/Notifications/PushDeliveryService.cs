using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class PushDeliveryService(
    AppDbContext db,
    IPushNotificationSender pushSender,
    ILogger<PushDeliveryService> logger)
{
    private const string DevStubTokenPrefix = "dev-stub-token-";

    public async Task TrySendAsync(InAppNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendCoreAsync(notification, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Push delivery failed for notification {NotificationId} user {UserId} eventType {EventType}",
                notification.Id,
                notification.UserId,
                notification.EventType);
        }
    }

    private async Task SendCoreAsync(InAppNotification notification, CancellationToken cancellationToken)
    {
        if (!PushNotificationChannelMapper.IsKnownEventType(notification.EventType))
        {
            logger.LogInformation(
                "Push skipped for unknown eventType {EventType} notification {NotificationId}",
                notification.EventType,
                notification.Id);
            return;
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == notification.UserId && u.OrganisationId == notification.OrganisationId,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Push skipped for inactive or missing user {UserId} notification {NotificationId}",
                notification.UserId,
                notification.Id);
            return;
        }

        var preferences = NotificationPreferencesDefaults.ForRole(user.Role);
        if (!preferences.PushEnabled)
        {
            logger.LogInformation(
                "Push skipped because pushEnabled=false for user {UserId} role {Role} eventType {EventType}",
                notification.UserId,
                user.Role,
                notification.EventType);
            return;
        }

        if (!PushNotificationChannelMapper.IsChannelEnabled(preferences.Channels, notification.EventType))
        {
            logger.LogInformation(
                "Push skipped because channel disabled for user {UserId} eventType {EventType}",
                notification.UserId,
                notification.EventType);
            return;
        }

        var devices = await db.UserDevices
            .Where(d => d.OrganisationId == notification.OrganisationId && d.UserId == notification.UserId)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            logger.LogInformation(
                "Push skipped because no registered devices for user {UserId} eventType {EventType}",
                notification.UserId,
                notification.EventType);
            return;
        }

        var data = BuildDataPayload(notification);

        foreach (var device in devices)
        {
            if (device.PushToken.StartsWith(DevStubTokenPrefix, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Push skipped for dev stub token on device {DeviceInstallId}",
                    device.DeviceInstallId);
                continue;
            }

            var request = new PushSendRequest(
                new PushDeviceTarget(device.PushToken, device.Platform, device.DeviceInstallId),
                notification.Title,
                notification.Body,
                data);

            var result = await pushSender.SendAsync(request, cancellationToken);
            if (result.Success)
            {
                continue;
            }

            logger.LogWarning(
                "Push send failed for user {UserId} eventType {EventType} device {DeviceInstallId}: {ErrorCode} {ErrorMessage}",
                notification.UserId,
                notification.EventType,
                device.DeviceInstallId,
                result.ErrorCode,
                result.ErrorMessage);

            if (result.IsStaleToken)
            {
                await RemoveStaleDeviceAsync(device, cancellationToken);
            }
        }
    }

    private async Task RemoveStaleDeviceAsync(UserDevice device, CancellationToken cancellationToken)
    {
        var tracked = await db.UserDevices.SingleOrDefaultAsync(
            d => d.Id == device.Id,
            cancellationToken);

        if (tracked is null)
        {
            return;
        }

        db.UserDevices.Remove(tracked);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Removed stale push device {DeviceInstallId} for user {UserId}",
            device.DeviceInstallId,
            device.UserId);
    }

    internal static Dictionary<string, string> BuildDataPayload(InAppNotification notification)
    {
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString("D"),
            ["eventType"] = notification.EventType,
            ["title"] = notification.Title,
            ["body"] = notification.Body,
        };

        if (notification.CaseId != Guid.Empty)
        {
            data["caseId"] = notification.CaseId.ToString("D");
        }

        if (!string.IsNullOrWhiteSpace(notification.ResourceType))
        {
            data["resourceType"] = notification.ResourceType;
        }

        if (notification.ResourceId != Guid.Empty)
        {
            data["resourceId"] = notification.ResourceId.ToString("D");
        }

        return data;
    }
}
