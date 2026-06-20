using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class InterventionOverdueJobOptions
{
    public const string SectionName = "InterventionOverdueJob";

    public int IntervalMinutes { get; set; } = 1440;
}

public sealed class InterventionOverdueJobRunner(
    AppDbContext db,
    NotificationService notificationService,
    ILogger<InterventionOverdueJobRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var overdue = await db.Interventions
            .Where(i => i.Direction == InterventionDirection.Needed
                && i.Status == InterventionStatus.Open
                && i.DueAtUtc != null
                && i.DueAtUtc < now
                && i.OverdueNotifiedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var intervention in overdue)
        {
            await notificationService.CreateInterventionOverdueNotificationAsync(intervention, cancellationToken);
            logger.LogInformation(
                "Intervention overdue notification created for intervention {InterventionId} assignee {UserId}",
                intervention.Id,
                intervention.AssignedStaffUserId);
        }
    }
}

public sealed class InterventionOverdueBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<InterventionOverdueJobOptions> options,
    ILogger<InterventionOverdueBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<InterventionOverdueJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Intervention overdue job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
