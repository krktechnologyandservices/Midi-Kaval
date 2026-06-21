namespace MidiKaval.Api.Models.Cases;

public sealed class CaseSearchQuery
{
    public string? Q { get; init; }
    public string? CurrentStage { get; init; }
    public string? TypeOfOffence { get; init; }
    public string? OffenceClassification { get; init; }
    public string? Domicile { get; init; }
    public string? Gender { get; init; }
    public string? FamilyType { get; init; }
    public string? EconomicStatus { get; init; }
    public Guid? OccupationId { get; init; }
    public Guid? EducationLevelId { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public Guid? AssignedWorkerUserId { get; init; }
    public bool? Overdue { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
