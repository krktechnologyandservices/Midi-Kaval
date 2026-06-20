namespace MidiKaval.Api.Models;

public sealed class ApiMeta
{
    public string RequestId { get; init; } = string.Empty;

    public int? TotalCount { get; init; }
}
