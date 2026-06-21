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

public sealed class BudgetUtilizationService
{
    private readonly AppDbContext db;
    private readonly IAuditService audit;
    private readonly IHttpContextAccessor httpContextAccessor;

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public BudgetUtilizationService(AppDbContext db, IAuditService audit, IHttpContextAccessor httpContextAccessor)
    {
        this.db = db;
        this.audit = audit;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<PaginatedResult<BudgetUtilizationListDto>> ListAsync(
        Guid budgetId, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct)
    {
        var (orgId, _) = ResolveActorContext();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Verify budget exists in org
        var budgetExists = await db.ProjectBudgets
            .AnyAsync(b => b.Id == budgetId && b.OrganisationId == orgId, ct);
        if (!budgetExists)
            throw new BudgetNotFoundException(budgetId);

        // Validate date range
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            throw new BudgetBusinessRuleException("fromDate must not be after toDate.");

        // Get all line item IDs for this budget
        var lineItemIds = await db.BudgetLineItems
            .Where(li => li.ProjectBudgetId == budgetId)
            .Select(li => li.Id)
            .ToListAsync(ct);

        var query = db.BudgetUtilizations
            .Where(u => lineItemIds.Contains(u.BudgetLineItemId))
            .Where(u => u.DeletedAtUtc == null); // Exclude soft-deleted

        if (fromDate.HasValue)
            query = query.Where(u => u.UtilizationDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(u => u.UtilizationDate <= toDate.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(u => u.UtilizationDate)
            .ThenByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new BudgetUtilizationListDto
            {
                Id = u.Id,
                BudgetLineItemId = u.BudgetLineItemId,
                BudgetHead = u.BudgetLineItem.BudgetHead.ToString(),
                CaseId = u.CaseId,
                CaseCrimeNumber = u.Case != null ? u.Case.CrimeNumber : null,
                AmountUtilized = u.AmountUtilized,
                UtilizationDate = u.UtilizationDate,
                Description = u.Description,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return new PaginatedResult<BudgetUtilizationListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<BudgetUtilizationSummaryDto> GetSummaryAsync(Guid budgetId, CancellationToken ct)
    {
        var (orgId, _) = ResolveActorContext();

        var budget = await db.ProjectBudgets
            .Include(b => b.BudgetLineItems)
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.OrganisationId == orgId, ct);
        if (budget == null)
            throw new BudgetNotFoundException(budgetId);

        var lineItemIds = budget.BudgetLineItems.Select(li => li.Id).ToList();

        var utilizationGroups = await db.BudgetUtilizations
            .Where(u => lineItemIds.Contains(u.BudgetLineItemId))
            .GroupBy(u => u.BudgetLineItemId)
            .Select(g => new
            {
                BudgetLineItemId = g.Key,
                TotalUtilized = g.Sum(u => (decimal?)u.AmountUtilized) ?? 0m,
            })
            .ToListAsync(ct);

        var utilizationByLineItemId = utilizationGroups.ToDictionary(g => g.BudgetLineItemId, g => g.TotalUtilized);

        var headSummaries = budget.BudgetLineItems.Select(li =>
        {
            var utilized = utilizationByLineItemId.GetValueOrDefault(li.Id, 0m);
            var balance = li.AmountAllocated - utilized;
            return new BudgetHeadSummaryDto
            {
                BudgetHead = li.BudgetHead.ToString(),
                Allocated = li.AmountAllocated,
                Utilized = utilized,
                Balance = balance,
                UtilizationPercentage = li.AmountAllocated > 0
                    ? Math.Round(utilized / li.AmountAllocated * 100, 2)
                    : 0m,
            };
        }).ToList();

        var totalAllocated = budget.BudgetLineItems.Sum(li => li.AmountAllocated);
        var totalUtilized = utilizationGroups.Sum(g => g.TotalUtilized);

        return new BudgetUtilizationSummaryDto
        {
            BudgetId = budgetId,
            HeadSummaries = headSummaries,
            TotalAllocated = totalAllocated,
            TotalUtilized = totalUtilized,
            TotalBalance = totalAllocated - totalUtilized,
            OverallUtilizationPercentage = totalAllocated > 0
                ? Math.Round(totalUtilized / totalAllocated * 100, 2)
                : 0m,
        };
    }

    public async Task<BudgetUtilizationListDto> CreateAsync(Guid budgetId, CreateBudgetUtilizationRequest request, CancellationToken ct)
    {
        var (orgId, actorUserId) = ResolveActorContext();

        // Verify the budget exists and is in Approved or Executed status
        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.OrganisationId == orgId, ct);
        if (budget == null)
            throw new BudgetNotFoundException(budgetId);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Approved && budget.ApprovalStatus != BudgetApprovalStatus.Executed)
            throw new BudgetBusinessRuleException("Cannot record utilization against a budget that is not Approved or Executed.");

        // Validate CaseId if provided
        if (request.CaseId.HasValue)
        {
            var caseExists = await db.Cases.AnyAsync(c => c.Id == request.CaseId.Value, ct);
            if (!caseExists)
                throw new BudgetBusinessRuleException("The specified case does not exist.");
        }

        // Verify the line item belongs to this budget and fetch current amounts
        var lineItem = await db.BudgetLineItems
            .FirstOrDefaultAsync(li => li.Id == request.BudgetLineItemId && li.ProjectBudgetId == budgetId, ct);
        if (lineItem == null)
            throw new BudgetNotFoundException(budgetId);

        // Validate amount doesn't exceed remaining allocation
        var newTotalUtilized = lineItem.AmountUtilized + request.AmountUtilized;
        if (newTotalUtilized > lineItem.AmountAllocated)
            throw new BudgetBusinessRuleException(
                $"Utilizing {request.AmountUtilized} would exceed the allocated amount of {lineItem.AmountAllocated} for this budget head. " +
                $"Current utilized: {lineItem.AmountUtilized}, remaining: {lineItem.AmountAllocated - lineItem.AmountUtilized}.");

        var utilization = new BudgetUtilization
        {
            Id = Guid.NewGuid(),
            BudgetLineItemId = request.BudgetLineItemId,
            CaseId = request.CaseId,
            AmountUtilized = request.AmountUtilized,
            UtilizationDate = request.UtilizationDate,
            Description = request.Description,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        // Increment the line item's running total
        lineItem.AmountUtilized += request.AmountUtilized;
        lineItem.UpdatedAtUtc = DateTime.UtcNow;

        db.BudgetUtilizations.Add(utilization);

        await audit.RecordAsync(AuditEventTypes.BudgetUtilizationCreated, orgId, actorUserId,
            metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budgetId,
                ["budgetLineItemId"] = request.BudgetLineItemId,
                ["utilizationId"] = utilization.Id,
                ["amountUtilized"] = request.AmountUtilized,
                ["utilizationDate"] = request.UtilizationDate.ToString("yyyy-MM-dd"),
            });

        await db.SaveChangesAsync(ct);

        // Re-fetch to include Case crime number
        var createdUtilization = await db.BudgetUtilizations
            .Where(u => u.Id == utilization.Id)
            .Select(u => new BudgetUtilizationListDto
            {
                Id = u.Id,
                BudgetLineItemId = u.BudgetLineItemId,
                BudgetHead = u.BudgetLineItem.BudgetHead.ToString(),
                CaseId = u.CaseId,
                CaseCrimeNumber = u.Case != null ? u.Case.CrimeNumber : null,
                AmountUtilized = u.AmountUtilized,
                UtilizationDate = u.UtilizationDate,
                Description = u.Description,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .FirstAsync(ct);

        return createdUtilization;
    }

    public async Task<BudgetUtilizationListDto> UpdateAsync(
        Guid budgetId, Guid id, UpdateBudgetUtilizationRequest request, CancellationToken ct)
    {
        var (orgId, actorUserId) = ResolveActorContext();

        // Verify the budget exists and is Approved/Executed
        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.OrganisationId == orgId, ct);
        if (budget == null)
            throw new BudgetNotFoundException(budgetId);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Approved && budget.ApprovalStatus != BudgetApprovalStatus.Executed)
            throw new BudgetBusinessRuleException("Cannot update utilization on a budget that is not Approved or Executed.");

        // Load the utilization entry and its line item
        var utilization = await db.BudgetUtilizations
            .Include(u => u.BudgetLineItem)
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAtUtc == null, ct);
        if (utilization == null)
            throw new BudgetBusinessRuleException("Utilization entry not found.");

        // Verify utilization belongs to this budget
        if (utilization.BudgetLineItem.ProjectBudgetId != budgetId)
            throw new BudgetBusinessRuleException("The utilization entry does not belong to the specified budget.");

        var lineItem = utilization.BudgetLineItem;

        // Calculate new total: subtract old amount, add new amount
        var newTotalUtilized = lineItem.AmountUtilized - utilization.AmountUtilized + request.AmountUtilized;
        if (newTotalUtilized > lineItem.AmountAllocated)
            throw new BudgetBusinessRuleException(
                $"Updating to {request.AmountUtilized} would exceed the allocated amount of {lineItem.AmountAllocated} for this budget head.");

        var oldAmount = utilization.AmountUtilized;

        // Update the utilization record
        utilization.AmountUtilized = request.AmountUtilized;
        utilization.UtilizationDate = request.UtilizationDate;
        utilization.CaseId = request.CaseId;
        utilization.Description = request.Description;
        utilization.UpdatedAtUtc = DateTime.UtcNow;

        // Adjust the line item's running total
        lineItem.AmountUtilized = newTotalUtilized;
        lineItem.UpdatedAtUtc = DateTime.UtcNow;

        await audit.RecordAsync(AuditEventTypes.BudgetUtilizationUpdated, orgId, actorUserId,
            metadata: new Dictionary<string, object?>
            {
                ["budgetId"] = budgetId,
                ["utilizationId"] = id,
                ["oldAmountUtilized"] = oldAmount,
                ["newAmountUtilized"] = request.AmountUtilized,
            });

        await db.SaveChangesAsync(ct);

        // Re-fetch to include Case crime number
        var updatedUtilization = await db.BudgetUtilizations
            .Where(u => u.Id == id)
            .Select(u => new BudgetUtilizationListDto
            {
                Id = u.Id,
                BudgetLineItemId = u.BudgetLineItemId,
                BudgetHead = u.BudgetLineItem.BudgetHead.ToString(),
                CaseId = u.CaseId,
                CaseCrimeNumber = u.Case != null ? u.Case.CrimeNumber : null,
                AmountUtilized = u.AmountUtilized,
                UtilizationDate = u.UtilizationDate,
                Description = u.Description,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .FirstAsync(ct);

        return updatedUtilization;
    }

    public async Task DeleteAsync(Guid budgetId, Guid id, bool force, CancellationToken ct)
    {
        var (orgId, actorUserId) = ResolveActorContext();

        // Verify the budget exists
        var budget = await db.ProjectBudgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.OrganisationId == orgId, ct);
        if (budget == null)
            throw new BudgetNotFoundException(budgetId);

        if (budget.ApprovalStatus != BudgetApprovalStatus.Approved && budget.ApprovalStatus != BudgetApprovalStatus.Executed)
            throw new BudgetBusinessRuleException("Cannot delete utilization on a budget that is not Approved or Executed.");

        // Load the utilization entry
        var utilization = await db.BudgetUtilizations
            .Include(u => u.BudgetLineItem)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (utilization == null)
            throw new BudgetBusinessRuleException("Utilization entry not found.");

        // Verify utilization belongs to this budget
        if (utilization.BudgetLineItem.ProjectBudgetId != budgetId)
            throw new BudgetBusinessRuleException("The utilization entry does not belong to the specified budget.");

        if (force)
        {
            // Soft delete: mark as deleted but keep the AmountUtilized
            utilization.DeletedAtUtc = DateTime.UtcNow;
            utilization.UpdatedAtUtc = DateTime.UtcNow;

            await audit.RecordAsync(AuditEventTypes.BudgetUtilizationDeleted, orgId, actorUserId,
                metadata: new Dictionary<string, object?>
                {
                    ["budgetId"] = budgetId,
                    ["utilizationId"] = id,
                    ["deleteType"] = "soft",
                    ["amountUtilized"] = utilization.AmountUtilized,
                });
        }
        else
        {
            // Hard delete: reverse the AmountUtilized
            // Prevent double-decrementing if already soft-deleted
            if (utilization.DeletedAtUtc != null)
                throw new BudgetBusinessRuleException("Cannot hard-delete a utilization entry that has already been soft-deleted.");

            var lineItem = utilization.BudgetLineItem;
            lineItem.AmountUtilized -= utilization.AmountUtilized;
            lineItem.UpdatedAtUtc = DateTime.UtcNow;

            db.BudgetUtilizations.Remove(utilization);

            await audit.RecordAsync(AuditEventTypes.BudgetUtilizationDeleted, orgId, actorUserId,
                metadata: new Dictionary<string, object?>
                {
                    ["budgetId"] = budgetId,
                    ["utilizationId"] = id,
                    ["deleteType"] = "hard",
                    ["amountUtilized"] = utilization.AmountUtilized,
                });
        }

        await db.SaveChangesAsync(ct);
    }

    private (Guid organisationId, Guid actorUserId) ResolveActorContext()
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var orgIdClaim = user.FindFirst("OrganisationId")?.Value;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
            throw new UnauthorizedAccessException("OrganisationId claim is missing or invalid.");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User ID claim is missing or invalid.");

        return (orgId, userId);
    }
}
