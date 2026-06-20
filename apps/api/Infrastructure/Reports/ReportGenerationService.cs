using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MidiKaval.Api.Infrastructure.Reports;

public sealed class ReportGenerationService(
    AppDbContext db,
    ILogger<ReportGenerationService> logger)
{
    public async Task<IReadOnlyList<ReportExportRowDto>> BuildReportAsync(
        ReportType type,
        Guid organisationId,
        DateOnly? from,
        DateOnly? to,
        int? year,
        CancellationToken ct = default)
    {
        return type switch
        {
            ReportType.DailyWork => await BuildDailyWorkReportAsync(organisationId, from ?? DateOnly.FromDateTime(DateTime.UtcNow), ct),
            ReportType.YearlyWork => await BuildYearlyWorkReportAsync(organisationId, year ?? DateTime.UtcNow.Year, ct),
            ReportType.VisitsPlannedVsCompleted => await BuildVisitsPlannedVsCompletedReportAsync(organisationId, from, to, ct),
            ReportType.Interventions => await BuildInterventionsReportAsync(organisationId, from, to, ct),
            ReportType.CourtSummary => await BuildCourtSummaryReportAsync(organisationId, from, to, ct),
            ReportType.OffenceAreaCounts => await BuildOffenceAreaCountsReportAsync(organisationId, ct),
            ReportType.WorkloadDistribution => await BuildWorkloadDistributionReportAsync(organisationId, ct),
            ReportType.TravelTotals => await BuildTravelTotalsReportAsync(organisationId, from, to, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public async Task<byte[]> GenerateFileAsync(
        IReadOnlyList<ReportExportRowDto> rows,
        string format,
        string title)
    {
        if (rows.Count == 0)
        {
            rows = [new ReportExportRowDto
            {
                Columns = new Dictionary<string, object?>
                {
                    ["Message"] = "No data available for the selected period"
                }
            }];
        }

        return format.ToLowerInvariant() switch
        {
            "excel" => GenerateExcel(rows, title),
            "pdf" => GeneratePdf(rows, title),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    public string ReportTypeToDisplayName(ReportType type) => type.ToDisplayName();

    /// <summary>
    /// Daily Work — Cases with visits scheduled/completed today, grouped by worker.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildDailyWorkReportAsync(
        Guid organisationId, DateOnly date, CancellationToken ct)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rows = await (
            from v in db.Visits
            join c in db.Cases on v.CaseId equals c.Id
            join u in db.Users on v.AssigneeUserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where v.OrganisationId == organisationId
                && v.ScheduledAtUtc >= dayStart
                && v.ScheduledAtUtc <= dayEnd
            select new
            {
                WorkerName = u != null && u.OrganisationId == organisationId ? u.Email : "Unknown",
                c.CrimeNumber,
                c.StNumber,
                VisitStatus = v.Status.ToString(),
                ScheduledAt = v.ScheduledAtUtc,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.WorkerName)
            .SelectMany(g => g.Select(r => new ReportExportRowDto
            {
                Columns = new Dictionary<string, object?>
                {
                    ["Worker"] = g.Key,
                    ["Crime Number"] = r.CrimeNumber,
                    ["ST Number"] = r.StNumber,
                    ["Visit Status"] = r.VisitStatus,
                    ["Scheduled At (UTC)"] = r.ScheduledAt.ToString("O"),
                }
            }))
            .ToList();
    }

    /// <summary>
    /// Yearly Work — Aggregate case/work stats for a given year.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildYearlyWorkReportAsync(
        Guid organisationId, int year, CancellationToken ct)
    {
        if (year < 1) year = DateTime.UtcNow.Year;
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var casesStarted = await db.Cases
            .CountAsync(c => c.OrganisationId == organisationId
                && c.CreatedAtUtc >= yearStart
                && c.CreatedAtUtc <= yearEnd, ct);

        var visitsCompleted = await db.Visits
            .CountAsync(v => v.OrganisationId == organisationId
                && v.Status == Domain.Enums.VisitStatus.Completed
                && v.CompletedAtUtc >= yearStart
                && v.CompletedAtUtc <= yearEnd, ct);

        var interventionsCompleted = await db.Interventions
            .CountAsync(i => i.OrganisationId == organisationId
                && i.Status == InterventionStatus.Completed
                && i.ProvidedAtUtc >= yearStart
                && i.ProvidedAtUtc <= yearEnd, ct);

        var stageCounts = await db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .GroupBy(c => c.CurrentStage)
            .Select(g => new { Stage = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var rows = new List<ReportExportRowDto>
        {
            new() { Columns = new Dictionary<string, object?> { ["Metric"] = "Cases Started", ["Value"] = casesStarted } },
            new() { Columns = new Dictionary<string, object?> { ["Metric"] = "Visits Completed", ["Value"] = visitsCompleted } },
            new() { Columns = new Dictionary<string, object?> { ["Metric"] = "Interventions Completed", ["Value"] = interventionsCompleted } },
        };

        rows.AddRange(stageCounts.Select(s => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Metric"] = $"Cases in Stage: {s.Stage}",
                ["Value"] = s.Count,
            }
        }));

        return rows;
    }

    /// <summary>
    /// Visits Planned vs Completed — Per-worker visit counts for a date range.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildVisitsPlannedVsCompletedReportAsync(
        Guid organisationId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rangeStart = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var visitData = await (
            from v in db.Visits
            join u in db.Users on v.AssigneeUserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where v.OrganisationId == organisationId
                && v.ScheduledAtUtc >= rangeStart
                && v.ScheduledAtUtc <= rangeEnd
            select new
            {
                WorkerName = u != null && u.OrganisationId == organisationId ? u.Email : "Unknown",
                IsCompleted = v.Status == Domain.Enums.VisitStatus.Completed,
            })
            .ToListAsync(ct);

        return visitData
            .GroupBy(v => v.WorkerName)
            .Select(g => new ReportExportRowDto
            {
                Columns = new Dictionary<string, object?>
                {
                    ["Worker"] = g.Key,
                    ["Planned"] = g.Count(),
                    ["Completed"] = g.Count(v => v.IsCompleted),
                }
            })
            .ToList();
    }

    /// <summary>
    /// Interventions — Interventions by status/outcome/worker for a date range.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildInterventionsReportAsync(
        Guid organisationId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rangeStart = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rawRows = await (
            from i in db.Interventions
            join c in db.Cases on i.CaseId equals c.Id
            join u in db.Users on i.AssignedStaffUserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where i.OrganisationId == organisationId
                && i.CreatedAtUtc >= rangeStart
                && i.CreatedAtUtc <= rangeEnd
            select new
            {
                WorkerEmail = u != null && u.OrganisationId == organisationId ? u.Email : (string?)null,
                CrimeNumber = c.CrimeNumber,
                CategoryName = i.CategoryName,
                Status = i.Status,
                Priority = i.Priority,
                DueAtUtc = i.DueAtUtc,
                ProvidedAtUtc = i.ProvidedAtUtc,
            })
            .ToListAsync(ct);

        var rows = rawRows.Select(r => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Worker"] = r.WorkerEmail ?? "Unknown",
                ["Case"] = r.CrimeNumber,
                ["Category"] = r.CategoryName,
                ["Status"] = r.Status.ToString(),
                ["Priority"] = r.Priority.ToString(),
                ["Due (UTC)"] = r.DueAtUtc?.ToString("O") ?? string.Empty,
                ["Provided (UTC)"] = r.ProvidedAtUtc?.ToString("O") ?? string.Empty,
            }
        }).ToList();

        return rows;
    }

    /// <summary>
    /// Court Summary — Court sittings by status/outcome for a date range.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildCourtSummaryReportAsync(
        Guid organisationId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rangeStart = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rawRows = await (
            from cs in db.CourtSittings
            join c in db.Cases on cs.CaseId equals c.Id
            where cs.OrganisationId == organisationId
                && cs.ScheduledAtUtc >= rangeStart
                && cs.ScheduledAtUtc <= rangeEnd
            select new
            {
                CrimeNumber = c.CrimeNumber,
                CourtName = cs.CourtName,
                Purpose = cs.Purpose,
                Status = cs.Status,
                ScheduledAtUtc = cs.ScheduledAtUtc,
                Outcome = cs.Outcome,
            })
            .ToListAsync(ct);

        var rows = rawRows.Select(r => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Case"] = r.CrimeNumber,
                ["Court"] = r.CourtName,
                ["Purpose"] = r.Purpose,
                ["Status"] = r.Status.ToString(),
                ["Scheduled (UTC)"] = r.ScheduledAtUtc.ToString("O"),
                ["Outcome"] = r.Outcome ?? string.Empty,
            }
        }).ToList();

        return rows;
    }

    /// <summary>
    /// Offence Area Counts — Case counts grouped by offence classification and domicile.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildOffenceAreaCountsReportAsync(
        Guid organisationId, CancellationToken ct)
    {
        var rawRows = await db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .GroupBy(c => new { c.OffenceClassification, c.Domicile })
            .Select(g => new
            {
                OffenceClassification = g.Key.OffenceClassification,
                Domicile = g.Key.Domicile,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var rows = rawRows.Select(r => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Offence Classification"] = r.OffenceClassification.ToString(),
                ["Domicile"] = r.Domicile.ToString(),
                ["Case Count"] = r.Count,
            }
        }).ToList();

        return rows;
    }

    /// <summary>
    /// Workload Distribution — Case counts per worker (distribution only, NOT performance scored).
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildWorkloadDistributionReportAsync(
        Guid organisationId, CancellationToken ct)
    {
        var rawRows = await (
            from c in db.Cases
            join u in db.Users on c.AssignedWorkerId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where c.OrganisationId == organisationId
                && c.CurrentStage != CaseStage.TerminationExclusion
                && c.AssignedWorkerId != null
            group c by new
            {
                WorkerId = c.AssignedWorkerId!.Value,
                WorkerName = u != null && u.OrganisationId == organisationId
                    ? u.Email
                    : "Unknown",
            } into g
            select new
            {
                WorkerName = g.Key.WorkerName,
                ActiveCaseCount = g.Count(),
            })
            .ToListAsync(ct);

        var rows = rawRows.Select(r => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Worker"] = r.WorkerName,
                ["Active Case Count"] = r.ActiveCaseCount,
            }
        }).ToList();

        return rows;
    }

    /// <summary>
    /// Travel Totals — Travel claim totals by worker for a date range.
    /// </summary>
    private async Task<IReadOnlyList<ReportExportRowDto>> BuildTravelTotalsReportAsync(
        Guid organisationId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rangeStart = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rawRows = await (
            from tc in db.TravelClaims
            join u in db.Users on tc.ClaimantUserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where tc.OrganisationId == organisationId
                && tc.ClaimDate >= rangeStart
                && tc.ClaimDate <= rangeEnd
            group new { tc, u } by new
            {
                WorkerId = tc.ClaimantUserId,
                WorkerName = u != null && u.OrganisationId == organisationId
                    ? u.Email
                    : "Unknown",
            } into g
            select new
            {
                WorkerName = g.Key.WorkerName,
                TotalClaims = g.Count(),
                TotalAmount = g.Sum(x => (decimal?)x.tc.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        var rows = rawRows.Select(r => new ReportExportRowDto
        {
            Columns = new Dictionary<string, object?>
            {
                ["Worker"] = r.WorkerName,
                ["Total Claims"] = r.TotalClaims,
                ["Total Amount"] = r.TotalAmount,
            }
        }).ToList();

        return rows;
    }

    private static byte[] GenerateExcel(IReadOnlyList<ReportExportRowDto> rows, string title)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(title.Length > 31 ? title[..31] : title);

        var headers = rows[0].Columns.Keys.ToArray();
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var excelRow = rowIndex + 2;
            for (var column = 0; column < headers.Length; column++)
            {
                var value = row.Columns.GetValueOrDefault(headers[column]);
                worksheet.Cell(excelRow, column + 1).Value = ConvertCellValue(value);
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GeneratePdf(IReadOnlyList<ReportExportRowDto> rows, string title)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Text(title).SemiBold().FontSize(12);

                page.Content().Table(table =>
                {
                    var headers = rows[0].Columns.Keys.ToArray();

                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(h).SemiBold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        foreach (var header in headers)
                        {
                            var value = row.Columns.GetValueOrDefault(header);
                            table.Cell().Padding(2).Text(FormatCellValue(value));
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    private static XLCellValue ConvertCellValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        int i => i,
        long l => l,
        decimal d => d,
        double dbl => dbl,
        bool b => b,
        DateTime dt => dt.ToString("O"),
        _ => value.ToString() ?? string.Empty,
    };

    private static string FormatCellValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        decimal d => d.ToString("N2"),
        _ => value.ToString() ?? string.Empty,
    };
}
