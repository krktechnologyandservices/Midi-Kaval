using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class CourtMissEscalationJobOptions
{
    public const string SectionName = "CourtMissEscalationJob";

    public int IntervalMinutes { get; set; } = 60;
}

public sealed class CourtMissEscalationJobRunner(
    AppDbContext db,
    NotificationService notificationService,
    PushDeliveryService pushDeliveryService,
    EmailDeliveryService emailDeliveryService,
    ILogger<CourtMissEscalationJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await (
            from sitting in db.CourtSittings
            join caseEntity in db.Cases on sitting.CaseId equals caseEntity.Id
            where sitting.Status == CourtSittingStatus.Upcoming
                && sitting.ScheduledAtUtc < now
                && sitting.MissEscalatedAtUtc == null
                && caseEntity.OrganisationId == sitting.OrganisationId
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            select new SittingEscalationContext(sitting, caseEntity))
            .ToListAsync(cancellationToken);

        foreach (var context in candidates)
        {
            try
            {
                await ProcessSittingAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Court miss escalation job failed for sitting {SittingId}", context.Sitting.Id);
            }
        }
    }

    private async Task ProcessSittingAsync(
        SittingEscalationContext context,
        CancellationToken cancellationToken)
    {
        var sitting = context.Sitting;
        var caseEntity = context.Case;

        if (sitting.MissEscalatedAtUtc is not null)
        {
            return;
        }

        var stillEligible = await (
            from s in db.CourtSittings.AsNoTracking()
            join caseRow in db.Cases.AsNoTracking() on s.CaseId equals caseRow.Id
            where s.Id == sitting.Id
                && s.MissEscalatedAtUtc == null
                && s.Status == CourtSittingStatus.Upcoming
                && s.ScheduledAtUtc < DateTime.UtcNow
                && caseRow.OrganisationId == s.OrganisationId
                && caseRow.CurrentStage != CaseStage.TerminationExclusion
            select s.Id).AnyAsync(cancellationToken);

        if (!stillEligible)
        {
            return;
        }

        var coordinators = await db.Users
            .Where(u => u.OrganisationId == sitting.OrganisationId
                && u.Role == UserRoles.Coordinator
                && u.IsActive
                && u.Email != string.Empty)
            .ToListAsync(cancellationToken);

        await emailDeliveryService.SendCourtMissEscalationEmailsAsync(
            sitting,
            caseEntity,
            cancellationToken);

        var now = DateTime.UtcNow;
        var coordinatorUserIds = coordinators.Select(c => c.Id).ToList();

        var notifications = notificationService.CreateCourtMissEscalationNotificationsForSave(
            sitting,
            caseEntity,
            coordinatorUserIds);
        sitting.MissEscalatedAtUtc = now;
        CourtMissFlagService.SetCaseFlagOnEscalation(caseEntity, now);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = sitting.OrganisationId,
            ActorUserId = null,
            SubjectUserId = caseEntity.AssignedWorkerId,
            EventType = AuditEventTypes.CourtSittingMissEscalated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["courtSittingId"] = sitting.Id.ToString("D"),
                    ["caseId"] = caseEntity.Id.ToString("D"),
                    ["assignedWorkerUserId"] = caseEntity.AssignedWorkerId?.ToString("D"),
                    ["scheduledAtUtc"] = sitting.ScheduledAtUtc.ToString("O"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            await pushDeliveryService.TrySendAsync(notification, cancellationToken);
        }

        logger.LogInformation(
            "Court miss escalated for sitting {SittingId} case {CaseId} coordinatorNotifications {CoordinatorCount}",
            sitting.Id,
            caseEntity.Id,
            coordinatorUserIds.Count);
    }

    private sealed record SittingEscalationContext(CourtSitting Sitting, Case Case);
}

public sealed class CourtMissEscalationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CourtMissEscalationJobOptions> options,
    ILogger<CourtMissEscalationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<CourtMissEscalationJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Court miss escalation job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
