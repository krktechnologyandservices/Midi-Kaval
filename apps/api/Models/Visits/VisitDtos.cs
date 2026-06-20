using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Models.Visits;

public sealed class VisitListItemDto
{
    public Guid Id { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletionNote { get; set; }
    public string? LastRescheduleReason { get; set; }
    public HandoffWhisperDto? HandoffWhisper { get; set; }
    public CaseSummaryDto Case { get; set; } = null!;
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
