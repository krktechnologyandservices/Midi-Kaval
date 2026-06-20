using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class Visit
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Scheduled;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastRescheduleReason { get; set; }
    public DateTime? RescheduledAtUtc { get; set; }
    public Guid? RescheduledByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
