using System.Text.Json;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Audit;

public sealed class AuditService(AppDbContext db) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(
        string eventType,
        Guid organisationId,
        Guid? actorUserId = null,
        Guid? subjectUserId = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = subjectUserId,
            EventType = eventType,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(cancellationToken);
    }
}
