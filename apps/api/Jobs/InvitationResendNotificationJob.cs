using System.Net;
using Hangfire;
using MidiKaval.Api.Infrastructure.Email;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 3)]
public sealed class InvitationResendNotificationJob(
    IEmailSender emailSender,
    ILogger<InvitationResendNotificationJob> logger)
{
    public async Task ExecuteAsync(
        string originalInviterEmail,
        string originalInviterName,
        string targetEmail,
        string resentByUserName,
        CancellationToken cancellationToken = default)
    {
        var encodedInviterName = WebUtility.HtmlEncode(originalInviterName);
        var encodedTargetEmail = WebUtility.HtmlEncode(targetEmail);
        var encodedResentBy = WebUtility.HtmlEncode(resentByUserName);

        var subject = $"Invitation resent for {targetEmail}";
        var body = $"""
            <p>Hi {encodedInviterName},</p>
            <p>A new invitation was sent to <strong>{encodedTargetEmail}</strong> by {encodedResentBy}, replacing the one you sent.</p>
            <p>If you have any questions, contact your Director.</p>
            """;

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(originalInviterEmail, subject, body),
                cancellationToken);

            logger.LogInformation(
                "Resend notification sent to original inviter {InviterEmail} for target {TargetEmail}, resent by {ResentBy}.",
                originalInviterEmail, targetEmail, resentByUserName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Resend notification delivery failed for original inviter {InviterEmail}, target {TargetEmail}.",
                originalInviterEmail, targetEmail);

            throw;
        }
    }
}
