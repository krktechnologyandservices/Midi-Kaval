namespace MidiKaval.Api.Models.Reports;

public sealed class ReportExportRequest
{
    public string Format { get; init; } = "excel";
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? Year { get; init; }
}

public sealed class ReportExportJobDto
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ReportType { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ReportExportStatusDto
{
    public string Status { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ReportExportListResultDto
{
    public IReadOnlyList<ReportExportJobDto> Items { get; init; } = [];
}
