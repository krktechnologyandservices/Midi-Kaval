namespace MidiKaval.Api.Domain.Entities;

public sealed class SyncMutation
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; }
    public Guid ClientMutationId { get; set; }
    public string MutationType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ServerMessage { get; set; }
    public Guid? ResultVisitId { get; set; }
    public Guid? ResultTravelClaimId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
