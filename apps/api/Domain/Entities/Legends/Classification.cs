namespace MidiKaval.Api.Domain.Entities.Legends;

public sealed class Classification
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
