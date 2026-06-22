namespace MidiKaval.Api.Models.Reports;

public sealed record SocioDemographicProfileDto
{
    public int Month { get; init; }
    public int Year { get; init; }
    public IReadOnlyList<ChildListItemDto> Children { get; init; } = [];
    public IReadOnlyList<CrossTabulationSectionDto> CrossTabulation { get; init; } = [];
}

public sealed record ChildListItemDto
{
    public int SlNo { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Age { get; init; }
    public string? Contact { get; init; }
    public DateTime CaseCommittedDate { get; init; }
    public string CrimeNumber { get; init; } = string.Empty;
    public string StNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PresentStage { get; init; } = string.Empty;
}

public sealed record CrossTabulationSectionDto
{
    public string DimensionName { get; init; } = string.Empty;
    public IReadOnlyList<CrossTabulationCategoryDto> Categories { get; init; } = [];
}

public sealed record CrossTabulationCategoryDto
{
    public string CategoryName { get; init; } = string.Empty;
    public int Count { get; init; }
}
