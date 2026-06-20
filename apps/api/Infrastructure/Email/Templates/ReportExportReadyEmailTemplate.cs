namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record ReportExportReadyEmailContext(
    string ReportName,
    DateTime ExpiresAtUtc);

internal static class ReportExportReadyEmailTemplate
{
    public static string RenderSubject(ReportExportReadyEmailContext context) =>
        $"Report ready — {context.ReportName}";

    public static string RenderBody(ReportExportReadyEmailContext context)
    {
        var body =
            "Your report export is ready.\n\n"
            + $"Report: {context.ReportName}\n"
            + $"Expires: {context.ExpiresAtUtc:O}";

        return EmailTemplateFooter.Append(body);
    }
}
