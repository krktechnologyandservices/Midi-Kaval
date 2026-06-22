using ClosedXML.Excel;
using MidiKaval.Api.Models.Reports;

namespace MidiKaval.Api.Infrastructure.Reports;

public sealed class SocioDemographicProfileExcelService
{
    private static readonly XLColor HeaderBgColor = XLColor.FromArgb(0xE5E7EB);
    private static readonly XLColor EvenRowColor = XLColor.FromArgb(0xF9FAFB);
    private static readonly XLColor OddRowColor = XLColor.White;

    public byte[] Generate(SocioDemographicProfileDto report, DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var workbook = new XLWorkbook();

        GenerateSheet1(workbook, report, generatedAtUtc);
        GenerateSheet2(workbook, report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void GenerateSheet1(XLWorkbook workbook, SocioDemographicProfileDto report, DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("List of Children");

        // Title
        ws.Cell(1, 1).Value = $"Socio-Demographic Profile — {report.Month}/{report.Year} (Generated: {generatedAtUtc:yyyy-MM-dd HH:mm:ss UTC})";
        ws.Range(1, 1, 1, 9).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        if (report.Children.Count == 0)
        {
            ws.Cell(3, 1).Value = "No data available for the selected period";
            ws.Range(3, 1, 3, 9).Merge();
            ws.Columns().AdjustToContents();
            return;
        }

        // Column headers
        var headers = new[] { "Sl No", "Name", "Age", "Contact", "Case Committed", "Crime Number", "ST Number", "Status", "Present Stage" };
        for (var col = 0; col < headers.Length; col++)
        {
            ws.Cell(3, col + 1).Value = headers[col];
            ws.Cell(3, col + 1).Style.Font.Bold = true;
            ws.Cell(3, col + 1).Style.Fill.BackgroundColor = HeaderBgColor;
        }

        ws.SheetView.Freeze(4, 0);

        // Data rows
        var row = 4;
        foreach (var child in report.Children)
        {
            ws.Cell(row, 1).Value = child.SlNo;
            ws.Cell(row, 2).Value = child.Name;
            ws.Cell(row, 3).Value = child.Age.HasValue ? (XLCellValue)child.Age.Value : (XLCellValue)"—";
            ws.Cell(row, 4).Value = child.Contact ?? "—";
            ws.Cell(row, 5).Value = child.CaseCommittedDate.ToString("dd-MMM-yyyy");
            ws.Cell(row, 6).Value = child.CrimeNumber;
            ws.Cell(row, 7).Value = child.StNumber;
            ws.Cell(row, 8).Value = child.Status;
            ws.Cell(row, 9).Value = child.PresentStage;

            // Alternating row colors
            var rowRange = ws.Range(row, 1, row, 9);
            rowRange.Style.Fill.BackgroundColor = row % 2 == 0 ? EvenRowColor : OddRowColor;

            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void GenerateSheet2(XLWorkbook workbook, SocioDemographicProfileDto report)
    {
        var ws = workbook.Worksheets.Add("Cross-Tabulation");

        // Title
        ws.Cell(1, 1).Value = $"Cross-Tabulation — {report.Month}/{report.Year}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        if (report.CrossTabulation.Count == 0)
        {
            ws.Cell(3, 1).Value = "No data available for the selected period";
            return;
        }

        var currentRow = 3;

        foreach (var section in report.CrossTabulation)
        {
            if (section.Categories.Count == 0)
                continue;

            var catCount = section.Categories.Count;
            var colCount = catCount + 1; // dimension name column + category columns

            // Header row
            ws.Cell(currentRow, 1).Value = section.DimensionName;
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 1).Style.Fill.BackgroundColor = HeaderBgColor;

            for (var i = 0; i < catCount; i++)
            {
                ws.Cell(currentRow, i + 2).Value = section.Categories[i].CategoryName;
                ws.Cell(currentRow, i + 2).Style.Font.Bold = true;
                ws.Cell(currentRow, i + 2).Style.Fill.BackgroundColor = HeaderBgColor;
            }

            // Data row
            currentRow++;
            ws.Cell(currentRow, 1).Value = "Count";
            ws.Cell(currentRow, 1).Style.Font.Bold = true;

            for (var i = 0; i < catCount; i++)
            {
                ws.Cell(currentRow, i + 2).Value = section.Categories[i].Count;
            }

            currentRow += 2; // blank row between sections
        }

        ws.Columns().AdjustToContents();
    }
}
