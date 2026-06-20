using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStageTransition
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }
    public CaseStage FromStage { get; set; }
    public CaseStage ToStage { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
