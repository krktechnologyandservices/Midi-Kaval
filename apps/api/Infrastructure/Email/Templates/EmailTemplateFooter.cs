namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal static class EmailTemplateFooter
{
    public const string Line = "Open Kaval Online for details.";

    public static string Append(string body) => $"{body.TrimEnd()}\n\n{Line}";
}
