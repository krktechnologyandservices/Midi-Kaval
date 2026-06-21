using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage6TerminationExclusion
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public TerminationExclusionType TerminationExclusionType { get; set; }
    public string? JjbDetails { get; set; }
    public string? ExclusionReason { get; set; }
    public Guid? ReportAttachmentId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
