using ClosedXML.Excel;
using MidiKaval.Api.Models.Budgets;

namespace MidiKaval.Api.Infrastructure.Budgets;

public interface IBudgetReportExportService
{
    byte[] Generate(BudgetReportDto report);
}

public sealed class BudgetReportExcelService : IBudgetReportExportService
{
    public byte[] Generate(BudgetReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Budget Report");

        // Header
        ws.Cell(1, 1).Value = $"Budget Report — {report.Frequency} — {report.Period} (Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC})";
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        // Column headers
        var headers = new[] { "Budget Head", "Allocated (₹)", "Utilized (₹)", "Balance (₹)", "Utilization %" };
        for (var col = 0; col < headers.Length; col++)
        {
            ws.Cell(3, col + 1).Value = headers[col];
            ws.Cell(3, col + 1).Style.Font.Bold = true;
            ws.Cell(3, col + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE5E7EB);
        }

        // Freeze panes below column headers
        ws.SheetView.Freeze(4, 0);

        // Data rows
        var row = 4;
        foreach (var line in report.Lines)
        {
            ws.Cell(row, 1).Value = line.BudgetHead;
            ws.Cell(row, 2).Value = line.Allocated;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 3).Value = line.Utilized;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 4).Value = line.Balance;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = line.UtilizationPercentage / 100;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00%";
            row++;
        }

        var hasData = report.Lines.Count > 0;

        // Empty data handling — write message then save and return early (no summary)
        if (!hasData)
        {
            ws.Cell(row, 1).Value = "No data available for the selected period";
            ws.Range(row, 1, row, 5).Merge();
        }
        else
        {
            // Summary row
            row++; // blank row before summary
            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Cell(row, 2).Value = report.TotalAllocated;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = report.TotalUtilized;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = report.TotalBalance;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = report.TotalAllocated > 0m
                ? report.TotalUtilized / report.TotalAllocated
                : 0m;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00%";
            ws.Cell(row, 5).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
