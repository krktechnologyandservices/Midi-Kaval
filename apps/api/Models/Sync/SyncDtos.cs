using System.Text.Json;
using MidiKaval.Api.Models.TravelClaims;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.Models.Sync;

public static class SyncMutationTypes
{
    public const string VisitStart = "visit.start";
    public const string VisitComplete = "visit.complete";
    public const string TravelClaimCreate = "travel.claim.create";
}

public static class SyncMutationStatuses
{
    public const string Applied = "applied";
    public const string Rejected = "rejected";
    public const string Duplicate = "duplicate";
}

public sealed class SyncPushRequest
{
    public IReadOnlyList<SyncMutationDto> Mutations { get; set; } = Array.Empty<SyncMutationDto>();
}

public sealed class SyncMutationDto
{
    public Guid ClientMutationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime ClientTimestampUtc { get; set; }
    public JsonElement Payload { get; set; }
}

public sealed class VisitStartSyncPayload
{
    public Guid VisitId { get; set; }
}

public sealed class VisitCompleteSyncPayload
{
    public Guid VisitId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime NoteClientTimestampUtc { get; set; }
}

public sealed class TravelClaimCreateSyncPayload
{
    public DateTime? ClaimDate { get; set; }
    public string? StartLocation { get; set; }
    public string? Destination { get; set; }
    public string? TransportMode { get; set; }
    public decimal? Amount { get; set; }
    public string? AutoNumber { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<Guid>? CaseIds { get; set; }
}

public sealed class SyncPushResultDto
{
    public IReadOnlyList<SyncMutationResultDto> Results { get; set; } = Array.Empty<SyncMutationResultDto>();
}

public sealed class SyncMutationResultDto
{
    public Guid ClientMutationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ServerMessage { get; set; }
    public VisitListItemDto? Visit { get; set; }
    public TravelClaimDto? TravelClaim { get; set; }
}
