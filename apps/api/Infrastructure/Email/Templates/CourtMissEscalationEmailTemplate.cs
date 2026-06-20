using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record CourtMissEscalationEmailContext(
    string CourtName,
    DateTime ScheduledAtUtc,
    string Purpose,
    SensitivityLevel SensitivityLevel,
    string CrimeNumber,
    string StNumber);

internal static class CourtMissEscalationEmailTemplate
{
    public static string RenderSubject(CourtMissEscalationEmailContext context) =>
        $"Court sitting missed — {context.CourtName}";

    public static string RenderBody(CourtMissEscalationEmailContext context)
    {
        var purposeLine = CourtSittingEmailBodyHelper.FormatPurposeLine(
            context.Purpose,
            context.SensitivityLevel);

        var body =
            "Court sitting missed\n\n"
            + $"Court: {context.CourtName}\n"
            + $"Scheduled: {context.ScheduledAtUtc:O}\n"
            + $"{purposeLine}\n"
            + $"Crime #: {context.CrimeNumber}\n"
            + $"ST #: {context.StNumber}";

        return EmailTemplateFooter.Append(body);
    }
}
