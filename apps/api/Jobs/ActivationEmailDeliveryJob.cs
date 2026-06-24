using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.Jobs;

public sealed class ActivationEmailDeliveryJob(
    AppDbContext db,
    IEmailSender emailSender,
    TokenService tokenService,
    IConfiguration configuration,
    ILogger<ActivationEmailDeliveryJob> logger)
{
    // Max 3 retries: delays [1, 5, 15] minutes — each retry increments DeliveryAttempts from 1 to 3
    private const int MaxRetryAttempts = 3;
    private static readonly int[] RetryDelaysMinutes = [1, 5, 15];

    [AutomaticRetry(Attempts = 0)] // We handle retries ourselves with custom delays
    public async Task ExecuteAsync(Guid activationTokenId, CancellationToken cancellationToken = default)
    {
        var token = await db.ActivationTokens
            .Include(t => t.Organisation)
            .FirstOrDefaultAsync(t => t.Id == activationTokenId, cancellationToken);

        if (token is null)
        {
            logger.LogWarning("Activation token {TokenId} not found for retry.", activationTokenId);
            return;
        }

        if (token.ConsumedAtUtc is not null)
        {
            logger.LogInformation("Activation token {TokenId} already consumed. Skipping retry.", activationTokenId);
            return;
        }

        if (token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            logger.LogWarning("Activation token {TokenId} expired. Skipping retry.", activationTokenId);
            return;
        }

        // On retry, generate a new raw token so the activation URL can be rebuilt.
        // The previous token's link remains valid until consumed or expired.
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        token.DeliveryAttempts++;

        var baseUrl = configuration.GetValue<string>("ActivationLink:BaseUrl") ?? "http://localhost:4200";
        var activationUrl = tokenService.BuildActivationUrl(baseUrl, rawToken, signature);

        var emailContext = new ActivationEmailContext(token.Organisation.Name, activationUrl);
        var subject = ActivationEmailTemplate.RenderSubject(emailContext);
        var body = ActivationEmailTemplate.RenderBody(emailContext);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(token.TargetEmail, subject, body),
                cancellationToken);

            token.DeliveryAttempts = 0;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Activation email retry succeeded for token {TokenId}.",
                activationTokenId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await db.SaveChangesAsync(cancellationToken);

            if (token.DeliveryAttempts <= MaxRetryAttempts)
            {
                var delayIndex = Math.Min(token.DeliveryAttempts - 1, RetryDelaysMinutes.Length - 1);
                var delayMinutes = RetryDelaysMinutes[delayIndex];

                BackgroundJob.Schedule<ActivationEmailDeliveryJob>(
                    j => j.ExecuteAsync(activationTokenId, CancellationToken.None),
                    TimeSpan.FromMinutes(delayMinutes));

                logger.LogWarning(
                    ex,
                    "Activation email delivery failed for token {TokenId} (attempt {AttemptCount}/{MaxAttempts}). " +
                    "Scheduled retry in {DelayMinutes} minute(s).",
                    activationTokenId, token.DeliveryAttempts, MaxRetryAttempts, delayMinutes);
            }
            else
            {
                logger.LogError(
                    ex,
                    "Activation email delivery failed permanently for token {TokenId} after {MaxAttempts} attempts.",
                    activationTokenId, MaxRetryAttempts);
            }
        }
    }
}
