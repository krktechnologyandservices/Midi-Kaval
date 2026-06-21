namespace MidiKaval.Api.Domain.Entities;

public sealed class CaseStage2Data
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid OrganisationId { get; set; }

    public string? BioPsychoSocialAssessment { get; set; }
    public string? IcpRecords { get; set; }
    public string? LifeSkillTraining { get; set; }
    public string? ParentManagement { get; set; }
    public string? GroupWork { get; set; }
    public string? CommunityProgramAttendance { get; set; }
    public string? PmaStatus { get; set; }
    public string? OverallProgress { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
