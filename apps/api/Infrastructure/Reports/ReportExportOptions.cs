namespace MidiKaval.Api.Infrastructure.Reports;

public sealed class ReportExportOptions
{
    public const string SectionName = "ReportExport";

    public int PollIntervalSeconds { get; init; } = 30;
    public int SasUrlExpiryMinutes { get; init; } = 15;
    public string BlobContainerPrefix { get; init; } = "reports";
}
