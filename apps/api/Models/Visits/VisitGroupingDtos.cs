namespace MidiKaval.Api.Models.Visits;

public sealed class VisitGroupingSuggestionDto
{
    public IReadOnlyList<VisitGroupingClusterDto> Clusters { get; set; } = Array.Empty<VisitGroupingClusterDto>();
    public IReadOnlyList<Guid> SuggestedVisitOrder { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<VisitGroupingLegDto> Legs { get; set; } = Array.Empty<VisitGroupingLegDto>();
    public IReadOnlyList<VisitGroupingExcludedDto> Excluded { get; set; } = Array.Empty<VisitGroupingExcludedDto>();
    public int EligibleCount { get; set; }
    public int ExcludedCount { get; set; }
    public string? Message { get; set; }
}

public sealed class VisitGroupingClusterDto
{
    public int ClusterIndex { get; set; }
    public IReadOnlyList<Guid> VisitIds { get; set; } = Array.Empty<Guid>();
    public double? CentroidLatitude { get; set; }
    public double? CentroidLongitude { get; set; }
}

public sealed class VisitGroupingLegDto
{
    public Guid VisitId { get; set; }
    public double? DistanceKmFromPrevious { get; set; }
}

public sealed class VisitGroupingExcludedDto
{
    public Guid VisitId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
