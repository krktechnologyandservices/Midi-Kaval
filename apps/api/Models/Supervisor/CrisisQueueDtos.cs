namespace MidiKaval.Api.Models.Supervisor;

public sealed class CrisisQueueListResultDto
{
    public IReadOnlyList<CrisisQueueItemDto> Items { get; init; } = [];
}

public sealed class CrisisQueueItemDto
{
    public string RowType { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string BadgeLabel { get; init; } = string.Empty;

    public Guid CaseId { get; init; }

    public Guid? CourtSittingId { get; init; }

    public Guid? TravelClaimId { get; init; }

    public Guid? AssignedWorkerUserId { get; init; }

    public Guid? ClaimantUserId { get; init; }

    public string? ClaimantEmail { get; init; }

    public decimal? Amount { get; init; }

    public int? ReceiptCount { get; init; }

    public string? CrimeNumber { get; init; }

    public string? StNumber { get; init; }

    public DateTime? ScheduledAtUtc { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    // New fields for Story 8.1 — overdue visits, court <48h, handoffs

    public Guid? VisitId { get; init; }

    public int? OverdueVisitCount { get; init; }

    public DateTime? VisitScheduledAtUtc { get; init; }

    public DateTime? TransferredAtUtc { get; init; }

    public string? PreviousWorkerName { get; init; }

    public string? CourtSittingStatus { get; init; }
}
