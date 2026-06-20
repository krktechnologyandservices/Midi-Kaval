namespace MidiKaval.Api.Infrastructure.Notifications;

public static class NotificationEventTypes
{
    public const string InterventionOverdue = "intervention.overdue";
    public const string CourtReminder24h = "court.reminder.24h";
    public const string CourtMissEscalated = "court.miss.escalated";
    public const string TravelClaimApproved = "travel.claim.approved";
    public const string TravelClaimReturned = "travel.claim.returned";
    public const string TravelClaimSubmitted = "travel.claim.submitted";
    public const string CaseTransferred = "case.transferred";
    public const string ReportExportReady = "report.export.ready";
}
