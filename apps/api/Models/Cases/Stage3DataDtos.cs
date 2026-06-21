using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class Stage3SupportDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string SupportType { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? Notes { get; set; }
    public bool ProvidedStatus { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Stage3SupportItemRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(50)]
    public string SupportType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ProviderName { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool ProvidedStatus { get; set; }
}

public sealed class UpsertStage3SupportsRequest
{
    [Required]
    [MinLength(1)]
    public List<Stage3SupportItemRequest> Items { get; set; } = [];
}
