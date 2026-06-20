using MidiKaval.Api.Models.Attachments;

namespace MidiKaval.Api.Models.TravelClaims;

public sealed class ApproveTravelClaimRequest
{
    public string? Comment { get; set; }
}

public sealed class ReturnTravelClaimRequest
{
    public string? Comment { get; set; }
}

public sealed class CreateTravelClaimRequest
{
    public DateTime? ClaimDate { get; set; }
    public string? StartLocation { get; set; }
    public string? Destination { get; set; }
    public string? TransportMode { get; set; }
    public decimal? Amount { get; set; }
    public string? AutoNumber { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<Guid>? CaseIds { get; set; }
}

public sealed class UpdateTravelClaimRequest
{
    public DateTime? ClaimDate { get; set; }
    public string? StartLocation { get; set; }
    public string? Destination { get; set; }
    public string? TransportMode { get; set; }
    public decimal? Amount { get; set; }
    public string? AutoNumber { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<Guid>? CaseIds { get; set; }
}

public sealed class TravelClaimDto
{
    public Guid Id { get; set; }
    public DateTime ClaimDate { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string TransportMode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? AutoNumber { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid ClaimantUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? ClaimantEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<Guid> CaseIds { get; set; } = [];
    public IReadOnlyList<AttachmentSummaryDto> Attachments { get; set; } = [];
}

public sealed class TravelClaimListResultDto
{
    public IReadOnlyList<TravelClaimDto> Items { get; set; } = [];
}

public sealed class TravelClaimMonthlyTotalDto
{
    public Guid StaffUserId { get; set; }
    public string StaffEmail { get; set; } = string.Empty;
    public int ClaimCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class TravelClaimMonthlyTotalsResultDto
{
    public IReadOnlyList<TravelClaimMonthlyTotalDto> Items { get; set; } = [];
}
