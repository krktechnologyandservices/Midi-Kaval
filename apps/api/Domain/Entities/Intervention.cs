using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class Intervention
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid CaseId { get; set; }
    public InterventionDirection Direction { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InterventionPriority Priority { get; set; }
    public InterventionStatus Status { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? ProvidedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public Guid AssignedStaffUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? OverdueNotifiedAtUtc { get; set; }
}
