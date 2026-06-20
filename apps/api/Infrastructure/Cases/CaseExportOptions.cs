namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CaseExportOptions
{
    public const string SectionName = "CaseExport";

    public int MaxRows { get; init; } = 5000;
}
