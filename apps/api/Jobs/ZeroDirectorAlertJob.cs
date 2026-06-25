using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

/// <summary>
/// Fire-and-forget Hangfire job that sends an email alert to all Vendor accounts
/// when an organisation enters zero-Director state.
/// </summary>
public sealed class ZeroDirectorAlertJob(
    AppDbContext db,
    IEmailSender emailSender,
    ILogger<ZeroDirectorAlertJob> logger)
{
    public async Task ExecuteAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        // 1. Load the organisation
        var org = await db.Organisations.FindAsync(new object[] { organisationId }, cancellationToken);
        if (org is null)
        {
            logger.LogWarning("ZeroDirectorAlertJob: Organisation {OrganisationId} not found.", organisationId);
            return;
        }

        // 2. Verify it still has zero active Directors (re-check to avoid race conditions)
        var hasDirector = await db.Users.AsNoTracking().AnyAsync(
            u => u.OrganisationId == organisationId
                && u.Role == UserRoles.Director
                && u.IsActive
                && !u.IsSuspended, cancellationToken);

        if (hasDirector)
        {
            logger.LogInformation(
                "ZeroDirectorAlertJob: Organisation {OrganisationId} now has active Directors. Skipping alert.",
                organisationId);
            return;
        }

        // 3. Find last known Director info (if available)
        var lastDirectorEvent = await db.AuditEvents
            .AsNoTracking()
            .Where(e =>
                e.OrganisationId == organisationId
                && (e.EventType == "user_created" || e.EventType == "user_deleted"))
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // 4. Send email alert to all Vendor accounts
        var vendors = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRoles.Vendor && u.IsActive && !u.IsSuspended)
            .ToListAsync(cancellationToken);

        if (vendors.Count == 0)
        {
            logger.LogWarning(
                "ZeroDirectorAlertJob: No active Vendor accounts found to notify for organisation {OrganisationId}.",
                organisationId);
            return;
        }

        // Build email content
        var lastDirectorInfo = lastDirectorEvent is not null
            ? $" Last known Director: {lastDirectorEvent.SubjectUserId} (last active: {lastDirectorEvent.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC)."
            : string.Empty;

        var subject = $"Midi-Kaval Alert: {org.Name} has no active Directors";
        var body = $"""
            Organisation "{org.Name}" has no active Directors.

            Operations cannot continue until a new Director is appointed.
            Please issue a new activation link from the Vendor backstage to resume operations.
            {lastDirectorInfo}

            This is an automated alert from Midi-Kaval.
            """;

        foreach (var vendor in vendors)
        {
            try
            {
                await emailSender.SendAsync(
                    new EmailMessage(vendor.Email, subject, body),
                    cancellationToken);

                logger.LogInformation(
                    "ZeroDirectorAlert sent to Vendor {VendorId} ({Email}) for organisation {OrganisationId}.",
                    vendor.Id, vendor.Email, organisationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to send ZeroDirectorAlert to Vendor {VendorId} ({Email}) for organisation {OrganisationId}.",
                    vendor.Id, vendor.Email, organisationId);
            }
        }
    }
}
