using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

public sealed class BudgetThresholdJobOptions
{
    public const string SectionName = "BudgetThresholdJob";

    public int IntervalMinutes { get; set; } = 60;

    public decimal ThresholdPercent { get; set; } = 80m;
}

public sealed class BudgetThresholdJobRunner(
    AppDbContext db,
    NotificationService notificationService,
    PushDeliveryService pushDeliveryService,
    IOptions<BudgetThresholdJobOptions> options,
    ILogger<BudgetThresholdJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var threshold = options.Value.ThresholdPercent;

        var candidates = await (
            from li in db.BudgetLineItems
            join pb in db.ProjectBudgets on li.ProjectBudgetId equals pb.Id
            where (pb.ApprovalStatus == BudgetApprovalStatus.Approved || pb.ApprovalStatus == BudgetApprovalStatus.Executed)
                && li.ThresholdNotifiedAtUtc == null
                && li.AmountAllocated > 0
                && (li.AmountUtilized / li.AmountAllocated * 100) >= threshold
            select new { LineItem = li, Budget = pb })
            .ToListAsync(cancellationToken);

        var processedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                await ProcessLineItemAsync(candidate.LineItem, candidate.Budget, cancellationToken);
                processedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Budget threshold job failed for line item {LineItemId}", candidate.LineItem.Id);
            }
        }

        logger.LogInformation("BudgetThresholdMonitorJob: Notified {Count} budget heads crossing {Threshold}% utilization.", processedCount, threshold);
    }

    private async Task ProcessLineItemAsync(
        BudgetLineItem lineItem,
        ProjectBudget budget,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Atomically claim this line item for notification: the WHERE clause on ThresholdNotifiedAtUtc
        // means a concurrent run (e.g. two instances briefly overlapping during a deploy) that races here
        // will affect zero rows and back off, instead of both proceeding to double-notify.
        var claimed = await db.BudgetLineItems
            .Where(li => li.Id == lineItem.Id && li.ThresholdNotifiedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(li => li.ThresholdNotifiedAtUtc, now), cancellationToken);

        if (claimed == 0)
        {
            return;
        }

        var recipients = await db.Users
            .Where(u => u.OrganisationId == budget.OrganisationId
                && (u.Role == UserRoles.Director || u.Role == UserRoles.Accountant)
                && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var utilizationPercentage = lineItem.AmountAllocated > 0
            ? Math.Round(lineItem.AmountUtilized / lineItem.AmountAllocated * 100, 1)
            : 0m;

        var notifications = notificationService.CreateBudgetThresholdNotificationsForSave(
            budget.OrganisationId,
            budget.Id,
            lineItem.BudgetHead.ToString(),
            budget.Source.ToString(),
            utilizationPercentage,
            recipients);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = budget.OrganisationId,
            ActorUserId = null,
            SubjectUserId = null,
            EventType = AuditEventTypes.BudgetThresholdNotified,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["budgetId"] = budget.Id.ToString("D"),
                    ["budgetLineItemId"] = lineItem.Id.ToString("D"),
                    ["budgetHead"] = lineItem.BudgetHead.ToString(),
                    ["utilizationPercentage"] = utilizationPercentage,
                    ["recipientCount"] = recipients.Count,
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
            "Budget threshold reached: budget {BudgetId} head {BudgetHead} at {Percentage}% — notified {RecipientCount} users.",
            budget.Id,
            lineItem.BudgetHead,
            utilizationPercentage,
            recipients.Count);
    }
}

public sealed class BudgetThresholdMonitorBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BudgetThresholdJobOptions> options,
    ILogger<BudgetThresholdMonitorBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<BudgetThresholdJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Budget threshold monitor job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
