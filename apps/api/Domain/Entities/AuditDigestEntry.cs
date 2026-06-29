namespace MidiKaval.Api.Domain.Entities;

public sealed class AuditDigestEntry
{
    public Guid Id { get; set; }
    public Guid AuditEventId { get; set; }
    public Guid OrganisationId { get; set; }
    public DateTime DigestSentAtUtc { get; set; }
    public Guid DigestBatchId { get; set; }

    public Organisation? Organisation { get; set; }
}
