using System.ComponentModel.DataAnnotations;
using MidiKaval.Api.Models.Attachments;

namespace MidiKaval.Api.Models.Budgets;

public sealed class BudgetUtilizationListDto
{
    public Guid Id { get; set; }
    public Guid BudgetLineItemId { get; set; }
    public string BudgetHead { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseCrimeNumber { get; set; }
    public decimal AmountUtilized { get; set; }
    public DateOnly UtilizationDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<AttachmentSummaryDto> Attachments { get; set; } = Array.Empty<AttachmentSummaryDto>();
}

public sealed class CreateBudgetUtilizationRequest
{
    [Required]
    public Guid BudgetLineItemId { get; set; }

    public Guid? CaseId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AmountUtilized { get; set; }

    [Required]
    public DateOnly UtilizationDate { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateBudgetUtilizationRequest
{
    public Guid? CaseId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AmountUtilized { get; set; }

    [Required]
    public DateOnly UtilizationDate { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}

public sealed class BudgetUtilizationSummaryDto
{
    public Guid BudgetId { get; set; }
    public List<BudgetHeadSummaryDto> HeadSummaries { get; set; } = [];
    public decimal TotalAllocated { get; set; }
    public decimal TotalUtilized { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal OverallUtilizationPercentage { get; set; }
}

public sealed class BudgetHeadSummaryDto
{
    public string BudgetHead { get; set; } = string.Empty;
    public decimal Allocated { get; set; }
    public decimal Utilized { get; set; }
    public decimal Balance { get; set; }
    public decimal UtilizationPercentage { get; set; }
}
