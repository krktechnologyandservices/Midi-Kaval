using System.Net;
using Hangfire;
using MidiKaval.Api.Infrastructure.Email;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 3)]
public sealed class UserStatusEmailJob(
    IEmailSender emailSender,
    ILogger<UserStatusEmailJob> logger)
{
    public async Task ExecuteAsync(
        Guid userId,
        string targetEmail,
        string targetName,
        string actionType,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var encodedName = WebUtility.HtmlEncode(targetName);
        var encodedReason = reason is not null ? WebUtility.HtmlEncode(reason) : null;

        var (subject, body) = actionType switch
        {
            "suspended" => (
                "Your account has been suspended",
                $"""
                <p>Hi {encodedName},</p>
                <p>Your account has been suspended.
                {(string.IsNullOrWhiteSpace(reason) ? "" : $"\n<p><strong>Reason:</strong> {encodedReason}</p>")}
                You will not be able to log in until your account is reactivated.</p>
                <p>If you believe this is in error, contact your Director.</p>
                """),
            "reactivated" => (
                "Your account has been reactivated",
                $"""
                <p>Hi {encodedName},</p>
                <p>Your account has been reactivated. You can log in again.</p>
                <p>If you have any questions, contact your Director.</p>
                """),
            "deleted" => (
                "Your account has been permanently deleted",
                $"""
                <p>Hi {encodedName},</p>
                <p>Your account has been permanently deleted. This action is irreversible.</p>
                <p>If you believe this is an error, contact your Director.</p>
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), $"Unknown action type: '{actionType}'."),
        };

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(targetEmail, subject, body),
                cancellationToken);

            logger.LogInformation(
                "User status email sent successfully for user {UserId} ({ActionType}) to {Email}.",
                userId, actionType, targetEmail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "User status email delivery failed for user {UserId} ({ActionType}) to {Email}.",
                userId, actionType, targetEmail);

            throw;
        }
    }
}
