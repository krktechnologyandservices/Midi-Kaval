using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.Infrastructure.Sync;

public sealed record VisitSyncOutcome(
    string Status,
    string? ServerMessage,
    VisitListItemDto? Visit,
    Guid? VisitId = null)
{
    public static VisitSyncOutcome Applied(VisitListItemDto visit, Guid visitId) =>
        new(Models.Sync.SyncMutationStatuses.Applied, null, visit, visitId);

    public static VisitSyncOutcome Rejected(string message) =>
        new(Models.Sync.SyncMutationStatuses.Rejected, message, null);
}
