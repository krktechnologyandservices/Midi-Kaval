namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record AuditDigestEventItem(
    string ActionType,
    string TargetName,
    string TargetEmail,
    string ActorName,
    string ActorEmail,
    DateTime TimestampUtc);

internal sealed record AuditDigestEmailContext(
    string OrganisationName,
    IReadOnlyList<AuditDigestEventItem> Events);

internal static class AuditDigestEmailTemplate
{
    public static string RenderSubject(AuditDigestEmailContext context) =>
        $"User activity digest — {context.OrganisationName}";

    public static string RenderBody(AuditDigestEmailContext context)
    {
        var lines = new List<string> { $"User Activity Digest — {context.OrganisationName}", "" };

        foreach (var evt in context.Events)
        {
            lines.Add($"• {evt.ActionType}: {evt.TargetName} ({evt.TargetEmail})");
            lines.Add($"  By: {evt.ActorName} ({evt.ActorEmail})");
            lines.Add($"  When: {evt.TimestampUtc:O}");
            lines.Add("");
        }

        var body = string.Join("\n", lines);
        return EmailTemplateFooter.Append(body);
    }
}
