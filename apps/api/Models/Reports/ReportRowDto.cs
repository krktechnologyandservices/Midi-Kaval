namespace MidiKaval.Api.Models.Reports;

public sealed class ReportExportRowDto
{
    public required IReadOnlyDictionary<string, object?> Columns { get; init; }
}
