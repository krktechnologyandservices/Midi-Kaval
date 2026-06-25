using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

/// <summary>
/// Recurring Hangfire job (every hour) that scans for organisations in
/// zero-Director state as a fallback detection mechanism (AC-4).
/// </summary>
public sealed class ZeroDirectorMonitorJob(
    AppDbContext db,
    ILogger<ZeroDirectorMonitorJob> logger)
{
    // Maximum alert frequency: once per hour (same as the job interval)
    private static readonly TimeSpan MinAlertInterval = TimeSpan.FromHours(1);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // 1. Find organisations that are active but have zero active Directors
        var orgIds = await db.Organisations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var orgId in orgIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var hasDirector = await db.Users.AsNoTracking().AnyAsync(
                u => u.OrganisationId == orgId
                    && u.Role == UserRoles.Director
                    && u.IsActive
                    && !u.IsSuspended, cancellationToken);

            if (hasDirector)
                continue;

            // Check if an alert was recently sent (dedup via last audit event)
            var lastAlert = await db.AuditEvents
                .AsNoTracking()
                .Where(e =>
                    e.OrganisationId == orgId
                    && e.EventType == "organisation_zero_director_alert")
                .OrderByDescending(e => e.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastAlert is not null && (now - lastAlert.CreatedAtUtc) < MinAlertInterval)
                continue;

            // Trigger recovery flow
            var org = await db.Organisations.FindAsync(new object[] { orgId }, cancellationToken);
            if (org is null)
                continue;

            // Skip if this org is already flagged for recovery (e.g. by ZeroDirectorTriggerService)
            if (org.HasPendingRecovery)
            {
                logger.LogInformation(
                    "ZeroDirectorMonitorJob: Organisation {OrganisationId} already in recovery. Skipping.",
                    orgId);
                continue;
            }

            org.HasPendingRecovery = true;

            // Record audit event for dedup tracking
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = "organisation_zero_director_alert",
                CreatedAtUtc = now,
            });

            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "ZeroDirectorMonitorJob: Organisation {OrganisationId} has zero active Directors. Recovery triggered.",
                orgId);

            // Enqueue alert job via Hangfire
            Hangfire.BackgroundJob.Enqueue<ZeroDirectorAlertJob>(
                j => j.ExecuteAsync(orgId, CancellationToken.None));
        }

        logger.LogInformation(
            "ZeroDirectorMonitorJob: Scanned {OrgCount} organisations. Completed.",
            orgIds.Count);
    }
}
