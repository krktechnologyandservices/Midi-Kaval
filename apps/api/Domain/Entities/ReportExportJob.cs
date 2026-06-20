namespace MidiKaval.Api.Domain.Entities;

public sealed class ReportExportJob
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = ReportExportJobStatus.Pending;
    public string? BlobPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? Year { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public static class ReportExportJobStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
