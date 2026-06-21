namespace MidiKaval.Api.Domain.Entities;

using MidiKaval.Api.Domain.Enums;

public sealed class CaseStage3Support
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public SupportType SupportType { get; set; }
    public string? ProviderName { get; set; }
    public string? Notes { get; set; }
    public bool ProvidedStatus { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
