using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Infrastructure.Notifications;

public static class PushNotificationChannelMapper
{
    public static bool IsKnownEventType(string eventType) =>
        eventType switch
        {
            NotificationEventTypes.InterventionOverdue => true,
            NotificationEventTypes.CourtReminder24h => true,
            NotificationEventTypes.CourtMissEscalated => true,
            NotificationEventTypes.TravelClaimApproved => true,
            NotificationEventTypes.TravelClaimReturned => true,
            _ => false,
        };

    public static bool IsChannelEnabled(NotificationPreferenceChannelsDto channels, string eventType) =>
        eventType switch
        {
            NotificationEventTypes.InterventionOverdue => channels.Interventions,
            NotificationEventTypes.CourtReminder24h => channels.Court,
            NotificationEventTypes.CourtMissEscalated => channels.Court,
            NotificationEventTypes.TravelClaimApproved => channels.Claims,
            NotificationEventTypes.TravelClaimReturned => channels.Claims,
            _ => false,
        };
}
