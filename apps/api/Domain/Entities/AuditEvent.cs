using System.ComponentModel.DataAnnotations.Schema;

namespace MidiKaval.Api.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public User? ActorUser { get; set; }

    [ForeignKey(nameof(SubjectUserId))]
    public User? SubjectUser { get; set; }
}
