using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record CourtReminderEmailContext(
    string CourtName,
    DateTime ScheduledAtUtc,
    string Purpose,
    SensitivityLevel SensitivityLevel,
    string CrimeNumber,
    string StNumber);

internal static class CourtReminderEmailTemplate
{
    public static string RenderSubject(CourtReminderEmailContext context) =>
        $"Court sitting reminder — {context.CourtName}";

    public static string RenderBody(CourtReminderEmailContext context)
    {
        var purposeLine = CourtSittingEmailBodyHelper.FormatPurposeLine(
            context.Purpose,
            context.SensitivityLevel);

        var body =
            "Court sitting reminder\n\n"
            + $"Court: {context.CourtName}\n"
            + $"Scheduled: {context.ScheduledAtUtc:O}\n"
            + $"{purposeLine}\n"
            + $"Crime #: {context.CrimeNumber}\n"
            + $"ST #: {context.StNumber}";

        return EmailTemplateFooter.Append(body);
    }
}
