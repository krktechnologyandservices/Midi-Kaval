using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Notifications;

internal static class TravelClaimNotificationCopy
{
    public static (string Title, string Body) BuildApproved(TravelClaim claim, string? comment)
    {
        var title = "Travel claim approved";
        var body = $"Your claim for {claim.Destination} (₹{claim.Amount:0.##}) was approved.";
        if (!string.IsNullOrWhiteSpace(comment))
        {
            body += $"\nDirector note: {comment}";
        }

        return (title, body);
    }

    public static (string Title, string Body) BuildReturned(TravelClaim claim, string comment)
    {
        var title = "Travel claim returned";
        var body =
            $"Your claim for {claim.Destination} (₹{claim.Amount:0.##}) was returned.\nDirector note: {comment}";
        return (title, body);
    }
}
