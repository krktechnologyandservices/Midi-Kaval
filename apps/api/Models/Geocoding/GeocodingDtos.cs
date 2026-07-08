namespace MidiKaval.Api.Models.Geocoding;

public sealed class GeocodingResultDto
{
    public string DisplayName { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? OsmReference { get; set; }
}

public sealed class GeocodingSearchResultDto
{
    public IReadOnlyList<GeocodingResultDto> Items { get; set; } = Array.Empty<GeocodingResultDto>();
}
