namespace MidiKaval.Api.Domain.Entities;

public sealed class BudgetUtilization
{
    public Guid Id { get; set; }
    public Guid BudgetLineItemId { get; set; }
    public Guid? CaseId { get; set; }
    public decimal AmountUtilized { get; set; }
    public DateOnly UtilizationDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public BudgetLineItem BudgetLineItem { get; set; } = null!;
    public Case? Case { get; set; }
}
