namespace MidiKaval.Api.Models.Cases;

public sealed class Stage2DataDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
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

public sealed class UpsertStage2DataRequest
{
    public string? BioPsychoSocialAssessment { get; set; }
    public string? IcpRecords { get; set; }
    public string? LifeSkillTraining { get; set; }
    public string? ParentManagement { get; set; }
    public string? GroupWork { get; set; }
    public string? CommunityProgramAttendance { get; set; }
    public string? PmaStatus { get; set; }
    public string? OverallProgress { get; set; }
}
