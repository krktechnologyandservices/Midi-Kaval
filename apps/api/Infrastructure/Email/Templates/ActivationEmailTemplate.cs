using System.Web;

namespace MidiKaval.Api.Infrastructure.Email.Templates;

public sealed record ActivationEmailContext(
    string OrganisationName,
    string ActivationUrl
);

public static class ActivationEmailTemplate
{
    public static string RenderSubject(ActivationEmailContext context) =>
        $"Activate your {context.OrganisationName} organisation on Midi-Kaval";

    public static string RenderBody(ActivationEmailContext context) =>
        $"""
        Welcome to Midi-Kaval!

        Your organisation "{HttpUtility.HtmlEncode(context.OrganisationName)}" has been registered.
        Click the link below to activate your account and become the first Director:

        {context.ActivationUrl}

        This link expires in 7 days.

        If you did not expect this invitation, please ignore this email.
        """;
}
