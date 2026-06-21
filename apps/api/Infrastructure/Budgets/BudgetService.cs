using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Budgets;

namespace MidiKaval.Api.Infrastructure.Budgets;

public sealed class BudgetService
{
    private readonly AppDbContext db;
    private readonly IAuditService audit;
    private readonly IHttpContextAccessor httpContextAccessor;

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public BudgetService(AppDbContext db, IAuditService audit, IHttpContextAccessor httpContextAccessor)
    {
        this.db = db;
        this.audit = audit;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<PaginatedResult<BudgetListDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.ProjectBudgets
            .Where(b => b.OrganisationId == organisationId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BudgetListDto
            {
                Id = b.Id,
                Source = b.Source.ToString(),
                FinancialYearStart = b.FinancialYearStart,
                FinancialYearEnd = b.FinancialYearEnd,
                ApprovalStatus = b.ApprovalStatus.ToString(),
                TotalAllocated = b.BudgetLineItems.Sum(li => li.AmountAllocated),
                TotalUtilized = b.BudgetLineItems.Sum(li => li.AmountUtilized),
                CreatedAtUtc = b.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return new PaginatedResult<BudgetListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<BudgetDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .Include(b => b.BudgetLineItems)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        return MapToDetailDto(budget);
    }

    public async Task<BudgetDetailDto> CreateAsync(CreateBudgetRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        // Validate source enum
        if (!Enum.TryParse<BudgetSource>(request.Source.Trim(), ignoreCase: true, out var source)
            || !Enum.IsDefined(source)
            || !Enum.GetName(source)!.Equals(request.Source.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BudgetBusinessRuleException(
                $"'{request.Source}' is not a valid named BudgetSource. Must be one of: DCPU, NPO.");
        }

        // Validate financial year
        ValidateFinancialYear(request.FinancialYearStart, request.FinancialYearEnd);

        // Validate line item amounts and budget heads
        ValidateLineItems(request.LineItems.Select(li => (li.BudgetHead, li.AmountAllocated)), isUpdate: false);

        // Check for duplicate BudgetHead entries in request
        CheckDuplicateBudgetHeads(request.LineItems.Select(li => li.BudgetHead));

        // Check for duplicate source + financial year
        var exists = await db.ProjectBudgets.AnyAsync(
            b => b.OrganisationId == organisationId
                && b.Source == source
                && b.FinancialYearStart == request.FinancialYearStart, ct);

        if (exists)
            throw new BudgetBusinessRuleException("A budget for the same source and financial year already exists.");

        var now = DateTime.UtcNow;

        var budget = new ProjectBudget
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Source = source,
            FinancialYearStart = request.FinancialYearStart,
            FinancialYearEnd = request.FinancialYearEnd,
            ApprovalStatus = BudgetApprovalStatus.Draft,
            Notes = request.Notes,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.ProjectBudgets.Add(budget);

        var lineItems = request.LineItems.Select(li => new BudgetLineItem
        {
            Id = Guid.NewGuid(),
            ProjectBudgetId = budget.Id,
            BudgetHead = Enum.Parse<BudgetHead>(li.BudgetHead.Trim(), ignoreCase: true),
            AmountAllocated = li.AmountAllocated,
            AmountUtilized = 0m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }).ToList();

        db.BudgetLineItems.AddRange(lineItems);

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditEventTypes.BudgetCreated,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
                ["financialYearStart"] = budget.FinancialYearStart.ToString("yyyy-MM-dd"),
            }, cancellationToken: ct);

        return MapToDetailDto(budget, lineItems);
    }

    public async Task<BudgetDetailDto> UpdateAsync(Guid id, UpdateBudgetRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .Include(b => b.BudgetLineItems)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        // Only Draft and Returned allow edits
        if (budget.ApprovalStatus != BudgetApprovalStatus.Draft
            && budget.ApprovalStatus != BudgetApprovalStatus.Returned)
        {
            throw new BudgetBusinessRuleException(
                "Budget can only be updated in Draft or Returned status.");
        }

        // Validate line item amounts and budget heads
        ValidateLineItems(request.LineItems.Select(li => (li.BudgetHead, li.AmountAllocated)), isUpdate: true);

        // Check for duplicate BudgetHead entries in request
        CheckDuplicateBudgetHeads(request.LineItems.Select(li => li.BudgetHead));

        var now = DateTime.UtcNow;
        budget.Notes = request.Notes;
        budget.UpdatedAtUtc = now;

        // Preserve existing line items with utilization, update allocations
        var existingByHead = budget.BudgetLineItems
            .ToDictionary(li => li.BudgetHead.ToString(), StringComparer.OrdinalIgnoreCase);

        var headsInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.LineItems)
        {
            var head = Enum.Parse<BudgetHead>(item.BudgetHead.Trim(), ignoreCase: true);
            var headName = head.ToString();
            headsInRequest.Add(headName);

            if (existingByHead.TryGetValue(headName, out var existing))
            {
                // Preserve existing utilization, update allocation
                if (existing.AmountUtilized > item.AmountAllocated)
                {
                    throw new BudgetBusinessRuleException(
                        $"Cannot reduce allocation for '{headName}' below already utilized amount ({existing.AmountUtilized}).");
                }
                existing.AmountAllocated = item.AmountAllocated;
                existing.UpdatedAtUtc = now;
            }
            else
            {
                // New line item
                budget.BudgetLineItems.Add(new BudgetLineItem
                {
                    Id = Guid.NewGuid(),
                    ProjectBudgetId = budget.Id,
                    BudgetHead = head,
                    AmountAllocated = item.AmountAllocated,
                    AmountUtilized = 0m,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
        }

        // Remove line items no longer in the request
        var itemsToRemove = budget.BudgetLineItems
            .Where(li => !headsInRequest.Contains(li.BudgetHead.ToString()))
            .ToList();

        var utilizedToRemove = itemsToRemove.Where(li => li.AmountUtilized > 0).ToList();
        if (utilizedToRemove.Count > 0)
        {
            var heads = string.Join(", ", utilizedToRemove.Select(li => li.BudgetHead.ToString()));
            throw new BudgetBusinessRuleException(
                $"Cannot remove line items with utilization: {heads}. Remove utilization first.");
        }

        db.BudgetLineItems.RemoveRange(itemsToRemove);

        await db.SaveChangesAsync(ct);

        // Build result excluding the removed items (still in-memory after save)
        var removedIds = new HashSet<Guid>(itemsToRemove.Select(r => r.Id));
        var resultLineItems = budget.BudgetLineItems.Where(li => !removedIds.Contains(li.Id)).ToList();

        await audit.RecordAsync(AuditEventTypes.BudgetUpdated,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
                ["financialYearStart"] = budget.FinancialYearStart.ToString("yyyy-MM-dd"),
            }, cancellationToken: ct);

        return MapToDetailDto(budget, resultLineItems);
    }

    public async Task ProposeAsync(Guid id, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Draft)
            throw new BudgetBusinessRuleException(
                $"Cannot propose budget in '{budget.ApprovalStatus}' status. Only Draft budgets can be proposed.");

        budget.ApprovalStatus = BudgetApprovalStatus.Proposed;
        budget.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditEventTypes.BudgetProposed,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
            }, cancellationToken: ct);
    }

    public async Task ApproveAsync(Guid id, ApproveBudgetRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Proposed)
            throw new BudgetBusinessRuleException(
                $"Cannot approve budget in '{budget.ApprovalStatus}' status. Only Proposed budgets can be approved.");

        var now = DateTime.UtcNow;
        budget.ApprovalStatus = BudgetApprovalStatus.Approved;
        budget.ApprovedByUserId = actorUserId;
        budget.DecisionComment = request.DecisionComment;
        budget.DecidedAtUtc = now;
        budget.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditEventTypes.BudgetApproved,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
            }, cancellationToken: ct);
    }

    public async Task ReturnAsync(Guid id, ReturnBudgetRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Proposed)
            throw new BudgetBusinessRuleException(
                $"Cannot return budget in '{budget.ApprovalStatus}' status. Only Proposed budgets can be returned.");

        var now = DateTime.UtcNow;
        budget.ApprovalStatus = BudgetApprovalStatus.Returned;
        budget.ApprovedByUserId = actorUserId;
        budget.DecisionComment = request.DecisionComment;
        budget.DecidedAtUtc = now;
        budget.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditEventTypes.BudgetReturned,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
            }, cancellationToken: ct);
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganisationId == organisationId, ct);

        if (budget is null)
            throw new BudgetNotFoundException(id);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Approved)
            throw new BudgetBusinessRuleException(
                $"Cannot execute budget in '{budget.ApprovalStatus}' status. Only Approved budgets can be executed.");

        budget.ApprovalStatus = BudgetApprovalStatus.Executed;
        budget.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditEventTypes.BudgetExecuted,
            organisationId, actorUserId, metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budget.Id,
                ["source"] = budget.Source.ToString(),
            }, cancellationToken: ct);
    }

    public async Task<BudgetReportDto> GetReportAsync(BudgetReportFrequency frequency, int year, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        // PSR financial year: April to March
        var fyStart = new DateOnly(year, 4, 1);
        var fyEnd = new DateOnly(year + 1, 3, 31);

        var budgets = await db.ProjectBudgets
            .Include(b => b.BudgetLineItems)
            .Where(b => b.OrganisationId == organisationId
                && b.FinancialYearStart == fyStart
                && (b.ApprovalStatus == BudgetApprovalStatus.Approved
                    || b.ApprovalStatus == BudgetApprovalStatus.Executed))
            .ToListAsync(ct);

        var allLineItems = budgets.SelectMany(b => b.BudgetLineItems).ToList();

        var periodLabel = frequency switch
        {
            BudgetReportFrequency.Annually => $"{year}-{year + 1}",
            BudgetReportFrequency.HalfYearly => $"{year}-H1",
            BudgetReportFrequency.Quarterly => $"{year}-Q1",
            BudgetReportFrequency.Monthly => $"{year}-Apr",
            _ => $"{year}",
        };

        var divisor = frequency switch
        {
            BudgetReportFrequency.Monthly => 12m,
            BudgetReportFrequency.Quarterly => 4m,
            BudgetReportFrequency.HalfYearly => 2m,
            BudgetReportFrequency.Annually => 1m,
            _ => 1m,
        };

        var lines = allLineItems
            .GroupBy(li => li.BudgetHead)
            .Select(g => new BudgetReportLineDto
            {
                BudgetHead = g.Key.ToString(),
                Allocated = Math.Round(g.Sum(li => li.AmountAllocated) / divisor, 2),
                Utilized = Math.Round(g.Sum(li => li.AmountUtilized) / divisor, 2),
                Balance = Math.Round((g.Sum(li => li.AmountAllocated) - g.Sum(li => li.AmountUtilized)) / divisor, 2),
                UtilizationPercentage = g.Sum(li => li.AmountAllocated) > 0
                    ? Math.Round(g.Sum(li => li.AmountUtilized) / g.Sum(li => li.AmountAllocated) * 100, 2)
                    : 0,
            })
            .OrderBy(l => l.BudgetHead)
            .ToList();

        return new BudgetReportDto
        {
            Period = periodLabel,
            Frequency = frequency.ToString(),
            Lines = lines,
            TotalAllocated = lines.Sum(l => l.Allocated),
            TotalUtilized = lines.Sum(l => l.Utilized),
            TotalBalance = lines.Sum(l => l.Balance),
        };
    }

    private static void ValidateFinancialYear(DateOnly start, DateOnly end)
    {
        if (start.Day != 1 || start.Month != 4)
            throw new BudgetBusinessRuleException("Financial year start must be April 1st.");

        if (end <= start)
            throw new BudgetBusinessRuleException("Financial year end must be after the start date.");

        // Validate approximately one year (PSR fiscal year: April to March)
        var expectedEnd = new DateOnly(start.Year + 1, 3, 31);
        if (end != expectedEnd)
            throw new BudgetBusinessRuleException("Financial year end must be March 31st of the following year.");
    }

    private static void ValidateLineItems(IEnumerable<(string BudgetHead, decimal AmountAllocated)> items, bool isUpdate)
    {
        foreach (var (budgetHead, amountAllocated) in items)
        {
            if (amountAllocated < 0)
                throw new BudgetBusinessRuleException("Line item amount cannot be negative.");

            if (!Enum.TryParse<BudgetHead>(budgetHead.Trim(), ignoreCase: true, out var head)
                || !Enum.IsDefined(head)
                || !Enum.GetName(head)!.Equals(budgetHead.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new BudgetBusinessRuleException(
                    $"'{budgetHead}' is not a valid named BudgetHead.");
            }
        }
    }

    private static void CheckDuplicateBudgetHeads(IEnumerable<string> budgetHeads)
    {
        var duplicates = budgetHeads
            .Select(h => h.Trim())
            .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new BudgetBusinessRuleException(
                $"Duplicate budget heads in request: {string.Join(", ", duplicates)}.");
    }

    private static BudgetDetailDto MapToDetailDto(ProjectBudget budget, List<BudgetLineItem>? lineItems = null)
    {
        var items = lineItems ?? budget.BudgetLineItems.ToList();

        return new BudgetDetailDto
        {
            Id = budget.Id,
            Source = budget.Source.ToString(),
            FinancialYearStart = budget.FinancialYearStart,
            FinancialYearEnd = budget.FinancialYearEnd,
            ApprovalStatus = budget.ApprovalStatus.ToString(),
            Notes = budget.Notes,
            LineItems = items.Select(li => new BudgetLineItemDto
            {
                Id = li.Id,
                BudgetHead = li.BudgetHead.ToString(),
                AmountAllocated = li.AmountAllocated,
                AmountUtilized = li.AmountUtilized,
            }).ToList(),
            CreatedByUserId = budget.CreatedByUserId,
            ApprovedByUserId = budget.ApprovedByUserId,
            DecisionComment = budget.DecisionComment,
            DecidedAtUtc = budget.DecidedAtUtc,
            CreatedAtUtc = budget.CreatedAtUtc,
            UpdatedAtUtc = budget.UpdatedAtUtc,
        };
    }

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, userId);
    }
}

public sealed class BudgetNotFoundException(Guid budgetId)
    : KeyNotFoundException($"Budget with ID '{budgetId}' was not found in the current organisation.")
{
    public Guid BudgetId { get; } = budgetId;
}

public sealed class BudgetBusinessRuleException(string message) : InvalidOperationException(message)
{
}

public sealed class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
