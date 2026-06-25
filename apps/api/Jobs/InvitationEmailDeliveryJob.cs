using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 3)]
public sealed class InvitationEmailDeliveryJob(
    AppDbContext db,
    IEmailSender emailSender,
    TokenService tokenService,
    IConfiguration configuration,
    ILogger<InvitationEmailDeliveryJob> logger)
{
    public async Task ExecuteAsync(
        Guid invitationId,
        string rawToken,
        string signature,
        string targetEmail,
        string role,
        CancellationToken cancellationToken = default)
    {
        var invitation = await db.Invitations
            .Include(i => i.Organisation)
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null)
        {
            logger.LogWarning("Invitation {InvitationId} not found. Skipping email delivery.", invitationId);
            return;
        }

        if (invitation.Status == InvitationStatus.Confirmed)
        {
            logger.LogInformation("Invitation {InvitationId} already confirmed. Skipping email delivery.", invitationId);
            return;
        }

        if (invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            logger.LogWarning("Invitation {InvitationId} expired. Skipping email delivery.", invitationId);
            return;
        }

        var baseUrl = configuration.GetValue<string>("ActivationLink:BaseUrl") ?? "http://localhost:4200";
        var invitationUrl = tokenService.BuildInvitationUrl(baseUrl, rawToken, signature);

        var orgName = invitation.Organisation?.Name ?? "your organisation";

        var subject = $"You've been invited to join {orgName}";
        var body = $"""
            <p>You have been invited to join <strong>{orgName}</strong> as a <strong>{role}</strong>.</p>
            <p>Click the link below to accept your invitation:</p>
            <p><a href="{invitationUrl}">{invitationUrl}</a></p>
            <p>This link expires in 24 hours.</p>
            """;

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(targetEmail, subject, body),
                cancellationToken);

            logger.LogInformation(
                "Invitation email sent successfully for invitation {InvitationId} to {Email}.",
                invitationId, targetEmail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Invitation email delivery failed for invitation {InvitationId} to {Email}.",
                invitationId, targetEmail);

            throw;
        }
    }
}
