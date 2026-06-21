using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Budgets;

public sealed class BudgetListDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateOnly FinancialYearStart { get; set; }
    public DateOnly FinancialYearEnd { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public decimal TotalAllocated { get; set; }
    public decimal TotalUtilized { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class BudgetDetailDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateOnly FinancialYearStart { get; set; }
    public DateOnly FinancialYearEnd { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<BudgetLineItemDto> LineItems { get; set; } = [];
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class BudgetLineItemDto
{
    public Guid Id { get; set; }
    public string BudgetHead { get; set; } = string.Empty;
    public decimal AmountAllocated { get; set; }
    public decimal AmountUtilized { get; set; }
}

public sealed class CreateBudgetRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(16)]
    public string Source { get; set; } = string.Empty;

    [Required]
    public DateOnly FinancialYearStart { get; set; }

    [Required]
    public DateOnly FinancialYearEnd { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateBudgetLineItemRequest> LineItems { get; set; } = [];
}

public sealed class CreateBudgetLineItemRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string BudgetHead { get; set; } = string.Empty;

    [Required]
    [Range(0, double.MaxValue)]
    public decimal AmountAllocated { get; set; }
}

public sealed class UpdateBudgetRequest
{
    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<UpdateBudgetLineItemRequest> LineItems { get; set; } = [];
}

public sealed class UpdateBudgetLineItemRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string BudgetHead { get; set; } = string.Empty;

    [Required]
    [Range(0, double.MaxValue)]
    public decimal AmountAllocated { get; set; }
}

public sealed class ApproveBudgetRequest
{
    [MaxLength(500)]
    public string? DecisionComment { get; set; }
}

public sealed class ReturnBudgetRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(500)]
    public string DecisionComment { get; set; } = string.Empty;
}

public sealed class BudgetReportDto
{
    public string Period { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public List<BudgetReportLineDto> Lines { get; set; } = [];
    public decimal TotalAllocated { get; set; }
    public decimal TotalUtilized { get; set; }
    public decimal TotalBalance { get; set; }
}

public sealed class BudgetReportLineDto
{
    public string BudgetHead { get; set; } = string.Empty;
    public decimal Allocated { get; set; }
    public decimal Utilized { get; set; }
    public decimal Balance { get; set; }
    public decimal UtilizationPercentage { get; set; }
}
