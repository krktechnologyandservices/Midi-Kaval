using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class NotificationService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    PushDeliveryService pushDeliveryService)
{
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var (organisationId, userId) = ResolveActorContext();

        return await db.InAppNotifications.CountAsync(
            n => n.OrganisationId == organisationId && n.UserId == userId && n.ReadAtUtc == null,
            cancellationToken);
    }

    public async Task<NotificationListResultDto> ListForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var (organisationId, userId) = ResolveActorContext();

        var items = await db.InAppNotifications
            .Where(n => n.OrganisationId == organisationId && n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => MapToDto(n))
            .ToListAsync(cancellationToken);

        return new NotificationListResultDto { Items = items };
    }

    public async Task<NotificationDto> MarkReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, userId) = ResolveActorContext();

        var notification = await db.InAppNotifications.SingleOrDefaultAsync(
            n => n.Id == notificationId && n.OrganisationId == organisationId && n.UserId == userId,
            cancellationToken);

        if (notification is null)
        {
            throw new NotificationNotFoundException();
        }

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(notification);
    }

    public async Task ResolveInterventionOverdueAsync(
        Guid interventionId,
        Guid organisationId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notifications = await db.InAppNotifications
            .Where(n => n.OrganisationId == organisationId
                && n.ResourceType == "Intervention"
                && n.ResourceId == interventionId
                && n.EventType == NotificationEventTypes.InterventionOverdue
                && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.ReadAtUtc = now;
        }

        if (notifications.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    internal async Task CreateInterventionOverdueNotificationAsync(
        Intervention intervention,
        CancellationToken cancellationToken = default)
    {
        if (intervention.OverdueNotifiedAtUtc is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var title = "Intervention overdue";
        var body = $"{intervention.CategoryName} intervention is past due.";

        var notification = new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = intervention.OrganisationId,
            UserId = intervention.AssignedStaffUserId,
            EventType = NotificationEventTypes.InterventionOverdue,
            Title = title,
            Body = body,
            CaseId = intervention.CaseId,
            ResourceType = "Intervention",
            ResourceId = intervention.Id,
            CreatedAtUtc = now,
        };

        db.InAppNotifications.Add(notification);
        intervention.OverdueNotifiedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await pushDeliveryService.TrySendAsync(notification, cancellationToken);
    }

    internal InAppNotification CreateCourtReminderNotificationForSave(
        CourtSitting sitting,
        Guid assigneeUserId)
    {
        var now = DateTime.UtcNow;
        var title = "Court sitting reminder";
        var body = $"{sitting.CourtName} — scheduled {sitting.ScheduledAtUtc:O} UTC";

        var notification = new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = sitting.OrganisationId,
            UserId = assigneeUserId,
            EventType = NotificationEventTypes.CourtReminder24h,
            Title = title,
            Body = body,
            CaseId = sitting.CaseId,
            ResourceType = "CourtSitting",
            ResourceId = sitting.Id,
            CreatedAtUtc = now,
        };

        db.InAppNotifications.Add(notification);
        return notification;
    }

    internal IReadOnlyList<InAppNotification> CreateCourtMissEscalationNotificationsForSave(
        CourtSitting sitting,
        Case caseEntity,
        IReadOnlyList<Guid> coordinatorUserIds)
    {
        var now = DateTime.UtcNow;
        var title = "Court sitting missed";
        var body = $"{caseEntity.CrimeNumber} — {sitting.CourtName} past due ({sitting.ScheduledAtUtc:O} UTC)";

        var notifications = new List<InAppNotification>(coordinatorUserIds.Count);
        foreach (var coordinatorUserId in coordinatorUserIds)
        {
            var notification = new InAppNotification
            {
                Id = Guid.NewGuid(),
                OrganisationId = sitting.OrganisationId,
                UserId = coordinatorUserId,
                EventType = NotificationEventTypes.CourtMissEscalated,
                Title = title,
                Body = body,
                CaseId = sitting.CaseId,
                ResourceType = "CourtSitting",
                ResourceId = sitting.Id,
                CreatedAtUtc = now,
            };

            db.InAppNotifications.Add(notification);
            notifications.Add(notification);
        }

        return notifications;
    }

    internal IReadOnlyList<InAppNotification> CreateBudgetThresholdNotificationsForSave(
        Guid organisationId,
        Guid budgetId,
        string budgetHead,
        string source,
        decimal utilizationPercentage,
        IReadOnlyList<Guid> recipientUserIds)
    {
        var now = DateTime.UtcNow;
        var title = "Budget nearing limit";
        var body = $"{budgetHead} ({source}) is at {utilizationPercentage}% utilization.";

        var notifications = new List<InAppNotification>(recipientUserIds.Count);
        foreach (var userId in recipientUserIds)
        {
            var notification = new InAppNotification
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                UserId = userId,
                EventType = NotificationEventTypes.BudgetThresholdReached,
                Title = title,
                Body = body,
                CaseId = null,
                ResourceType = "ProjectBudget",
                ResourceId = budgetId,
                CreatedAtUtc = now,
            };

            db.InAppNotifications.Add(notification);
            notifications.Add(notification);
        }

        return notifications;
    }

    internal InAppNotification CreateTravelClaimDecisionNotificationForSave(
        TravelClaim claim,
        Guid caseId,
        string eventType,
        string? decisionComment)
    {
        var (title, body) = eventType switch
        {
            NotificationEventTypes.TravelClaimApproved =>
                TravelClaimNotificationCopy.BuildApproved(claim, decisionComment),
            NotificationEventTypes.TravelClaimReturned =>
                TravelClaimNotificationCopy.BuildReturned(claim, decisionComment!),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
        };

        var notification = new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = claim.OrganisationId,
            UserId = claim.ClaimantUserId,
            EventType = eventType,
            Title = title,
            Body = body,
            CaseId = caseId,
            ResourceType = "TravelClaim",
            ResourceId = claim.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.InAppNotifications.Add(notification);
        return notification;
    }

    private static NotificationDto MapToDto(InAppNotification notification) =>
        new()
        {
            Id = notification.Id,
            EventType = notification.EventType,
            Title = notification.Title,
            Body = notification.Body,
            CaseId = notification.CaseId,
            ResourceType = notification.ResourceType,
            ResourceId = notification.ResourceId,
            IsRead = notification.ReadAtUtc is not null,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = notification.ReadAtUtc,
        };

    private (Guid OrganisationId, Guid UserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, userId);
    }
}

public sealed class NotificationNotFoundException : Exception;
