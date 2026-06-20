using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Supervisor;

namespace MidiKaval.Api.Infrastructure.Supervisor;

public sealed class CrisisQueueService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IDistributedCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, int> SeverityOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["critical"] = 0,
        ["warning"] = 1,
        ["info"] = 2,
        ["neutral"] = 3,
    };

    public async Task<(CrisisQueueListResultDto Result, int TotalCount)> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveOrganisationId();
        var cacheKey = $"{organisationId}:crisis-queue";

        // Try cache first
        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached is { Length: > 0 })
        {
            var cachedResult = JsonSerializer.Deserialize<CrisisQueueListResultDto>(cached, JsonOptions);
            if (cachedResult is not null)
            {
                return (cachedResult, cachedResult.Items.Count);
            }
        }

        // Cache miss — build from DB
        var now = DateTime.UtcNow;
        var items = new List<CrisisQueueItemDto>();

        // 1. Court-miss rows (existing, preserved from Story 5.4)
        await AddCourtMissRowsAsync(organisationId, now, items, cancellationToken);

        // 2. Overdue visits (new)
        await AddOverdueVisitRowsAsync(organisationId, now, items, cancellationToken);

        // 3. Court <48h without prep (new)
        await AddCourtWarningRowsAsync(organisationId, now, items, cancellationToken);

        // 4. Recent handoffs (new)
        await AddHandoffRowsAsync(organisationId, now, items, cancellationToken);

        // 5. Pending travel claims (existing, preserved from Epic 6)
        await AddPendingClaimRowsAsync(organisationId, items, cancellationToken);

        // Sort: severity priority, then earliest deadline within each tier
        var sorted = items
            .OrderBy(i => SeverityOrder.GetValueOrDefault(i.Severity, int.MaxValue))
            .ThenBy(i => GetDeadline(i))
            .ToList();

        var result = new CrisisQueueListResultDto { Items = sorted };

        // Store in cache with 30s TTL
        var serialized = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
        await cache.SetAsync(cacheKey, serialized, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        }, cancellationToken);

        return (result, sorted.Count);
    }

    public async Task InvalidateCacheAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{organisationId}:crisis-queue";
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }

    private async Task AddCourtMissRowsAsync(
        Guid organisationId, DateTime now, List<CrisisQueueItemDto> items, CancellationToken ct)
    {
        var courtRows = await (
            from sitting in db.CourtSittings
            join caseEntity in db.Cases on sitting.CaseId equals caseEntity.Id
            join assignee in db.Users on caseEntity.AssignedWorkerId equals assignee.Id into assignees
            from assigneeUser in assignees.DefaultIfEmpty()
            where sitting.OrganisationId == organisationId
                && caseEntity.OrganisationId == organisationId
                && sitting.MissEscalatedAtUtc != null
                && sitting.Status == CourtSittingStatus.Upcoming
                && sitting.ScheduledAtUtc < now
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            orderby sitting.ScheduledAtUtc, sitting.Id
            select new
            {
                Sitting = sitting,
                Case = caseEntity,
                AssigneeEmail = assigneeUser != null
                    && assigneeUser.OrganisationId == organisationId
                    && assigneeUser.IsActive
                    ? assigneeUser.Email
                    : null,
            }).ToListAsync(ct);

        foreach (var row in courtRows)
        {
            var assigneeLabel = string.IsNullOrWhiteSpace(row.AssigneeEmail)
                ? "Unassigned"
                : row.AssigneeEmail.Trim();

            items.Add(new CrisisQueueItemDto
            {
                RowType = "court_miss",
                Severity = "critical",
                BadgeLabel = "Court miss",
                CaseId = row.Case.Id,
                CourtSittingId = row.Sitting.Id,
                CourtSittingStatus = row.Sitting.Status.ToString(),
                AssignedWorkerUserId = row.Case.AssignedWorkerId,
                CrimeNumber = row.Case.CrimeNumber,
                StNumber = row.Case.StNumber,
                ScheduledAtUtc = row.Sitting.ScheduledAtUtc,
                Title = $"{row.Case.CrimeNumber} — court sitting past due",
                Detail = $"{assigneeLabel} · {row.Sitting.CourtName}",
            });
        }
    }

    private async Task AddOverdueVisitRowsAsync(
        Guid organisationId, DateTime now, List<CrisisQueueItemDto> items, CancellationToken ct)
    {
        // Group overdue visits by CaseId: overdue = past schedule, not completed/cancelled
        var overdueGroups = await (
            from visit in db.Visits
            join caseEntity in db.Cases on visit.CaseId equals caseEntity.Id
            where visit.OrganisationId == organisationId
                && caseEntity.OrganisationId == organisationId
                && visit.ScheduledAtUtc < now
                && visit.Status != VisitStatus.Completed
                && visit.Status != VisitStatus.Cancelled
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            group visit by new
            {
                caseEntity.Id,
                caseEntity.CrimeNumber,
                caseEntity.StNumber,
                VisitAssigneeUserId = visit.AssigneeUserId,
            } into g
            select new
            {
                CaseId = g.Key.Id,
                CrimeNumber = g.Key.CrimeNumber,
                StNumber = g.Key.StNumber,
                AssignedWorkerUserId = g.Key.VisitAssigneeUserId,
                OverdueCount = g.Count(),
                EarliestScheduledAtUtc = g.Min(v => v.ScheduledAtUtc),
                EarliestVisitId = g.OrderBy(v => v.ScheduledAtUtc).Select(v => (Guid?)v.Id).FirstOrDefault(),
            }).ToListAsync(ct);

        foreach (var group in overdueGroups)
        {
            items.Add(new CrisisQueueItemDto
            {
                RowType = "visit_overdue",
                Severity = "critical",
                BadgeLabel = "Overdue",
                CaseId = group.CaseId,
                VisitId = group.EarliestVisitId,
                OverdueVisitCount = group.OverdueCount,
                AssignedWorkerUserId = group.AssignedWorkerUserId,
                CrimeNumber = group.CrimeNumber,
                StNumber = group.StNumber,
                VisitScheduledAtUtc = group.EarliestScheduledAtUtc,
                ScheduledAtUtc = group.EarliestScheduledAtUtc,
                Title = $"{group.CrimeNumber} — {group.OverdueCount} overdue visit(s)",
                Detail = $"Earliest overdue: {group.EarliestScheduledAtUtc:dd MMM yyyy}",
            });
        }
    }

    private async Task AddCourtWarningRowsAsync(
        Guid organisationId, DateTime now, List<CrisisQueueItemDto> items, CancellationToken ct)
    {
        var horizon = now.AddHours(48);

        var courtRows = await (
            from sitting in db.CourtSittings
            join caseEntity in db.Cases on sitting.CaseId equals caseEntity.Id
            join assignee in db.Users on caseEntity.AssignedWorkerId equals assignee.Id into assignees
            from assigneeUser in assignees.DefaultIfEmpty()
            where sitting.OrganisationId == organisationId
                && caseEntity.OrganisationId == organisationId
                && sitting.Status == CourtSittingStatus.Upcoming
                && sitting.ScheduledAtUtc > now
                && sitting.ScheduledAtUtc <= horizon
                && sitting.MissEscalatedAtUtc == null
                && (sitting.Notes == null || sitting.Notes.Trim() == string.Empty)
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            orderby sitting.ScheduledAtUtc, sitting.Id
            select new
            {
                Sitting = sitting,
                Case = caseEntity,
                AssigneeEmail = assigneeUser != null
                    && assigneeUser.OrganisationId == organisationId
                    && assigneeUser.IsActive
                    ? assigneeUser.Email
                    : null,
            }).ToListAsync(ct);

        foreach (var row in courtRows)
        {
            var assigneeLabel = string.IsNullOrWhiteSpace(row.AssigneeEmail)
                ? "Unassigned"
                : row.AssigneeEmail.Trim();

            items.Add(new CrisisQueueItemDto
            {
                RowType = "court_48h",
                Severity = "warning",
                BadgeLabel = "Court 48h",
                CaseId = row.Case.Id,
                CourtSittingId = row.Sitting.Id,
                CourtSittingStatus = row.Sitting.Status.ToString(),
                AssignedWorkerUserId = row.Case.AssignedWorkerId,
                CrimeNumber = row.Case.CrimeNumber,
                StNumber = row.Case.StNumber,
                ScheduledAtUtc = row.Sitting.ScheduledAtUtc,
                Title = $"{row.Case.CrimeNumber} — court within 48 hours",
                Detail = $"{assigneeLabel} · {row.Sitting.CourtName} · {row.Sitting.ScheduledAtUtc:dd MMM yyyy HH:mm}",
            });
        }
    }

    private async Task AddHandoffRowsAsync(
        Guid organisationId, DateTime now, List<CrisisQueueItemDto> items, CancellationToken ct)
    {
        var sevenDaysAgo = now.AddDays(-7);

        var handoffRows = await (
            from assignment in db.CaseAssignments
            join caseEntity in db.Cases on assignment.CaseId equals caseEntity.Id
            join previousWorker in db.Users on assignment.FromWorkerId equals previousWorker.Id into prevWorkers
            from prevWorker in prevWorkers.DefaultIfEmpty()
            where assignment.OrganisationId == organisationId
                && caseEntity.OrganisationId == organisationId
                && assignment.CreatedAtUtc >= sevenDaysAgo
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            orderby assignment.CreatedAtUtc descending
            select new
            {
                Assignment = assignment,
                Case = caseEntity,
                PreviousWorkerName = prevWorker != null ? prevWorker.Email : null,
            }).ToListAsync(ct);

        foreach (var row in handoffRows)
        {
            items.Add(new CrisisQueueItemDto
            {
                RowType = "handoff",
                Severity = "info",
                BadgeLabel = "Handoff",
                CaseId = row.Case.Id,
                AssignedWorkerUserId = row.Case.AssignedWorkerId,
                CrimeNumber = row.Case.CrimeNumber,
                StNumber = row.Case.StNumber,
                TransferredAtUtc = row.Assignment.CreatedAtUtc,
                PreviousWorkerName = row.PreviousWorkerName ?? "Unknown",
                ScheduledAtUtc = row.Assignment.CreatedAtUtc,
                Title = $"{row.Case.CrimeNumber} — recent handoff",
                Detail = $"From: {row.PreviousWorkerName ?? "Unknown"} · {row.Assignment.CreatedAtUtc:dd MMM yyyy}",
            });
        }
    }

    private async Task AddPendingClaimRowsAsync(
        Guid organisationId, List<CrisisQueueItemDto> items, CancellationToken ct)
    {
        var claimRows = await (
            from claim in db.TravelClaims
            join user in db.Users on claim.ClaimantUserId equals user.Id
            where claim.OrganisationId == organisationId
                && claim.Status == TravelClaimStatus.Submitted
                && user.OrganisationId == organisationId
            orderby claim.SubmittedAtUtc, claim.Id
            select new
            {
                Claim = claim,
                ClaimantEmail = user.Email,
            }).ToListAsync(ct);

        if (claimRows.Count == 0)
        {
            return;
        }

        // Batch-load receipt counts and case links to avoid N+1
        var claimIds = claimRows.Select(r => r.Claim.Id).ToList();

        var receiptCounts = await db.Attachments
            .Where(a => a.OrganisationId == organisationId
                && a.ResourceType == AttachmentResourceType.TravelClaim
                && claimIds.Contains(a.ResourceId)
                && a.Status == AttachmentStatus.Confirmed)
            .GroupBy(a => a.ResourceId)
            .Select(g => new { ResourceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ResourceId, g => g.Count, ct);

        var caseLinks = await db.Set<TravelClaimCaseLink>()
            .Where(l => l.OrganisationId == organisationId && claimIds.Contains(l.TravelClaimId))
            .GroupBy(l => l.TravelClaimId)
            .Select(g => new { TravelClaimId = g.Key, FirstCaseId = g.Min(l => l.CaseId) })
            .ToDictionaryAsync(g => g.TravelClaimId, g => g.FirstCaseId, ct);

        foreach (var row in claimRows)
        {
            if (!caseLinks.TryGetValue(row.Claim.Id, out var firstCaseId) || firstCaseId == Guid.Empty)
            {
                continue;
            }

            var receiptCount = receiptCounts.GetValueOrDefault(row.Claim.Id, 0);
            var claimantName = string.IsNullOrWhiteSpace(row.ClaimantEmail)
                ? "staff"
                : row.ClaimantEmail.Split('@')[0];
            var amountLabel = $"₹{row.Claim.Amount:0.##}";
            var receiptLabel = $"{receiptCount} receipt(s)";

            items.Add(new CrisisQueueItemDto
            {
                RowType = "travel_claim_pending",
                Severity = "neutral",
                BadgeLabel = "Claim",
                CaseId = firstCaseId,
                TravelClaimId = row.Claim.Id,
                ClaimantUserId = row.Claim.ClaimantUserId,
                ClaimantEmail = row.ClaimantEmail,
                Amount = row.Claim.Amount,
                ReceiptCount = receiptCount,
                ScheduledAtUtc = row.Claim.SubmittedAtUtc,
                Title = $"Travel claim from {claimantName} — pending approval",
                Detail = $"{amountLabel} · {receiptLabel}",
            });
        }
    }

    private static DateTime? GetDeadline(CrisisQueueItemDto item) => item.RowType switch
    {
        "visit_overdue" => item.VisitScheduledAtUtc ?? item.ScheduledAtUtc,
        "court_miss" => item.ScheduledAtUtc,
        "court_48h" => item.ScheduledAtUtc,
        "handoff" => item.TransferredAtUtc ?? item.ScheduledAtUtc,
        "travel_claim_pending" => item.ScheduledAtUtc,
        _ => item.ScheduledAtUtc,
    };

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
