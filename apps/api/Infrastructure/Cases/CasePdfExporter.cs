using MidiKaval.Api.Models.Cases;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MidiKaval.Api.Infrastructure.Cases;

public static class CasePdfExporter
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
        "Visits",
        "Next Visit Due (UTC)",
        "Updated (UTC)",
    ];

    public static byte[] Export(IReadOnlyList<CaseExportRowDto> rows)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Text("Case export").SemiBold().FontSize(12);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var i = 0; i < Headers.Length; i++)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var title in Headers)
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(title).SemiBold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Padding(2).Text(row.CrimeNumber);
                        table.Cell().Padding(2).Text(row.StNumber);
                        table.Cell().Padding(2).Text(row.BeneficiaryName);
                        table.Cell().Padding(2).Text(row.CurrentStage);
                        table.Cell().Padding(2).Text(row.TypeOfOffence);
                        table.Cell().Padding(2).Text(row.OffenceClassification);
                        table.Cell().Padding(2).Text(row.Domicile);
                        table.Cell().Padding(2).Text(row.VisitCount.ToString());
                        table.Cell().Padding(2).Text(row.NextVisitDueAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty);
                        table.Cell().Padding(2).Text(row.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                    }
                });
            });
        }).GeneratePdf();
    }
}
