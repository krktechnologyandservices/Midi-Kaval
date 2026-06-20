namespace MidiKaval.Api.Domain.Entities;

public sealed class VisitNote
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid VisitId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
