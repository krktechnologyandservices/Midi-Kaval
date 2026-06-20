namespace MidiKaval.Api.Models.Cases;

public sealed class CaseSearchQuery
{
    public string? Q { get; init; }
    public string? CurrentStage { get; init; }
    public string? TypeOfOffence { get; init; }
    public string? OffenceClassification { get; init; }
    public string? Domicile { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public Guid? AssignedWorkerUserId { get; init; }
    public bool? Overdue { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
