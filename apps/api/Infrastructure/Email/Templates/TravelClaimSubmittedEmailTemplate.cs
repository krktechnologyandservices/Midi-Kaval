namespace MidiKaval.Api.Infrastructure.Email.Templates;

internal sealed record TravelClaimSubmittedEmailContext(
    string ClaimantEmail,
    string Destination,
    decimal Amount,
    DateTime ClaimDate,
    Guid ClaimId);

internal static class TravelClaimSubmittedEmailTemplate
{
    public static string RenderSubject(TravelClaimSubmittedEmailContext context) =>
        $"Travel claim submitted — {context.Destination}";

    public static string RenderBody(TravelClaimSubmittedEmailContext context)
    {
        var body =
            "A travel claim was submitted for review.\n\n"
            + $"Claimant: {context.ClaimantEmail}\n"
            + $"Destination: {context.Destination}\n"
            + $"Amount: ₹{context.Amount:0.##}\n"
            + $"Claim date: {context.ClaimDate:O}\n"
            + $"Claim ID: {context.ClaimId:D}";

        return EmailTemplateFooter.Append(body);
    }
}
