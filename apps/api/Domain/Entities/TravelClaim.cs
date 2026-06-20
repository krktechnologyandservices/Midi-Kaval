using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class TravelClaim
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ClaimantUserId { get; set; }
    public DateTime ClaimDate { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public TransportMode TransportMode { get; set; }
    public decimal Amount { get; set; }
    public string? AutoNumber { get; set; }
    public string? Notes { get; set; }
    public TravelClaimStatus Status { get; set; } = TravelClaimStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
