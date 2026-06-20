namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseAssignment
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid? FromWorkerId { get; set; }
    public Guid ToWorkerId { get; set; }
    public string PriorActions { get; set; } = string.Empty;
    public string OpenItems { get; set; } = string.Empty;
    public string NextVisitPurpose { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
