using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CourtSitting
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid CaseId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string CourtName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public CourtSittingStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextCourtAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
    public DateTime? MissEscalatedAtUtc { get; set; }
}
