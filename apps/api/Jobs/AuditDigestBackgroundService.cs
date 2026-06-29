using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

public sealed class AuditDigestJobOptions
{
    public const string SectionName = "AuditDigestJob";

    public int WindowMinutes { get; set; } = 5;
    public int IntervalMinutes { get; set; } = 5;
}

public sealed class AuditDigestJobRunner(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<AuditDigestJobOptions> options,
    ILogger<AuditDigestJobRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> DigestEventTypes =
    [
        AuditEventTypes.UserSuspended,
        AuditEventTypes.UserReactivated,
        AuditEventTypes.UserDeleted,
        AuditEventTypes.AccountCreated,
    ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        var jobOptions = options.Value;
        var now = DateTime.UtcNow;
        var jobCutoff = now.AddMinutes(-Math.Max(1, jobOptions.WindowMinutes));

        var events = await db.AuditEvents
            .Include(ae => ae.ActorUser)
            .Where(ae => DigestEventTypes.Contains(ae.EventType)
                && !db.AuditDigestEntries.Any(ade => ade.AuditEventId == ae.Id)
                && ae.CreatedAtUtc < jobCutoff)
            .OrderBy(ae => ae.OrganisationId)
            .ThenBy(ae => ae.CreatedAtUtc)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return;
        }

        var orgGroup = events.GroupBy(ae => ae.OrganisationId);
        var allEntries = new List<AuditDigestEntry>();

        foreach (var group in orgGroup)
        {
            var orgId = group.Key;
            var orgEvents = group.ToList();

            var directors = await db.Users
                .Where(u => u.OrganisationId == orgId
                    && u.Role == UserRoles.Director
                    && u.IsActive
                    && !u.IsSuspended)
                .OrderBy(u => u.Email)
                .ToListAsync(ct);

            // Create digest entries once per org (not per director) to avoid unique constraint violation
            var orgBatchId = Guid.NewGuid();
            var sentAt = DateTime.UtcNow;

            if (directors.Count == 0)
            {
                // Mark events as processed even with no active Directors (AC 2)
                foreach (var evt in orgEvents)
                {
                    allEntries.Add(new AuditDigestEntry
                    {
                        Id = Guid.NewGuid(),
                        AuditEventId = evt.Id,
                        OrganisationId = orgId,
                        DigestSentAtUtc = sentAt,
                        DigestBatchId = orgBatchId,
                    });
                }

                logger.LogInformation(
                    "No active Directors for org {OrganisationId} — marking {EventCount} events processed",
                    orgId,
                    orgEvents.Count);

                continue;
            }

            var orgName = await db.Organisations
                .Where(o => o.Id == orgId)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(ct);

            var eventItems = new List<AuditDigestEventItem>(orgEvents.Count);
            foreach (var evt in orgEvents)
            {
                try
                {
                    eventItems.Add(BuildDigestEventItem(evt));
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex,
                        "Skipping malformed event {EventId} in org {OrganisationId} — invalid TargetUserSnapshot JSON",
                        evt.Id,
                        orgId);
                    // Build a minimal event item for malformed events
                    eventItems.Add(new AuditDigestEventItem(
                        FormatEventType(evt.EventType),
                        "Unknown",
                        "Unknown",
                        evt.ActorUser != null ? $"{evt.ActorUser.FirstName} {evt.ActorUser.LastName}".Trim() : "System",
                        evt.ActorUser?.Email ?? "",
                        evt.CreatedAtUtc));
                }
            }

            try
            {
                foreach (var director in directors)
                {
                    if (string.IsNullOrWhiteSpace(director.Email))
                    {
                        logger.LogWarning(
                            "Skipping digest for Director {DirectorId} in org {OrganisationId} — empty email",
                            director.Id,
                            orgId);
                        continue;
                    }

                    try
                    {
                        var context = new AuditDigestEmailContext(
                            orgName ?? "Unknown Organisation",
                            eventItems);

                        var subject = AuditDigestEmailTemplate.RenderSubject(context);
                        var body = AuditDigestEmailTemplate.RenderBody(context);

                        await emailSender.SendAsync(
                            new EmailMessage(director.Email, subject, body),
                            ct);

                        logger.LogInformation(
                            "Audit digest sent: {BatchId} org {OrganisationId} {EventCount} events to Director {DirectorEmail}",
                            orgBatchId,
                            orgId,
                            orgEvents.Count,
                            director.Email);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex,
                            "Failed to send digest for org {OrganisationId} to Director {DirectorEmail} — events will still be marked processed",
                            orgId,
                            director.Email);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "Digest sending cancelled for org {OrganisationId} — entries will still be persisted",
                    orgId);
            }

            // Create entries once per org (not per director) to avoid unique constraint violation
            foreach (var evt in orgEvents)
            {
                allEntries.Add(new AuditDigestEntry
                {
                    Id = Guid.NewGuid(),
                    AuditEventId = evt.Id,
                    OrganisationId = orgId,
                    DigestSentAtUtc = sentAt,
                    DigestBatchId = orgBatchId,
                });
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.AuditDigestEntries.AddRange(allEntries);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation(
            "Audit digest batch complete: {TotalEntries} entries processed across {OrgCount} organisations",
            allEntries.Count,
            orgGroup.Count());
    }

    private static AuditDigestEventItem BuildDigestEventItem(AuditEvent evt)
    {
        var targetSnapshot = evt.TargetUserSnapshot != null
            ? JsonSerializer.Deserialize<TargetSnapshot>(evt.TargetUserSnapshot, JsonOptions)
            : null;

        var actorName = evt.ActorUser != null
            ? $"{evt.ActorUser.FirstName} {evt.ActorUser.LastName}".Trim()
            : "System";
        var actorEmail = evt.ActorUser?.Email ?? "";

        return new AuditDigestEventItem(
            FormatEventType(evt.EventType),
            targetSnapshot?.Name ?? targetSnapshot?.Email ?? "Unknown",
            targetSnapshot?.Email ?? "Unknown",
            actorName,
            actorEmail,
            evt.CreatedAtUtc);
    }

    private static string FormatEventType(string eventType) => eventType switch
    {
        AuditEventTypes.UserSuspended => "User suspended",
        AuditEventTypes.UserReactivated => "User reactivated",
        AuditEventTypes.UserDeleted => "User permanently deleted",
        AuditEventTypes.AccountCreated => "Account created",
        _ => eventType,
    };

    private sealed record TargetSnapshot(string? Name, string? Email, string? Role);
}

public sealed class AuditDigestBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditDigestJobOptions> options,
    ILogger<AuditDigestBackgroundService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _executionLock.WaitAsync(0, stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var runner = scope.ServiceProvider.GetRequiredService<AuditDigestJobRunner>();
                    await runner.RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Audit digest job failed.");
                }
                finally
                {
                    _executionLock.Release();
                }
            }
            else
            {
                logger.LogWarning("Audit digest job skipped — previous execution still running.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _executionLock.Dispose();
        base.Dispose();
    }
}
