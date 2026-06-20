namespace MidiKaval.Api.Domain.Entities;

public sealed class TravelClaimCaseLink
{
    public Guid TravelClaimId { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }
}
