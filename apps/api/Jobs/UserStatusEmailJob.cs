using System.Net;
using Hangfire;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 3)]
public sealed class UserStatusEmailJob(
    IEmailSender emailSender,
    IUserNotificationRateLimiter rateLimiter,
    IAuditService auditService,
    ILogger<UserStatusEmailJob> logger)
{
    public async Task ExecuteAsync(
        Guid userId,
        Guid organisationId,
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
                {(string.IsNullOrWhiteSpace(reason) ? "" : $"\n<br/><strong>Reason:</strong> {encodedReason}")}
                You will not be able to log in until your account is reactivated.</p>
                <p>If you believe this is an error, contact another Director in your organisation to reactivate your account.</p>
                """),
            "reactivated" => (
                "Your account has been reactivated",
                $"""
                <p>Hi {encodedName},</p>
                <p>Your account has been reactivated. You can log in again.</p>
                <p>If you have any questions, contact another Director in your organisation.</p>
                """),
            "deleted" => (
                "Your account has been permanently deleted",
                $"""
                <p>Hi {encodedName},</p>
                <p>Your account has been permanently deleted. Your account and all associated personal data have been permanently removed. This action cannot be undone.</p>
                <p>If you believe this is an error, contact your Director.</p>
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), $"Unknown action type: '{actionType}'."),
        };

        // Check rate limit — skip silently if exceeded
        var canSend = await rateLimiter.CanSendAsync(userId, actionType, cancellationToken);
        if (!canSend)
        {
            logger.LogWarning(
                "Rate limit exceeded for user notification ({ActionType}) to user {UserId}. Skipping.",
                actionType, userId);
            return;
        }

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(targetEmail, subject, body),
                cancellationToken);

            // Record audit BEFORE rate-limit increment so a failed audit does not
            // consume a rate-limit slot (Hangfire retry will re-execute from scratch).
            await auditService.RecordAsync(
                AuditEventTypes.UserNotificationSent,
                organisationId,
                subjectUserId: userId,
                metadata: new Dictionary<string, object?>
                {
                    ["notification_type"] = actionType,
                    ["target_email"] = targetEmail,
                },
                cancellationToken: cancellationToken);

            await rateLimiter.RecordSendAsync(userId, actionType, cancellationToken);

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
