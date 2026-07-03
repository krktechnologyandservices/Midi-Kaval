using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 0)] // We handle retries ourselves with custom delays
public sealed class ConfirmationEmailDeliveryJob(
    AppDbContext db,
    IEmailSender emailSender,
    IConfiguration configuration,
    IAuditService auditService,
    ILogger<ConfirmationEmailDeliveryJob> logger)
{
    private const int MaxRetryAttempts = 3;
    private static readonly int[] RetryDelaysMinutes = [1, 5, 15];

    public async Task ExecuteAsync(
        Guid confirmationTokenId,
        string rawToken,
        string signature,
        string targetEmail,
        string userName,
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var token = await db.ConfirmationTokens
            .Include(t => t.User)
            .Include(t => t.Invitation)
                .ThenInclude(i => i!.Organisation)
            .FirstOrDefaultAsync(t => t.Id == confirmationTokenId, cancellationToken);

        if (token is null)
        {
            logger.LogWarning("Confirmation token {TokenId} not found. Skipping email delivery.", confirmationTokenId);
            return;
        }

        if (token.ConsumedAtUtc is not null)
        {
            logger.LogInformation("Confirmation token {TokenId} already consumed. Skipping email delivery.", confirmationTokenId);
            return;
        }

        if (token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            logger.LogWarning("Confirmation token {TokenId} expired. Skipping email delivery.", confirmationTokenId);
            return;
        }

        // Atomically increment delivery attempts to prevent race conditions
        // across concurrent Hangfire workers.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE confirmation_tokens SET delivery_attempts = delivery_attempts + 1, last_delivery_attempt_at_utc = NOW() WHERE id = {confirmationTokenId}",
            cancellationToken);

        // Reload delivery_attempts from DB to pick up the atomic increment
        await db.Entry(token).ReloadAsync(cancellationToken);

        var orgName = token.Invitation?.Organisation?.Name ?? string.Empty;
        var role = token.Invitation?.Role ?? string.Empty;
        var directorName = token.Invitation?.InvitedByUser?.FirstName;

        var ttlHours = configuration.GetValue<int>("ConfirmationLink:TokenTtlHours", 24);
        var baseUrl = configuration.GetValue<string>("ConfirmationLink:BaseUrl") ?? "http://localhost:4200";
        var confirmationUrl = $"{baseUrl.TrimEnd('/')}/confirm-email?token={rawToken}&sig={signature}";

        var orgRequires2fa = token.Invitation?.Organisation?.Require2fa ?? false;

        var emailContext = new ConfirmationEmailContext(
            userName,
            orgName,
            role,
            confirmationUrl,
            ttlHours,
            directorName,
            Include2faInfo: true,
            OrgRequires2fa: orgRequires2fa);

        var subject = ConfirmationEmailTemplate.RenderSubject(emailContext);
        var body = ConfirmationEmailTemplate.RenderBody(emailContext);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(targetEmail, subject, body),
                cancellationToken);

            token.DeliveryAttempts = 0;
            token.LastDeliveryAttemptAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);

            await auditService.RecordAsync(
                AuditEventTypes.ConfirmationDelivered,
                orgId,
                subjectUserId: userId,
                metadata: new Dictionary<string, object?>
                {
                    ["confirmationTokenId"] = confirmationTokenId,
                    ["email"] = targetEmail,
                },
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Confirmation email sent successfully for token {TokenId} to {Email}.",
                confirmationTokenId, targetEmail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await db.SaveChangesAsync(cancellationToken);

            if (token.DeliveryAttempts < MaxRetryAttempts)
            {
                var delayIndex = Math.Min(token.DeliveryAttempts - 1, RetryDelaysMinutes.Length - 1);
                var delayMinutes = RetryDelaysMinutes[delayIndex];

                BackgroundJob.Schedule<ConfirmationEmailDeliveryJob>(
                    j => j.ExecuteAsync(confirmationTokenId, rawToken, signature, targetEmail, userName, userId, orgId, CancellationToken.None),
                    TimeSpan.FromMinutes(delayMinutes));

                logger.LogWarning(
                    ex,
                    "Confirmation email delivery failed for token {TokenId} (attempt {AttemptCount}/{MaxAttempts}). " +
                    "Scheduled retry in {DelayMinutes} minute(s).",
                    confirmationTokenId, token.DeliveryAttempts, MaxRetryAttempts, delayMinutes);
            }
            else
            {
                await auditService.RecordAsync(
                    AuditEventTypes.ConfirmationDeliveryFailed,
                    orgId,
                    subjectUserId: userId,
                    metadata: new Dictionary<string, object?>
                    {
                        ["confirmationTokenId"] = confirmationTokenId,
                        ["deliveryAttempts"] = token.DeliveryAttempts,
                        ["email"] = targetEmail,
                    },
                    cancellationToken: cancellationToken);

                logger.LogError(
                    ex,
                    "Confirmation email delivery failed permanently for token {TokenId} after {MaxAttempts} attempts.",
                    confirmationTokenId, MaxRetryAttempts);
            }
        }
    }
}
