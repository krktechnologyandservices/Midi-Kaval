namespace MidiKaval.Api.Domain.Enums;

public enum ReportType
{
    DailyWork,
    YearlyWork,
    VisitsPlannedVsCompleted,
    Interventions,
    CourtSummary,
    OffenceAreaCounts,
    WorkloadDistribution,
    TravelTotals,
}

public static class ReportTypeExtensions
{
    public static string ToApiString(this ReportType type) => type switch
    {
        ReportType.DailyWork => "daily-work",
        ReportType.YearlyWork => "yearly-work",
        ReportType.VisitsPlannedVsCompleted => "visits-planned-vs-completed",
        ReportType.Interventions => "interventions",
        ReportType.CourtSummary => "court-summary",
        ReportType.OffenceAreaCounts => "offence-area-counts",
        ReportType.WorkloadDistribution => "workload-distribution",
        ReportType.TravelTotals => "travel-totals",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static ReportType? FromApiString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
    {
        "daily-work" => ReportType.DailyWork,
        "yearly-work" => ReportType.YearlyWork,
        "visits-planned-vs-completed" => ReportType.VisitsPlannedVsCompleted,
        "interventions" => ReportType.Interventions,
        "court-summary" => ReportType.CourtSummary,
        "offence-area-counts" => ReportType.OffenceAreaCounts,
        "workload-distribution" => ReportType.WorkloadDistribution,
        "travel-totals" => ReportType.TravelTotals,
        _ => null,
        };
    }

    public static string ToDisplayName(this ReportType type) => type switch
    {
        ReportType.DailyWork => "Daily Work",
        ReportType.YearlyWork => "Yearly Work",
        ReportType.VisitsPlannedVsCompleted => "Visits Planned vs Completed",
        ReportType.Interventions => "Interventions",
        ReportType.CourtSummary => "Court Summary",
        ReportType.OffenceAreaCounts => "Offence Area Counts",
        ReportType.WorkloadDistribution => "Workload Distribution",
        ReportType.TravelTotals => "Travel Totals",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static IReadOnlyList<string> All { get; } =
    [
        "daily-work",
        "yearly-work",
        "visits-planned-vs-completed",
        "interventions",
        "court-summary",
        "offence-area-counts",
        "workload-distribution",
        "travel-totals",
    ];
}
