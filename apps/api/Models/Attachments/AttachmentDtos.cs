namespace MidiKaval.Api.Models.Attachments;

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
}

public sealed class AttachmentSummaryDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
}

public sealed class AttachmentContentDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
}
