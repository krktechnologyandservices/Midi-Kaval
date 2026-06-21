using ClosedXML.Excel;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public static class CaseExcelExporter
{
    private static readonly string[] Headers =
    [
        "Crime Number",
        "ST Number",
        "Beneficiary Name",
        "Stage",
        "Offence Type",
        "Classification",
        "Area (domicile)",
        "Gender",
        "Family Type",
        "Economic Status",
        "Occupation",
        "Education Level",
        "Family History of Crime",
        "Recidivism (Before)",
        "Recidivism (After)",
        "Visits",
        "Next Visit Due (UTC)",
        "Updated (UTC)",
    ];

    public static byte[] Export(IReadOnlyList<CaseExportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Cases");

        for (var column = 0; column < Headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = Headers[column];
        }

        var headerRange = worksheet.Range(1, 1, 1, Headers.Length);
        headerRange.Style.Font.Bold = true;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var excelRow = rowIndex + 2;
            worksheet.Cell(excelRow, 1).Value = row.CrimeNumber;
            worksheet.Cell(excelRow, 2).Value = row.StNumber;
            worksheet.Cell(excelRow, 3).Value = row.BeneficiaryName;
            worksheet.Cell(excelRow, 4).Value = row.CurrentStage;
            worksheet.Cell(excelRow, 5).Value = row.TypeOfOffence;
            worksheet.Cell(excelRow, 6).Value = row.OffenceClassification;
            worksheet.Cell(excelRow, 7).Value = row.Domicile;
            worksheet.Cell(excelRow, 8).Value = row.Gender ?? string.Empty;
            worksheet.Cell(excelRow, 9).Value = row.FamilyType ?? string.Empty;
            worksheet.Cell(excelRow, 10).Value = row.EconomicStatus ?? string.Empty;
            worksheet.Cell(excelRow, 11).Value = row.Occupation ?? string.Empty;
            worksheet.Cell(excelRow, 12).Value = row.EducationLevel ?? string.Empty;
            worksheet.Cell(excelRow, 13).Value = row.FamilyHistoryOfCrime ? "Yes" : "No";
            worksheet.Cell(excelRow, 14).Value = row.RecidivismBeforeCount?.ToString() ?? string.Empty;
            worksheet.Cell(excelRow, 15).Value = row.RecidivismAfterCount?.ToString() ?? string.Empty;
            worksheet.Cell(excelRow, 16).Value = row.VisitCount;
            worksheet.Cell(excelRow, 17).Value = row.NextVisitDueAtUtc?.ToString("O") ?? string.Empty;
            worksheet.Cell(excelRow, 18).Value = row.UpdatedAtUtc.ToString("O");
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
