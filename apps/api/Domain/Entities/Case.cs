using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class Case
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public int? BeneficiaryAge { get; set; }
    public string? BeneficiaryContact { get; set; }
    public string TypeOfOffence { get; set; } = string.Empty;
    public OffenceClassification OffenceClassification { get; set; }
    public Domicile Domicile { get; set; }
    public bool IsFirstTimeOffender { get; set; } = true;
    public CaseStage CurrentStage { get; set; } = CaseStage.ProcessInitiation;
    public int VisitCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedWorkerId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? NextVisitDueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Landmark { get; set; }
    public bool GpsVerified { get; set; }
    public DateTime? GpsVerifiedAtUtc { get; set; }
    public Guid? GpsVerifiedByUserId { get; set; }
    public SensitivityLevel SensitivityLevel { get; set; } = SensitivityLevel.Standard;
    public DateTime? CourtMissFlaggedAtUtc { get; set; }
}
