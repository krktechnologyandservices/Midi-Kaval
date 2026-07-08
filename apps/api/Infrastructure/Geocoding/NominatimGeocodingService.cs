using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MidiKaval.Api.Models.Geocoding;

namespace MidiKaval.Api.Infrastructure.Geocoding;

public interface IGeocodingService
{
    Task<IReadOnlyList<GeocodingResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

// Proxies address search to OpenStreetMap's free Nominatim API. Nominatim's usage
// policy requires a real identifying User-Agent (set at HttpClient registration) and
// caps requests at 1/sec — browsers can't set a custom User-Agent header at all, so
// this can't be called directly from the Angular app; it must go through us.
public sealed class NominatimGeocodingService(HttpClient httpClient) : IGeocodingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim ThrottleLock = new(1, 1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1100);
    private static DateTime lastRequestUtc = DateTime.MinValue;

    public async Task<IReadOnlyList<GeocodingResultDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 3)
        {
            return Array.Empty<GeocodingResultDto>();
        }

        await ThrottleLock.WaitAsync(cancellationToken);
        try
        {
            var elapsed = DateTime.UtcNow - lastRequestUtc;
            if (elapsed < MinInterval)
            {
                await Task.Delay(MinInterval - elapsed, cancellationToken);
            }

            var url = $"search?format=jsonv2&q={Uri.EscapeDataString(trimmed)}&limit=5&addressdetails=0";

            List<NominatimResult>? rows;
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return Array.Empty<GeocodingResultDto>();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                rows = await JsonSerializer.DeserializeAsync<List<NominatimResult>>(
                    stream,
                    JsonOptions,
                    cancellationToken);
            }
            finally
            {
                lastRequestUtc = DateTime.UtcNow;
            }

            if (rows is null)
            {
                return Array.Empty<GeocodingResultDto>();
            }

            var results = new List<GeocodingResultDto>();
            foreach (var row in rows)
            {
                if (row.Lat is null || row.Lon is null)
                {
                    continue;
                }

                if (!decimal.TryParse(row.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                    || !decimal.TryParse(row.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                {
                    continue;
                }

                results.Add(new GeocodingResultDto
                {
                    DisplayName = row.DisplayName ?? string.Empty,
                    Latitude = latitude,
                    Longitude = longitude,
                    OsmReference = row.OsmType is not null && row.OsmId is not null
                        ? $"{row.OsmType}/{row.OsmId}"
                        : null,
                });
            }

            return results;
        }
        finally
        {
            ThrottleLock.Release();
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        public string? Lat { get; set; }

        public string? Lon { get; set; }

        [JsonPropertyName("osm_type")]
        public string? OsmType { get; set; }

        [JsonPropertyName("osm_id")]
        public long? OsmId { get; set; }
    }
}
