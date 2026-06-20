using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Infrastructure.Email;

public static class EmailNotificationChannelMapper
{
    public static bool IsKnownEventType(string eventType) =>
        eventType switch
        {
            NotificationEventTypes.CourtReminder24h => true,
            NotificationEventTypes.CourtMissEscalated => true,
            NotificationEventTypes.TravelClaimSubmitted => true,
            NotificationEventTypes.TravelClaimApproved => true,
            NotificationEventTypes.TravelClaimReturned => true,
            NotificationEventTypes.CaseTransferred => true,
            NotificationEventTypes.ReportExportReady => true,
            _ => false,
        };

    public static bool IsChannelEnabled(NotificationPreferenceChannelsDto channels, string eventType) =>
        eventType switch
        {
            NotificationEventTypes.CourtReminder24h => channels.Court,
            NotificationEventTypes.CourtMissEscalated => channels.Court,
            NotificationEventTypes.TravelClaimSubmitted => channels.Claims,
            NotificationEventTypes.TravelClaimApproved => channels.Claims,
            NotificationEventTypes.TravelClaimReturned => channels.Claims,
            NotificationEventTypes.CaseTransferred => channels.Assignments,
            NotificationEventTypes.ReportExportReady => channels.Reports,
            _ => false,
        };
}
