using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage4Placement
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public PlacementType PlacementType { get; set; }
    public string? InstitutionName { get; set; }
    public string? Address { get; set; }
    public DateOnly StartDate { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
