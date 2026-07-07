using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class BudgetLineItem
{
    public Guid Id { get; set; }
    public Guid ProjectBudgetId { get; set; }
    public BudgetHead BudgetHead { get; set; }
    public decimal AmountAllocated { get; set; }
    public decimal AmountUtilized { get; set; }
    public DateTime? ThresholdNotifiedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
