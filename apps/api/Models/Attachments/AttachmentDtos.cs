namespace MidiKaval.Api.Models.Attachments;

public sealed class AttachmentPresignRequest
{
    public string? ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
}

public sealed class AttachmentPresignResultDto
{
    public Guid AttachmentId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> RequiredHeaders { get; set; }
        = new Dictionary<string, string>();
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class AttachmentConfirmRequest
{
    public Guid AttachmentId { get; set; }
}

public sealed class AttachmentDto
{
    public Guid Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime DownloadExpiresAtUtc { get; set; }
}

public sealed class AttachmentSummaryDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
}

public sealed class AttachmentDownloadUrlDto
{
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime DownloadExpiresAtUtc { get; set; }
}
