namespace MidiKaval.Api.Models.Cases;

public sealed class CaseExportRowDto
{
    public required string CrimeNumber { get; init; }
    public required string StNumber { get; init; }
    public required string BeneficiaryName { get; init; }
    public required string CurrentStage { get; init; }
    public required string TypeOfOffence { get; init; }
    public required string OffenceClassification { get; init; }
    public required string Domicile { get; init; }
    public string? Gender { get; init; }
    public string? FamilyType { get; init; }
    public string? EconomicStatus { get; init; }
    public int VisitCount { get; init; }
    public DateTime? NextVisitDueAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
