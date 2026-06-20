namespace MidiKaval.Api.Models.Supervisor;

public sealed class DashboardResultDto
{
    public IReadOnlyList<CasesByStageDto> CasesByStage { get; init; } = [];
    public IReadOnlyList<CasesByOffenceClassificationDto> CasesByOffenceClassification { get; init; } = [];
    public IReadOnlyList<CasesByDomicileDto> CasesByDomicile { get; init; } = [];
    public IReadOnlyList<CasesByStaffDto> CasesByStaff { get; init; } = [];
    public OverdueVisitsDto OverdueVisits { get; init; } = null!;
    public InterventionsGaugeDto InterventionsGauge { get; init; } = null!;
    public CourtThisWeekDto CourtThisWeek { get; init; } = null!;
    public PendingClaimsDto PendingClaims { get; init; } = null!;
    public IReadOnlyList<IntakeTrendPointDto> IntakeTrend { get; init; } = [];
}

public sealed class CasesByStageDto
{
    public string Stage { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class CasesByOffenceClassificationDto
{
    public string OffenceClassification { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class CasesByDomicileDto
{
    public string Domicile { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class CasesByStaffDto
{
    public string WorkerName { get; init; } = string.Empty;
    public Guid WorkerId { get; init; }
    public int CaseCount { get; init; }
}

public sealed class OverdueVisitsDto
{
    public int TotalOverdue { get; init; }
    public int UniqueCasesAffected { get; init; }
}

public sealed class InterventionsGaugeDto
{
    public int InProgress { get; init; }
    public int Overdue { get; init; }
    public int CompletedThisMonth { get; init; }
}

public sealed class CourtThisWeekDto
{
    public int TotalUpcoming { get; init; }
    public int AttendedSoFar { get; init; }
    public int TotalCasesWithSittings { get; init; }
}

public sealed class PendingClaimsDto
{
    public int PendingCount { get; init; }
    public decimal TotalAmountPending { get; init; }
    public int OldestPendingDays { get; init; }
}

public sealed class IntakeTrendPointDto
{
    public string Month { get; init; } = string.Empty;
    public int Count { get; init; }
}
