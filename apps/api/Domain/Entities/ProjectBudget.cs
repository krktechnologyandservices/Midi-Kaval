using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class ProjectBudget
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public BudgetSource Source { get; set; }
    public DateOnly FinancialYearStart { get; set; }
    public DateOnly FinancialYearEnd { get; set; }
    public BudgetApprovalStatus ApprovalStatus { get; set; } = BudgetApprovalStatus.Draft;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<BudgetLineItem> BudgetLineItems { get; set; } = [];
}
