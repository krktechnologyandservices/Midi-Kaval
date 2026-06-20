namespace MidiKaval.Api.Models.Cases;

public sealed class CreateCourtSittingRequest
{
    public DateTime? ScheduledAtUtc { get; set; }
    public string? CourtName { get; set; }
    public string? Purpose { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
}

public sealed class UpdateCourtSittingRequest
{
    public string? Status { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public string? CourtName { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextCourtAtUtc { get; set; }
}

public sealed class CourtSittingDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string CourtName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextCourtAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CourtSittingListResultDto
{
    public IReadOnlyList<CourtSittingDto> Items { get; set; } = Array.Empty<CourtSittingDto>();
}

public sealed class CourtSittingScheduleItemDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string CourtName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextCourtAtUtc { get; set; }
    public bool IsPastDue { get; set; }
    public CaseSummaryDto Case { get; set; } = null!;
}

public sealed class CourtSittingUpcomingListResultDto
{
    public IReadOnlyList<CourtSittingScheduleItemDto> Items { get; set; } =
        Array.Empty<CourtSittingScheduleItemDto>();
}
