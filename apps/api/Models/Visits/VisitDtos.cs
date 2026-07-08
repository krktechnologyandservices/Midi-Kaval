using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Models.Visits;

public sealed class VisitListItemDto
{
    public Guid Id { get; set; }
    public Guid AssigneeUserId { get; set; }
    public string? AssigneeEmail { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletionNote { get; set; }
    public string? LastRescheduleReason { get; set; }
    public string? CancellationReason { get; set; }
    public HandoffWhisperDto? HandoffWhisper { get; set; }
    public IReadOnlyList<VisitPlaceDto> Places { get; set; } = Array.Empty<VisitPlaceDto>();
    public CaseSummaryDto Case { get; set; } = null!;
}

public sealed class VisitPlaceDto
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? OsmReference { get; set; }
    public decimal? PlannedLatitude { get; set; }
    public decimal? PlannedLongitude { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal? LoggedLatitude { get; set; }
    public decimal? LoggedLongitude { get; set; }
    public DateTime? LoggedAtUtc { get; set; }
    public string? LoggedByEmail { get; set; }
}

public sealed class AddVisitPlaceRequest
{
    public string? Address { get; set; }
    public string? OsmReference { get; set; }
    public decimal? PlannedLatitude { get; set; }
    public decimal? PlannedLongitude { get; set; }
}

public sealed class LogVisitPlaceRequest
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class VisitListResultDto
{
    public IReadOnlyList<VisitListItemDto> Items { get; set; } = Array.Empty<VisitListItemDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class ScheduleVisitRequest
{
    public DateTime? ScheduledAtUtc { get; set; }
    public Guid? AssigneeUserId { get; set; }
}

public sealed class RescheduleVisitRequest
{
    public DateTime? ScheduledAtUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class CompleteVisitRequest
{
    public string? Note { get; set; }
}

public sealed class CancelVisitRequest
{
    public string? Reason { get; set; }
}
