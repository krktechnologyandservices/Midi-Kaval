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
    public const string PasswordChanged = "auth.password.changed";
    public const string CaseCreated = "case.created";
    public const string CaseStageChanged = "case.stage.changed";
    public const string CaseMerged = "case.merged";
    public const string CaseTransferred = "case.transferred";
    public const string VisitScheduled = "visit.scheduled";
    public const string VisitCompleted = "visit.completed";
    public const string VisitRescheduled = "visit.rescheduled";
    public const string VisitStarted = "visit.started";
    public const string VisitCancelled = "visit.cancelled";
    public const string VisitPlaceAdded = "visit.place.added";
    public const string VisitPlaceLogged = "visit.place.logged";
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
    public const string MigrationValidationRun = "migration.validation_run";
    public const string CaseImported = "case.imported";
    public const string MigrationImportCompleted = "migration.import_completed";
    public const string Stage2DataCreated = "case.stage2_data.created";
    public const string Stage2DataUpdated = "case.stage2_data.updated";
    public const string Stage3DataCreated = "case.stage3_data.created";
    public const string Stage3DataUpdated = "case.stage3_data.updated";
    public const string Stage4PlacementCreated = "case.stage4_placement.created";
    public const string Stage4PlacementUpdated = "case.stage4_placement.updated";
    public const string Stage5ReintegrationCreated = "case.stage5_reintegration.created";
    public const string Stage5ReintegrationUpdated = "case.stage5_reintegration.updated";
    public const string Stage6TerminationExclusionCreated = "case.stage6_termination_exclusion.created";
    public const string Stage6TerminationExclusionUpdated = "case.stage6_termination_exclusion.updated";
    public const string CaseLinked = "case.related.created";
    public const string CaseUnlinked = "case.related.deleted";
    public const string BudgetCreated = "budget.created";
    public const string BudgetUpdated = "budget.updated";
    public const string BudgetProposed = "budget.proposed";
    public const string BudgetApproved = "budget.approved";
    public const string BudgetReturned = "budget.returned";
    public const string BudgetExecuted = "budget.executed";
    public const string BudgetUtilizationCreated = "budget.utilization.created";
    public const string BudgetUtilizationUpdated = "budget.utilization.updated";
    public const string BudgetUtilizationDeleted = "budget.utilization.deleted";
    public const string BudgetThresholdNotified = "budget.threshold.notified";
    public const string CaseAnonymized = "case.anonymized";
    public const string CasePersonalDataErased = "case.personal_data_erased";

    public const string InvitationSent = "invitation.sent";
    public const string InvitationResent = "invitation.resent";
    public const string InvitationResentNotified = "invitation.resent_notified";

    public const string UserSuspended = "user.suspended";
    public const string UserReactivated = "user.reactivated";
    public const string UserDeleted = "user.deleted";
    public const string TwoFactorProvisioned = "user.two_factor_provisioned";
    public const string TwoFactorEnrolled = "user.two_factor_enrolled";
    public const string TwoFactorReset = "user.two_factor_reset";

    public const string ConfirmationTokenCreated = "auth.confirmation_token_created";
    public const string AccountCreated = "auth.account_created";
    public const string EmailConfirmed = "auth.email_confirmed";
    public const string ConfirmationDeliveryFailed = "auth.confirmation_delivery_failed";
    public const string ConfirmationDelivered = "auth.confirmation_delivered";

    public const string ActivationReissued = "activation_reissued";
    public const string OrganisationActivated = "organisation.activated";

    public const string UserNotificationSent = "user.notification_sent";

    // 2FA events
    public const string TwoFactorBackupUsed = "2fa_backup_used";
    public const string TwoFactorFailedTotp = "2fa_failed_totp";
    public const string TwoFactorBypassGenerated = "2fa_bypass_generated";
    public const string TwoFactorBypassUsed = "2fa_bypass_used";
    public const string TwoFactorMandateEnabled = "2fa_mandate_enabled";
    public const string TwoFactorMandateDisabled = "2fa_mandate_disabled";
    public const string TwoFactorDelegationEnabled = "2fa_delegation_enabled";
    public const string TwoFactorDelegationDisabled = "2fa_delegation_disabled";
    public const string TwoFactorReminderSent = "2fa_reminder_sent";
    public const string TwoFactorMigrationEmailSent = "2fa_migration_email_sent";
}
