using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class Attachment
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public AttachmentResourceType ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public string BlobName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public AttachmentStatus Status { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}
