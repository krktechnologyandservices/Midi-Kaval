using MidiKaval.Api.Models.TravelClaims;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.Infrastructure.Sync;

public sealed record SyncApplyOutcome(
    string Status,
    string? ServerMessage,
    VisitListItemDto? Visit = null,
    Guid? VisitId = null,
    TravelClaimDto? TravelClaim = null,
    Guid? TravelClaimId = null)
{
    public static SyncApplyOutcome AppliedVisit(VisitListItemDto visit, Guid visitId) =>
        new(Models.Sync.SyncMutationStatuses.Applied, null, visit, visitId);

    public static SyncApplyOutcome AppliedTravelClaim(TravelClaimDto claim, Guid claimId) =>
        new(Models.Sync.SyncMutationStatuses.Applied, null, TravelClaim: claim, TravelClaimId: claimId);

    public static SyncApplyOutcome Rejected(string message) =>
        new(Models.Sync.SyncMutationStatuses.Rejected, message);
}
