using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseRelatedCase
{
    public Guid Id { get; set; }
    public Guid CaseIdA { get; set; }
    public Guid CaseIdB { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
