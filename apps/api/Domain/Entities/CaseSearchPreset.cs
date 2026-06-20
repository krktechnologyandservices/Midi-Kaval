namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseSearchPreset
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
