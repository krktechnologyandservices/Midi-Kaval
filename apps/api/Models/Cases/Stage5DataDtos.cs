using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage5ReintegrationDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string ReintegrationLevel { get; set; } = string.Empty;
    public string? InstitutionDetails { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage5ReintegrationRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string ReintegrationLevel { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? InstitutionDetails { get; set; }
}
