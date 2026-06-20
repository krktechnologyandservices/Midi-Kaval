namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record TravelClaimDecisionEmailContext(
    string ClaimantEmail,
    string Destination,
    decimal Amount,
    bool IsApproved,
    string? DirectorComment,
    Guid ClaimId);

internal static class TravelClaimDecisionEmailTemplate
{
    public static string RenderSubject(TravelClaimDecisionEmailContext context)
    {
        var status = context.IsApproved ? "approved" : "returned";
        return $"Travel claim {status} — {context.Destination} ({context.ClaimId:D})";
    }

    public static string RenderBody(TravelClaimDecisionEmailContext context)
    {
        var status = context.IsApproved ? "approved" : "returned";
        var body =
            $"Claim from {context.ClaimantEmail} for {context.Destination} (₹{context.Amount:0.##}) was {status}.";

        if (!string.IsNullOrWhiteSpace(context.DirectorComment))
        {
            body += $"\nDirector note: {context.DirectorComment}";
        }

        return EmailTemplateFooter.Append(body);
    }
}
