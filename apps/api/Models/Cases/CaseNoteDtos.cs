using MidiKaval.Api.Models.Attachments;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Models.Cases;

public sealed class CreateCaseNoteRequest
{
    public string? NoteType { get; set; }
    public string? BodyText { get; set; }
    public bool ActionRequired { get; set; }
    public DateTime? ActionDueAtUtc { get; set; }
}

public sealed class CaseNoteDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string NoteType { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public bool ActionRequired { get; set; }
    public DateTime? ActionDueAtUtc { get; set; }
    public Guid AuthorUserId { get; set; }
    public string? AuthorEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<AttachmentSummaryDto> Attachments { get; set; } = Array.Empty<AttachmentSummaryDto>();
}

public sealed class CaseNoteListResultDto
{
    public IReadOnlyList<CaseNoteDto> Items { get; set; } = Array.Empty<CaseNoteDto>();
}
