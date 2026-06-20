namespace MidiKaval.Api.Models.Legends;

public record LegendDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
