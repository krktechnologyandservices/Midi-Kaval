using System.Text.Json.Serialization;

namespace MidiKaval.Api.Models.Audit;

public sealed record TargetUserSnapshotDto(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role);
