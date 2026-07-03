using System.Web;

namespace MidiKaval.Api.Infrastructure.Email.Templates;

public sealed record ConfirmationEmailContext(
    string UserName,
    string OrganisationName,
    string Role,
    string ConfirmationUrl,
    int TokenTtlHours,
    string? DirectorName,
    bool Include2faInfo = false,
    bool OrgRequires2fa = false
);

public static class ConfirmationEmailTemplate
{
    public static string RenderSubject(ConfirmationEmailContext context) =>
        $"Confirm your account on {context.OrganisationName}";

    public static string RenderBody(ConfirmationEmailContext context)
    {
        var welcome = $"Welcome, {HttpUtility.HtmlEncode(context.UserName)}!";
        var invitationLine = context.DirectorName is not null
            ? $"You've been invited by {HttpUtility.HtmlEncode(context.DirectorName)} to join {HttpUtility.HtmlEncode(context.OrganisationName)} as a {HttpUtility.HtmlEncode(context.Role)}."
            : $"You've been invited to join {HttpUtility.HtmlEncode(context.OrganisationName)} as a {HttpUtility.HtmlEncode(context.Role)}.";

        var body = $"""
        {welcome}

        {invitationLine}

        Click the link below to confirm your account:

        {context.ConfirmationUrl}

        This link expires in {context.TokenTtlHours} hours.

        If you did not expect this invitation, please ignore this email.
        """;

        if (context.Include2faInfo)
        {
            var setupUrl = context.Role == "Vendor" ? "/vendor/settings" : "/settings/2fa";
            body += $$"""


            After logging in, set up two-factor authentication (2FA) to add an extra layer of security to your account. You can set this up at {{setupUrl}}.
            """;
            if (context.OrgRequires2fa)
            {
                body += """

                Your organisation requires two-factor authentication. You will be prompted to set it up on your next login.
                """;
            }
        }

        return body;
    }
}
