using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Cases;

public sealed class RelatedCaseDto
{
    public Guid CaseId { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
}

public sealed class LinkRelatedCaseRequest
{
    [Required(AllowEmptyStrings = false)]
    public Guid RelatedCaseId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string RelationshipType { get; set; } = string.Empty;
}
