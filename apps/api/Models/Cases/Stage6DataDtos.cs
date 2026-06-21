using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage6TerminationExclusionDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string TerminationExclusionType { get; set; } = string.Empty;
    public string? JjbDetails { get; set; }
    public string? ExclusionReason { get; set; }
    public Guid? ReportAttachmentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage6TerminationExclusionRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string TerminationExclusionType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? JjbDetails { get; set; }

    [MaxLength(2000)]
    public string? ExclusionReason { get; set; }

    public Guid? ReportAttachmentId { get; set; }
}
