namespace MidiKaval.Api.Infrastructure.Audit;

/// <summary>
/// Catalog of audit event types whose MetadataJson historically contained PII fields.
/// 
/// Usage: This is a documentation catalog — not a runtime filter. PII stripping is applied
/// at each audit event creation point (per AD-07), not in AuditService itself.
/// 
/// When adding a new audit event type, check whether its MetadataJson will include any
/// beneficiary PII (BeneficiaryName, BeneficiaryContact, BeneficiaryAge, GPS). If so,
/// add it to AffectedTypes below and strip the PII fields at the creation point.
/// </summary>
public static class PiiAuditEventTypes
{
    /// <summary>
    /// Event types whose MetadataJson may contain PII fields that must be stripped.
    /// Keep this list synchronized with the actual audit event creation points.
    /// </summary>
    public static readonly HashSet<string> AffectedTypes =
    [
        // CaseMerged — draftSnapshot previously included beneficiaryName, beneficiaryAge,
        // beneficiaryContact. These are now stripped at the creation point.
        AuditEventTypes.CaseMerged,
    ];

    /// <summary>
    /// Event types that were verified as NOT containing PII in their MetadataJson.
    /// Included for maintenance reference.
    /// </summary>
    public static readonly HashSet<string> VerifiedCleanTypes =
    [
        // CaseCreated — metadata only contains caseId, crimeNumber, stNumber (verified clean)
        AuditEventTypes.CaseCreated,

        // CaseStageChanged — metadata only contains caseId, fromStage, toStage
        AuditEventTypes.CaseStageChanged,

        // CaseTransferred — metadata only contains caseId, fromUserId, toUserId
        AuditEventTypes.CaseTransferred,

        // VisitScheduled / VisitCompleted / VisitRescheduled / VisitStarted / VisitNoteMerged
        // — metadata only contains caseId, visitId, assigneeUserId
        AuditEventTypes.VisitScheduled,
        AuditEventTypes.VisitCompleted,
        AuditEventTypes.VisitRescheduled,
        AuditEventTypes.VisitStarted,
        AuditEventTypes.VisitNoteMerged,

        // CaseGpsVerified — metadata only contains caseId, userId
        AuditEventTypes.CaseGpsVerified,

        // CaseNoteCreated — metadata only contains caseId, noteId
        AuditEventTypes.CaseNoteCreated,

        // InterventionCreated / InterventionUpdated — metadata only contains caseId, interventionId
        AuditEventTypes.InterventionCreated,
        AuditEventTypes.InterventionUpdated,

        // CourtSittingCreated / CourtSittingUpdated — metadata only contains caseId, sittingId
        AuditEventTypes.CourtSittingCreated,
        AuditEventTypes.CourtSittingUpdated,

        // CourtSittingReminderSent / CourtSittingMissEscalated — operational only
        AuditEventTypes.CourtSittingReminderSent,
        AuditEventTypes.CourtSittingMissEscalated,

        // TravelClaim* — metadata only contains claimId, caseIds
        AuditEventTypes.TravelClaimCreated,
        AuditEventTypes.TravelClaimUpdated,
        AuditEventTypes.TravelClaimSubmitted,
        AuditEventTypes.TravelClaimApproved,
        AuditEventTypes.TravelClaimReturned,

        // Attachment* — metadata only contains noteId, blobPath
        AuditEventTypes.AttachmentPresignIssued,
        AuditEventTypes.AttachmentConfirmed,

        // Stage data events (Stage2 through Stage6) — metadata only contains caseId, actorUserId
        AuditEventTypes.Stage2DataCreated,
        AuditEventTypes.Stage2DataUpdated,
        AuditEventTypes.Stage3DataCreated,
        AuditEventTypes.Stage3DataUpdated,
        AuditEventTypes.Stage4PlacementCreated,
        AuditEventTypes.Stage4PlacementUpdated,
        AuditEventTypes.Stage5ReintegrationCreated,
        AuditEventTypes.Stage5ReintegrationUpdated,
        AuditEventTypes.Stage6TerminationExclusionCreated,
        AuditEventTypes.Stage6TerminationExclusionUpdated,

        // CaseLinked / CaseUnlinked — metadata only contains caseId, relatedCaseId
        AuditEventTypes.CaseLinked,
        AuditEventTypes.CaseUnlinked,

        // CasePersonalDataErased — metadata only contains nullifiedFields (field names, no PII values)
        AuditEventTypes.CasePersonalDataErased,

        // Budget* — metadata only contains budgetId, caseId
        AuditEventTypes.BudgetCreated,
        AuditEventTypes.BudgetUpdated,
        AuditEventTypes.BudgetProposed,
        AuditEventTypes.BudgetApproved,
        AuditEventTypes.BudgetReturned,
        AuditEventTypes.BudgetExecuted,
        AuditEventTypes.BudgetUtilizationCreated,
        AuditEventTypes.BudgetUtilizationUpdated,
        AuditEventTypes.BudgetUtilizationDeleted,
    ];

    /// <summary>
    /// Event types that intentionally contain PII-related metadata.
    /// These are excluded from redaction because their purpose IS to log PII access.
    /// </summary>
    public static readonly HashSet<string> IntentionalPiiTypes =
    [
        // CasePiiRevealed — logs when PII was accessed by a user.
        // Metadata contains caseId and userId only (not the beneficiary data itself).
        // Excluded from redaction by design per architecture decision.
        AuditEventTypes.CasePiiRevealed,
    ];
}
