namespace MidiKaval.Api.Domain.Entities;

public sealed class VisitPlace
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid VisitId { get; set; }

    // Populated from the OpenStreetMap/Nominatim address search selection made at
    // scheduling time.
    public string Address { get; set; } = string.Empty;
    public string? OsmReference { get; set; }
    public decimal? PlannedLatitude { get; set; }
    public decimal? PlannedLongitude { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Populated when the field worker actually arrives and logs their real position —
    // deliberately separate from Planned* above, since the two can legitimately differ.
    public decimal? LoggedLatitude { get; set; }
    public decimal? LoggedLongitude { get; set; }
    public DateTime? LoggedAtUtc { get; set; }
    public Guid? LoggedByUserId { get; set; }
}
