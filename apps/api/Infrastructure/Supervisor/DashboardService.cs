using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Supervisor;

namespace MidiKaval.Api.Infrastructure.Supervisor;

public sealed class DashboardService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IDistributedCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();

    public async Task<(DashboardResultDto Result, int TotalCount)> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveOrganisationId();
        var cacheKey = $"{organisationId}:dashboard";

        // Try cache first (no lock — fast path for cache hits)
        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached is { Length: > 0 })
        {
            try
            {
                var cachedResult = JsonSerializer.Deserialize<DashboardResultDto>(cached, JsonOptions);
                if (cachedResult is not null)
                {
                    return (cachedResult, CountNonEmptyWidgets(cachedResult));
                }
            }
            catch (JsonException)
            {
                // Corrupted cache — treat as cache miss, fall through to rebuild
            }
        }

        // Cache miss or corrupted — acquire per-key lock to prevent stampede
        var semaphore = CacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            cached = await cache.GetAsync(cacheKey, cancellationToken);
            if (cached is { Length: > 0 })
            {
                try
                {
                    var cachedResult = JsonSerializer.Deserialize<DashboardResultDto>(cached, JsonOptions);
                    if (cachedResult is not null)
                    {
                        return (cachedResult, CountNonEmptyWidgets(cachedResult));
                    }
                }
                catch (JsonException)
                {
                    // Corrupted cache — remove and recompute
                    await cache.RemoveAsync(cacheKey, cancellationToken);
                }
            }

            var result = await BuildDashboardAsync(organisationId, cancellationToken);

            var serialized = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
            await cache.SetAsync(cacheKey, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            }, cancellationToken);

            var totalCount = CountNonEmptyWidgets(result);
            return (result, totalCount);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task InvalidateCacheAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{organisationId}:dashboard";
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }

    private async Task<DashboardResultDto> BuildDashboardAsync(Guid organisationId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var casesByStageTask = QueryCasesByStageAsync(organisationId, ct);
        var casesByOffenceClassificationTask = QueryCasesByOffenceClassificationAsync(organisationId, ct);
        var casesByDomicileTask = QueryCasesByDomicileAsync(organisationId, ct);
        var casesByStaffTask = QueryCasesByStaffAsync(organisationId, ct);
        var overdueVisitsTask = QueryOverdueVisitsAsync(organisationId, now, ct);
        var interventionsGaugeTask = QueryInterventionsGaugeAsync(organisationId, now, ct);
        var courtThisWeekTask = QueryCourtThisWeekAsync(organisationId, now, ct);
        var pendingClaimsTask = QueryPendingClaimsAsync(organisationId, now, ct);
        var intakeTrendTask = QueryIntakeTrendAsync(organisationId, now, ct);

        await Task.WhenAll(
            casesByStageTask, casesByOffenceClassificationTask, casesByDomicileTask,
            casesByStaffTask, overdueVisitsTask, interventionsGaugeTask,
            courtThisWeekTask, pendingClaimsTask, intakeTrendTask);

        return new DashboardResultDto
        {
            CasesByStage = casesByStageTask.Result,
            CasesByOffenceClassification = casesByOffenceClassificationTask.Result,
            CasesByDomicile = casesByDomicileTask.Result,
            CasesByStaff = casesByStaffTask.Result,
            OverdueVisits = overdueVisitsTask.Result,
            InterventionsGauge = interventionsGaugeTask.Result,
            CourtThisWeek = courtThisWeekTask.Result,
            PendingClaims = pendingClaimsTask.Result,
            IntakeTrend = intakeTrendTask.Result,
        };
    }

    private static int CountNonEmptyWidgets(DashboardResultDto dto)
    {
        var count = 0;
        if (dto.CasesByStage.Count > 0) count++;
        if (dto.CasesByOffenceClassification.Count > 0) count++;
        if (dto.CasesByDomicile.Count > 0) count++;
        if (dto.CasesByStaff.Count > 0) count++;
        if (dto.OverdueVisits.TotalOverdue > 0) count++;
        if (dto.InterventionsGauge.InProgress > 0 || dto.InterventionsGauge.Overdue > 0 || dto.InterventionsGauge.CompletedThisMonth > 0) count++;
        if (dto.CourtThisWeek.TotalUpcoming > 0 || dto.CourtThisWeek.AttendedSoFar > 0) count++;
        if (dto.PendingClaims.PendingCount > 0) count++;
        if (dto.IntakeTrend.Any(i => i.Count > 0)) count++;
        return count;
    }

    private Task<List<CasesByStageDto>> QueryCasesByStageAsync(Guid organisationId, CancellationToken ct)
    {
        return db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .GroupBy(c => c.CurrentStage)
            .Select(g => new CasesByStageDto
            {
                Stage = g.Key.ToString(),
                Count = g.Count(),
            })
            .ToListAsync(ct);
    }

    private Task<List<CasesByOffenceClassificationDto>> QueryCasesByOffenceClassificationAsync(
        Guid organisationId, CancellationToken ct)
    {
        return db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .GroupBy(c => c.OffenceClassification)
            .Select(g => new CasesByOffenceClassificationDto
            {
                OffenceClassification = g.Key.ToString(),
                Count = g.Count(),
            })
            .ToListAsync(ct);
    }

    private Task<List<CasesByDomicileDto>> QueryCasesByDomicileAsync(Guid organisationId, CancellationToken ct)
    {
        return db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .GroupBy(c => c.Domicile)
            .Select(g => new CasesByDomicileDto
            {
                Domicile = g.Key.ToString(),
                Count = g.Count(),
            })
            .ToListAsync(ct);
    }

    private async Task<List<CasesByStaffDto>> QueryCasesByStaffAsync(Guid organisationId, CancellationToken ct)
    {
        return await (
            from c in db.Cases
            join u in db.Users on c.AssignedWorkerId equals u.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            where c.OrganisationId == organisationId
                && c.CurrentStage != CaseStage.TerminationExclusion
                && c.AssignedWorkerId != null
            group new { c, user } by new
            {
                WorkerId = c.AssignedWorkerId!.Value,
                WorkerName = user != null && user.OrganisationId == organisationId
                    ? user.Email
                    : "Unknown",
            } into g
            select new CasesByStaffDto
            {
                WorkerId = g.Key.WorkerId,
                WorkerName = g.Key.WorkerName,
                CaseCount = g.Count(),
            }
        ).ToListAsync(ct);
    }

    private async Task<OverdueVisitsDto> QueryOverdueVisitsAsync(Guid organisationId, DateTime now, CancellationToken ct)
    {
        var overdueQuery = db.Visits
            .Where(v => v.OrganisationId == organisationId
                && v.ScheduledAtUtc < now
                && v.Status != VisitStatus.Completed
                && v.Status != VisitStatus.Cancelled);

        var totalOverdue = await overdueQuery.CountAsync(ct);
        var uniqueCasesAffected = await overdueQuery
            .Select(v => v.CaseId)
            .Distinct()
            .CountAsync(ct);

        return new OverdueVisitsDto
        {
            TotalOverdue = totalOverdue,
            UniqueCasesAffected = uniqueCasesAffected,
        };
    }

    private async Task<InterventionsGaugeDto> QueryInterventionsGaugeAsync(
        Guid organisationId, DateTime now, CancellationToken ct)
    {
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var inProgressTask = db.Interventions
            .Where(i => i.OrganisationId == organisationId
                && (i.Status == InterventionStatus.Open || i.Status == InterventionStatus.InProgress))
            .CountAsync(ct);

        var overdueTask = db.Interventions
            .Where(i => i.OrganisationId == organisationId
                && i.DueAtUtc != null
                && i.DueAtUtc < now
                && i.Status != InterventionStatus.Completed
                && i.Status != InterventionStatus.Cancelled)
            .CountAsync(ct);

        var completedThisMonthTask = db.Interventions
            .Where(i => i.OrganisationId == organisationId
                && i.Status == InterventionStatus.Completed
                && i.ProvidedAtUtc >= monthStart)
            .CountAsync(ct);

        await Task.WhenAll(inProgressTask, overdueTask, completedThisMonthTask);

        return new InterventionsGaugeDto
        {
            InProgress = inProgressTask.Result,
            Overdue = overdueTask.Result,
            CompletedThisMonth = completedThisMonthTask.Result,
        };
    }

    private async Task<CourtThisWeekDto> QueryCourtThisWeekAsync(Guid organisationId, DateTime now, CancellationToken ct)
    {
        // Monday 00:00:00 UTC of the current week
        var daysSinceMonday = (int)now.DayOfWeek - (int)DayOfWeek.Monday;
        if (daysSinceMonday < 0) daysSinceMonday += 7;
        var weekStart = now.Date.AddDays(-daysSinceMonday);
        var weekEnd = weekStart.AddDays(7);

        var weekQuery = db.CourtSittings
            .Where(cs => cs.OrganisationId == organisationId
                && cs.ScheduledAtUtc >= weekStart
                && cs.ScheduledAtUtc < weekEnd);

        var totalUpcoming = await weekQuery
            .CountAsync(cs => cs.Status == CourtSittingStatus.Upcoming, ct);

        var attendedSoFar = await weekQuery
            .CountAsync(cs => cs.Status == CourtSittingStatus.Attended, ct);

        var totalCasesWithSittings = await weekQuery
            .Select(cs => cs.CaseId)
            .Distinct()
            .CountAsync(ct);

        return new CourtThisWeekDto
        {
            TotalUpcoming = totalUpcoming,
            AttendedSoFar = attendedSoFar,
            TotalCasesWithSittings = totalCasesWithSittings,
        };
    }

    private async Task<PendingClaimsDto> QueryPendingClaimsAsync(Guid organisationId, DateTime now, CancellationToken ct)
    {
        var pendingQuery = db.TravelClaims
            .Where(tc => tc.OrganisationId == organisationId
                && tc.Status == TravelClaimStatus.Submitted);

        var pendingCount = await pendingQuery.CountAsync(ct);
        var totalAmountPending = await pendingQuery
            .Where(tc => tc.Amount > 0)
            .SumAsync(tc => (decimal?)tc.Amount, ct) ?? 0m;

        var oldestSubmittedAtUtc = await pendingQuery
            .MinAsync(tc => (DateTime?)tc.SubmittedAtUtc, ct);

        var oldestPendingDays = oldestSubmittedAtUtc.HasValue
            ? (int)(now - oldestSubmittedAtUtc.Value).TotalDays
            : 0;

        return new PendingClaimsDto
        {
            PendingCount = pendingCount,
            TotalAmountPending = totalAmountPending,
            OldestPendingDays = oldestPendingDays,
        };
    }

    private async Task<List<IntakeTrendPointDto>> QueryIntakeTrendAsync(
        Guid organisationId, DateTime now, CancellationToken ct)
    {
        // Generate 12-month label list (oldest first)
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var months = Enumerable.Range(0, 12)
            .Select(i => startMonth.AddMonths(i))
            .ToList();

        // Query monthly case counts from DB
        var monthlyCounts = await db.Cases
            .Where(c => c.OrganisationId == organisationId && c.CreatedAtUtc >= startMonth)
            .GroupBy(c => new { c.CreatedAtUtc.Year, c.CreatedAtUtc.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var countLookup = monthlyCounts
            .GroupBy(x => (x.Year, x.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        return months.Select(m =>
        {
            var key = (m.Year, m.Month);
            return new IntakeTrendPointDto
            {
                Month = $"{m.Year}-{m.Month:D2}",
                Count = countLookup.GetValueOrDefault(key, 0),
            };
        }).ToList();
    }

    private Guid ResolveOrganisationId()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var organisationClaim = httpContext.User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (!Guid.TryParse(organisationClaim, out var organisationId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return organisationId;
    }
}
