using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class CourtReminderJobOptions
{
    public const string SectionName = "CourtReminderJob";

    public int IntervalMinutes { get; set; } = 60;

    public int LeadHours { get; set; } = 24;

    public int WindowMinutes { get; set; } = 60;
}

public sealed class CourtReminderJobRunner(
    AppDbContext db,
    NotificationService notificationService,
    PushDeliveryService pushDeliveryService,
    EmailDeliveryService emailDeliveryService,
    IOptions<CourtReminderJobOptions> options,
    ILogger<CourtReminderJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var jobOptions = options.Value;
        var windowHalf = TimeSpan.FromMinutes(Math.Max(1, jobOptions.WindowMinutes) / 2.0);
        var leadTime = TimeSpan.FromHours(Math.Max(1, jobOptions.LeadHours));
        var windowStart = now + leadTime - windowHalf;
        var windowEnd = now + leadTime + windowHalf;

        var candidates = await (
            from sitting in db.CourtSittings
            join caseEntity in db.Cases on sitting.CaseId equals caseEntity.Id
            join assignee in db.Users on caseEntity.AssignedWorkerId equals assignee.Id
            where sitting.Status == CourtSittingStatus.Upcoming
                && sitting.ReminderSentAtUtc == null
                && caseEntity.OrganisationId == sitting.OrganisationId
                && caseEntity.AssignedWorkerId != null
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
                && assignee.IsActive
                && sitting.ScheduledAtUtc >= windowStart
                && sitting.ScheduledAtUtc <= windowEnd
            select new SittingReminderContext(sitting, caseEntity, assignee))
            .ToListAsync(cancellationToken);

        foreach (var context in candidates)
        {
            try
            {
                await ProcessSittingAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Court reminder job failed for sitting {SittingId}", context.Sitting.Id);
            }
        }
    }

    private async Task ProcessSittingAsync(
        SittingReminderContext context,
        CancellationToken cancellationToken)
    {
        var sitting = context.Sitting;
        var caseEntity = context.Case;
        var assignee = context.Assignee;

        if (!assignee.IsActive || caseEntity.AssignedWorkerId is null)
        {
            logger.LogInformation(
                "Skipping court reminder for sitting {SittingId}: assignee missing or inactive",
                sitting.Id);
            return;
        }

        if (sitting.ReminderSentAtUtc is not null)
        {
            return;
        }

        await emailDeliveryService.SendCourtReminderEmailsAsync(
            sitting,
            caseEntity,
            assignee,
            cancellationToken);

        var now = DateTime.UtcNow;
        var notification = notificationService.CreateCourtReminderNotificationForSave(sitting, assignee.Id);
        sitting.ReminderSentAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = sitting.OrganisationId,
            ActorUserId = null,
            SubjectUserId = assignee.Id,
            EventType = AuditEventTypes.CourtSittingReminderSent,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["courtSittingId"] = sitting.Id.ToString("D"),
                    ["caseId"] = caseEntity.Id.ToString("D"),
                    ["assignedWorkerUserId"] = assignee.Id.ToString("D"),
                    ["scheduledAtUtc"] = sitting.ScheduledAtUtc.ToString("O"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        await pushDeliveryService.TrySendAsync(notification, cancellationToken);

        logger.LogInformation(
            "Court reminder sent for sitting {SittingId} assignee {AssigneeId}",
            sitting.Id,
            assignee.Id);
    }

    private sealed record SittingReminderContext(CourtSitting Sitting, Case Case, User Assignee);
}

public sealed class CourtReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CourtReminderJobOptions> options,
    ILogger<CourtReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<CourtReminderJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Court reminder job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
