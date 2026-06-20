using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseNote
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AuthorUserId { get; set; }
    public CaseNoteType NoteType { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public bool ActionRequired { get; set; }
    public DateTime? ActionDueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
