using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Email;

public sealed class EmailDeliveryService(
    AppDbContext db,
    IEmailSender emailSender,
    ILogger<EmailDeliveryService> logger)
{
    public async Task SendCourtReminderEmailsAsync(
        CourtSitting sitting,
        Case caseEntity,
        User assignee,
        CancellationToken cancellationToken = default)
    {
        var eventType = NotificationEventTypes.CourtReminder24h;
        var context = BuildCourtReminderContext(sitting, caseEntity);
        var subject = CourtReminderEmailTemplate.RenderSubject(context);
        var body = CourtReminderEmailTemplate.RenderBody(context);

        var coordinators = await db.Users
            .Where(u => u.OrganisationId == sitting.OrganisationId
                && u.Role == UserRoles.Coordinator
                && u.IsActive)
            .ToListAsync(cancellationToken);

        var sentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var coordinator in coordinators)
        {
            await SendToUserAsync(
                coordinator,
                eventType,
                subject,
                body,
                operationalOverride: false,
                throwOnFailure: true,
                sentEmails,
                cancellationToken);
        }

        await SendToUserAsync(
            assignee,
            eventType,
            subject,
            body,
            operationalOverride: true,
            throwOnFailure: true,
            sentEmails,
            cancellationToken);
    }

    public async Task SendCourtMissEscalationEmailsAsync(
        CourtSitting sitting,
        Case caseEntity,
        CancellationToken cancellationToken = default)
    {
        var eventType = NotificationEventTypes.CourtMissEscalated;
        var context = BuildCourtMissContext(sitting, caseEntity);
        var subject = CourtMissEscalationEmailTemplate.RenderSubject(context);
        var body = CourtMissEscalationEmailTemplate.RenderBody(context);

        var coordinators = await db.Users
            .Where(u => u.OrganisationId == sitting.OrganisationId
                && u.Role == UserRoles.Coordinator
                && u.IsActive)
            .ToListAsync(cancellationToken);

        var sentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var coordinator in coordinators)
        {
            await SendToUserAsync(
                coordinator,
                eventType,
                subject,
                body,
                operationalOverride: false,
                throwOnFailure: true,
                sentEmails,
                cancellationToken);
        }
    }

    public async Task TrySendTravelClaimSubmittedAsync(
        TravelClaim claim,
        string claimantEmail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendTravelClaimSubmittedCoreAsync(claim, claimantEmail, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email delivery failed for travel claim submitted {ClaimId}",
                claim.Id);
        }
    }

    public async Task TrySendTravelClaimDecisionAsync(
        TravelClaim claim,
        string claimantEmail,
        string decisionEventType,
        string? directorComment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendTravelClaimDecisionCoreAsync(
                claim,
                claimantEmail,
                decisionEventType,
                directorComment,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email delivery failed for travel claim decision {ClaimId} eventType {EventType}",
                claim.Id,
                decisionEventType);
        }
    }

    public async Task TrySendCaseTransferredAsync(
        Case caseEntity,
        User newAssignee,
        DateTime transferredAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendCaseTransferredCoreAsync(caseEntity, newAssignee, transferredAtUtc, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email delivery failed for case transferred {CaseId}",
                caseEntity.Id);
        }
    }

    public async Task TrySendReportExportReadyAsync(
        Guid userId,
        Guid organisationId,
        string reportName,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendReportExportReadyCoreAsync(
                userId,
                organisationId,
                reportName,
                expiresAtUtc,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email delivery failed for report export ready user {UserId} report {ReportName}",
                userId,
                reportName);
        }
    }

    private async Task SendTravelClaimSubmittedCoreAsync(
        TravelClaim claim,
        string claimantEmail,
        CancellationToken cancellationToken)
    {
        var eventType = NotificationEventTypes.TravelClaimSubmitted;
        var context = new TravelClaimSubmittedEmailContext(
            claimantEmail,
            claim.Destination,
            claim.Amount,
            claim.ClaimDate,
            claim.Id);
        var subject = TravelClaimSubmittedEmailTemplate.RenderSubject(context);
        var body = TravelClaimSubmittedEmailTemplate.RenderBody(context);

        await SendToDeskStaffAsync(
            claim.OrganisationId,
            eventType,
            subject,
            body,
            throwOnFailure: false,
            cancellationToken);
    }

    private async Task SendTravelClaimDecisionCoreAsync(
        TravelClaim claim,
        string claimantEmail,
        string decisionEventType,
        string? directorComment,
        CancellationToken cancellationToken)
    {
        if (!EmailNotificationChannelMapper.IsKnownEventType(decisionEventType))
        {
            logger.LogInformation(
                "Email skipped for unknown travel claim decision eventType {EventType} claim {ClaimId}",
                decisionEventType,
                claim.Id);
            return;
        }

        var isApproved = decisionEventType == NotificationEventTypes.TravelClaimApproved;
        var context = new TravelClaimDecisionEmailContext(
            claimantEmail,
            claim.Destination,
            claim.Amount,
            isApproved,
            directorComment,
            claim.Id);
        var subject = TravelClaimDecisionEmailTemplate.RenderSubject(context);
        var body = TravelClaimDecisionEmailTemplate.RenderBody(context);

        await SendToDeskStaffAsync(
            claim.OrganisationId,
            decisionEventType,
            subject,
            body,
            throwOnFailure: false,
            cancellationToken);
    }

    private async Task SendCaseTransferredCoreAsync(
        Case caseEntity,
        User newAssignee,
        DateTime transferredAtUtc,
        CancellationToken cancellationToken)
    {
        var eventType = NotificationEventTypes.CaseTransferred;
        var assigneeEmail = newAssignee.Email.Trim();
        var context = new CaseTransferredEmailContext(
            caseEntity.CrimeNumber,
            caseEntity.StNumber,
            assigneeEmail,
            transferredAtUtc);
        var subject = CaseTransferredEmailTemplate.RenderSubject(context);
        var body = CaseTransferredEmailTemplate.RenderBody(context);

        var sentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await SendToDeskStaffAsync(
            caseEntity.OrganisationId,
            eventType,
            subject,
            body,
            throwOnFailure: false,
            sentEmails,
            cancellationToken);

        await SendToUserAsync(
            newAssignee,
            eventType,
            subject,
            body,
            operationalOverride: true,
            throwOnFailure: false,
            sentEmails,
            cancellationToken);
    }

    private async Task SendReportExportReadyCoreAsync(
        Guid userId,
        Guid organisationId,
        string reportName,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var eventType = NotificationEventTypes.ReportExportReady;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == userId && u.OrganisationId == organisationId,
            cancellationToken);

        if (user is null)
        {
            logger.LogInformation(
                "Email skipped for missing user {UserId} eventType {EventType}",
                userId,
                eventType);
            return;
        }

        var context = new ReportExportReadyEmailContext(reportName, expiresAtUtc);
        var subject = ReportExportReadyEmailTemplate.RenderSubject(context);
        var body = ReportExportReadyEmailTemplate.RenderBody(context);
        var sentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await SendToUserAsync(
            user,
            eventType,
            subject,
            body,
            operationalOverride: false,
            throwOnFailure: false,
            sentEmails,
            cancellationToken);
    }

    private async Task SendToDeskStaffAsync(
        Guid organisationId,
        string eventType,
        string subject,
        string body,
        bool throwOnFailure,
        CancellationToken cancellationToken) =>
        await SendToDeskStaffAsync(
            organisationId,
            eventType,
            subject,
            body,
            throwOnFailure,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

    private async Task SendToDeskStaffAsync(
        Guid organisationId,
        string eventType,
        string subject,
        string body,
        bool throwOnFailure,
        HashSet<string> sentEmails,
        CancellationToken cancellationToken)
    {
        var deskStaff = await db.Users
            .Where(u => u.OrganisationId == organisationId
                && u.IsActive
                && (u.Role == UserRoles.Coordinator || u.Role == UserRoles.Director))
            .ToListAsync(cancellationToken);

        foreach (var user in deskStaff)
        {
            await SendToUserAsync(
                user,
                eventType,
                subject,
                body,
                operationalOverride: false,
                throwOnFailure,
                sentEmails,
                cancellationToken);
        }
    }

    private async Task SendToUserAsync(
        User user,
        string eventType,
        string subject,
        string body,
        bool operationalOverride,
        bool throwOnFailure,
        HashSet<string> sentEmails,
        CancellationToken cancellationToken)
    {
        var skipReason = GetSkipReason(user, eventType, operationalOverride);
        if (skipReason is not null)
        {
            logger.LogInformation(
                "Email skipped: {SkipReason} user {UserId} eventType {EventType}",
                skipReason, user.Id, eventType);
            return;
        }

        var toEmail = user.Email.Trim();
        if (!sentEmails.Add(toEmail))
        {
            return;
        }

        try
        {
            await emailSender.SendAsync(new EmailMessage(toEmail, subject, body), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (throwOnFailure)
            {
                throw;
            }

            logger.LogWarning(
                ex,
                "Email send failed for user {UserId} eventType {EventType} to {ToEmail}",
                user.Id,
                eventType,
                toEmail);
        }
    }

    internal static bool ShouldSendToUser(User user, string eventType, bool operationalOverride)
    {
        if (!user.IsActive)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        if (operationalOverride)
        {
            return true;
        }

        if (!EmailNotificationChannelMapper.IsKnownEventType(eventType))
        {
            return false;
        }

        var preferences = NotificationPreferencesDefaults.ForRole(user.Role);
        if (!preferences.EmailEnabled)
        {
            return false;
        }

        if (!EmailNotificationChannelMapper.IsChannelEnabled(preferences.Channels, eventType))
        {
            return false;
        }

        return true;
    }

    private static string? GetSkipReason(User user, string eventType, bool operationalOverride)
    {
        if (!user.IsActive) return "inactive user";
        if (string.IsNullOrWhiteSpace(user.Email)) return "empty email";
        if (operationalOverride) return null;

        if (!EmailNotificationChannelMapper.IsKnownEventType(eventType)) return "unknown event type";

        var preferences = NotificationPreferencesDefaults.ForRole(user.Role);
        if (!preferences.EmailEnabled) return "email disabled for role";
        if (!EmailNotificationChannelMapper.IsChannelEnabled(preferences.Channels, eventType)) return "channel disabled";

        return null;
    }

    private static CourtReminderEmailContext BuildCourtReminderContext(CourtSitting sitting, Case caseEntity) =>
        new(
            sitting.CourtName,
            sitting.ScheduledAtUtc,
            sitting.Purpose,
            caseEntity.SensitivityLevel,
            caseEntity.CrimeNumber,
            caseEntity.StNumber);

    private static CourtMissEscalationEmailContext BuildCourtMissContext(CourtSitting sitting, Case caseEntity) =>
        new(
            sitting.CourtName,
            sitting.ScheduledAtUtc,
            sitting.Purpose,
            caseEntity.SensitivityLevel,
            caseEntity.CrimeNumber,
            caseEntity.StNumber);
}
