namespace MidiKaval.Api.Models.Cases;

public sealed class CreateInterventionRequest
{
    public string? Direction { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? ProvidedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public Guid? AssignedStaffUserId { get; set; }
}

public sealed class UpdateInterventionRequest
{
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? ProvidedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public Guid? AssignedStaffUserId { get; set; }
}

public sealed class InterventionDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DueAtUtc { get; set; }
    public DateTime? ProvidedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public Guid AssignedStaffUserId { get; set; }
    public string? AssignedStaffEmail { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class InterventionListResultDto
{
    public IReadOnlyList<InterventionDto> Items { get; set; } = Array.Empty<InterventionDto>();
}
