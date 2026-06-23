using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

public sealed class CaseAnonymizationJobOptions
{
    public const string SectionName = "CaseAnonymizationJob";
    public int RetentionYears { get; set; } = 7;
    public int BatchSize { get; set; } = 100;
    public int IntervalHours { get; set; } = 24;
}

public sealed class CaseAnonymizationJobOptionsValidator : IValidateOptions<CaseAnonymizationJobOptions>
{
    public ValidateOptionsResult Validate(string? name, CaseAnonymizationJobOptions options)
    {
        if (options.RetentionYears <= 0)
            return ValidateOptionsResult.Fail("CaseAnonymizationJob:RetentionYears must be positive.");
        if (options.BatchSize <= 0)
            return ValidateOptionsResult.Fail("CaseAnonymizationJob:BatchSize must be positive.");
        if (options.IntervalHours <= 0)
            return ValidateOptionsResult.Fail("CaseAnonymizationJob:IntervalHours must be positive.");
        return ValidateOptionsResult.Success;
    }
}

public sealed class CaseAnonymizationJobRunner(
    AppDbContext db,
    IOptions<CaseAnonymizationJobOptions> options,
    ILogger<CaseAnonymizationJobRunner> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        var cutoff = DateTime.UtcNow.AddYears(-opts.RetentionYears);
        var totalAnonymized = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Cases
                .Where(c => c.CurrentStage == CaseStage.TerminationExclusion
                    && !c.ActiveLegalStay
                    && c.UpdatedAtUtc < cutoff)
                .Take(opts.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var c in batch)
            {
                c.BeneficiaryName = null;
                c.BeneficiaryContact = null;
                c.BeneficiaryAge = null;
                c.Latitude = null;
                c.Longitude = null;
                c.Landmark = null;
            }

            // Create per-organisation audit events for accurate audit trail
            foreach (var orgGroup in batch.GroupBy(c => c.OrganisationId))
            {
                var orgCount = orgGroup.Count();
                db.AuditEvents.Add(new Domain.Entities.AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgGroup.Key,
                    EventType = Infrastructure.Audit.AuditEventTypes.CaseAnonymized,
                    CreatedAtUtc = DateTime.UtcNow,
                    ActorUserId = null,
                    SubjectUserId = null,
                    MetadataJson = $$"""{"count":{{orgCount}},"cutoffDate":"{{cutoff:O}}"}""",
                });
            }

            await db.SaveChangesAsync(ct);
            totalAnonymized += batch.Count;
            logger.LogInformation("Anonymized batch of {BatchCount} cases (running total: {Total}).", batch.Count, totalAnonymized);
        }

        if (totalAnonymized == 0)
        {
            logger.LogInformation("No cases eligible for anonymization (cutoff {Cutoff}).", cutoff);
        }
        else
        {
            logger.LogInformation("Anonymization job complete. Total cases processed: {Total}, cutoff: {Cutoff}.", totalAnonymized, cutoff);
        }
    }
}

public sealed class CaseAnonymizationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<CaseAnonymizationJobOptions> options,
    ILogger<CaseAnonymizationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<CaseAnonymizationJobRunner>();
                await runner.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Case anonymization job failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
