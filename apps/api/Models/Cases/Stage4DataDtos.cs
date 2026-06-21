using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage4PlacementDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string PlacementType { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string? Address { get; set; }
    public DateOnly StartDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertStage4PlacementRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string PlacementType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? InstitutionName { get; set; }

    [MaxLength(2000)]
    public string? Address { get; set; }

    public DateOnly StartDate { get; set; }
}
