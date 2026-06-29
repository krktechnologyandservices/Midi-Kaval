using MidiKaval.Api.Models.Audit;

namespace MidiKaval.Api.Infrastructure.Audit;

public interface IAuditService
{
    Task RecordAsync(
        string eventType,
        Guid organisationId,
        Guid? actorUserId = null,
        Guid? subjectUserId = null,
        TargetUserSnapshotDto? targetUserSnapshot = null,
        string? actorIpAddress = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);
}
