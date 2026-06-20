namespace MidiKaval.Api.Infrastructure.Audit;

public static class AuditEventTypes
{
    public const string LoginSuccess = "auth.login.success";
    public const string LoginFailed = "auth.login.failed";
    public const string OtpFailed = "auth.otp.failed";
    public const string Logout = "auth.logout";
    public const string RefreshSuccess = "auth.refresh.success";
    public const string SessionInvalidated = "auth.session.invalidated";
    public const string PasswordResetRequested = "auth.password_reset.requested";
    public const string PasswordResetCompleted = "auth.password_reset.completed";
    public const string CaseCreated = "case.created";
    public const string CaseStageChanged = "case.stage.changed";
    public const string CaseMerged = "case.merged";
    public const string CaseTransferred = "case.transferred";
    public const string VisitScheduled = "visit.scheduled";
    public const string VisitCompleted = "visit.completed";
    public const string VisitRescheduled = "visit.rescheduled";
    public const string VisitStarted = "visit.started";
    public const string VisitNoteMerged = "visit.note.merged";
    public const string CaseGpsVerified = "case.gps.verified";
    public const string CasePiiRevealed = "case.pii.revealed";
    public const string CaseNoteCreated = "case.note.created";
    public const string InterventionCreated = "case.intervention.created";
    public const string InterventionUpdated = "case.intervention.updated";
    public const string CourtSittingCreated = "court.sitting.created";
    public const string CourtSittingUpdated = "court.sitting.updated";
    public const string CourtSittingReminderSent = "court.sitting.reminder_sent";
    public const string CourtSittingMissEscalated = "court.sitting.miss_escalated";
    public const string TravelClaimCreated = "travel.claim.created";
    public const string TravelClaimUpdated = "travel.claim.updated";
    public const string TravelClaimSubmitted = "travel.claim.submitted";
    public const string TravelClaimApproved = "travel.claim.approved";
    public const string TravelClaimReturned = "travel.claim.returned";
    public const string AttachmentPresignIssued = "attachment.presign.issued";
    public const string AttachmentConfirmed = "attachment.confirmed";
    public const string LegendCreated = "legend.created";
    public const string LegendUpdated = "legend.updated";
    public const string LegendDeactivated = "legend.deactivated";
    public const string LegendReactivated = "legend.reactivated";
    public const string StaffCreated = "staff.created";
    public const string StaffUpdated = "staff.updated";
    public const string StaffDeactivated = "staff.deactivated";
    public const string StaffReactivated = "staff.reactivated";
    public const string StaffForceReset = "staff.force-reset";
}
