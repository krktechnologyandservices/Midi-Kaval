namespace MidiKaval.Api.Models.Cases;

public sealed class CreateCaseRequest
{
    public string? CrimeNumber { get; set; }
    public string? StNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public int? BeneficiaryAge { get; set; }
    public string? BeneficiaryContact { get; set; }
    public string? TypeOfOffence { get; set; }
    public string? OffenceClassification { get; set; }
    public string? Domicile { get; set; }
    public string? Gender { get; set; }
    public string? FamilyType { get; set; }
    public string? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public Guid? EducationLevelId { get; set; }
    public bool? FamilyHistoryOfCrime { get; set; }
    public int? RecidivismBeforeCount { get; set; }
    public int? RecidivismAfterCount { get; set; }
    public bool? IsFirstTimeOffender { get; set; }
    public string? SensitivityLevel { get; set; }
}

public sealed class TransitionCaseStageRequest
{
    public string? TargetStage { get; set; }
    public string? Notes { get; set; }
}

public sealed class TransferCaseRequest
{
    public Guid? AssigneeUserId { get; set; }
    public string? PriorActions { get; set; }
    public string? OpenItems { get; set; }
    public string? NextVisitPurpose { get; set; }
}

public sealed class HandoffWhisperDto
{
    public string PriorActions { get; set; } = string.Empty;
    public string OpenItems { get; set; } = string.Empty;
    public string NextVisitPurpose { get; set; } = string.Empty;
    public DateTime TransferredAtUtc { get; set; }
}

public sealed class CaseDetailDto
{
    public Guid Id { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string Domicile { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public Guid? AssignedWorkerUserId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? NextVisitDueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool GpsVerified { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Landmark { get; set; }
    public DateTime? GpsVerifiedAtUtc { get; set; }
    public Guid? GpsVerifiedByUserId { get; set; }
    public HandoffWhisperDto? HandoffWhisper { get; set; }
    public string SensitivityLevel { get; set; } = "Standard";
    public string? Gender { get; set; }
    public string? FamilyType { get; set; }
    public string? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public string? OccupationName { get; set; }
    public Guid? EducationLevelId { get; set; }
    public string? EducationLevelName { get; set; }
    public bool FamilyHistoryOfCrime { get; set; }
    public int? RecidivismBeforeCount { get; set; }
    public int? RecidivismAfterCount { get; set; }
    public IReadOnlyList<RelatedCaseDto> RelatedCases { get; set; } = Array.Empty<RelatedCaseDto>();
}

public sealed class FieldWorkerUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public sealed class CheckCaseDuplicateRequest
{
    public string? CrimeNumber { get; set; }
    public string? StNumber { get; set; }
}

public sealed class CaseDuplicateMatchDto
{
    public Guid CaseId { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string MatchedOn { get; set; } = string.Empty;
}

public sealed class CheckCaseDuplicateResultDto
{
    public bool HasMatch { get; set; }
    public IReadOnlyList<CaseDuplicateMatchDto> Matches { get; set; } = Array.Empty<CaseDuplicateMatchDto>();
}

public sealed class CaseDto
{
    public Guid Id { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Gender { get; set; }
    public string? FamilyType { get; set; }
    public string? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public string? OccupationName { get; set; }
    public Guid? EducationLevelId { get; set; }
    public string? EducationLevelName { get; set; }
    public bool FamilyHistoryOfCrime { get; set; }
    public int? RecidivismBeforeCount { get; set; }
    public int? RecidivismAfterCount { get; set; }
}

public sealed class CaseSummaryDto
{
    public Guid Id { get; set; }
    public string CrimeNumber { get; set; } = string.Empty;
    public string StNumber { get; set; } = string.Empty;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string TypeOfOffence { get; set; } = string.Empty;
    public string OffenceClassification { get; set; } = string.Empty;
    public string Domicile { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedWorkerUserId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? NextVisitDueAtUtc { get; set; }
    public bool GpsVerified { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Landmark { get; set; }
    public string SensitivityLevel { get; set; } = "Standard";
    public string? Gender { get; set; }
    public string? FamilyType { get; set; }
    public string? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public string? OccupationName { get; set; }
    public Guid? EducationLevelId { get; set; }
    public string? EducationLevelName { get; set; }
    public bool FamilyHistoryOfCrime { get; set; }
    public int? RecidivismBeforeCount { get; set; }
    public int? RecidivismAfterCount { get; set; }
}

public sealed class CaseSearchResultDto
{
    public IReadOnlyList<CaseSummaryDto> Items { get; set; } = Array.Empty<CaseSummaryDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class CaseSearchFiltersDto
{
    public string? Q { get; set; }
    public string? CurrentStage { get; set; }
    public string? TypeOfOffence { get; set; }
    public string? OffenceClassification { get; set; }
    public string? Domicile { get; set; }
    public string? Gender { get; set; }
    public string? FamilyType { get; set; }
    public string? EconomicStatus { get; set; }
    public Guid? OccupationId { get; set; }
    public Guid? EducationLevelId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? AssignedWorkerUserId { get; set; }
    public bool? Overdue { get; set; }
}

public sealed class CreateCaseSearchPresetRequest
{
    public string? Name { get; set; }
    public CaseSearchFiltersDto? Filters { get; set; }
}

public sealed class VerifyCaseGpsRequest
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Landmark { get; set; }
}

public sealed class CaseGpsDto
{
    public Guid CaseId { get; set; }
    public bool GpsVerified { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Landmark { get; set; }
    public DateTime? GpsVerifiedAtUtc { get; set; }
    public Guid? GpsVerifiedByUserId { get; set; }
}

public sealed class RevealCasePiiResponse
{
    public string BeneficiaryName { get; set; } = string.Empty;
    public int? BeneficiaryAge { get; set; }
    public string? BeneficiaryContact { get; set; }
}

public sealed class CaseSearchPresetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CaseSearchFiltersDto Filters { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
}
