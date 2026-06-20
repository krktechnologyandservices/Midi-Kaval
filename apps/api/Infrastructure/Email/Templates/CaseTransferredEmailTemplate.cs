namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record CaseTransferredEmailContext(
    string CrimeNumber,
    string StNumber,
    string AssigneeEmail,
    DateTime TransferredAtUtc);

internal static class CaseTransferredEmailTemplate
{
    public static string RenderSubject(CaseTransferredEmailContext context) =>
        $"Case assigned — Crime {context.CrimeNumber}";

    public static string RenderBody(CaseTransferredEmailContext context)
    {
        var body =
            "A case was assigned to a field worker.\n\n"
            + $"Crime #: {context.CrimeNumber}\n"
            + $"ST #: {context.StNumber}\n"
            + $"Assignee: {context.AssigneeEmail}\n"
            + $"Transferred: {context.TransferredAtUtc:O}";

        return EmailTemplateFooter.Append(body);
    }
}
