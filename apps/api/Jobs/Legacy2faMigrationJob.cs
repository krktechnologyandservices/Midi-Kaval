using System.Globalization;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MidiKaval.Api.Jobs;

[AutomaticRetry(Attempts = 0)]
public sealed class Legacy2faMigrationJob(
    AppDbContext db,
    IEmailSender emailSender,
    IAuditService auditService,
    IConnectionMultiplexer redis,
    ILogger<Legacy2faMigrationJob> logger)
{
    private const int BatchSize = 100;
    private const string CursorKey = "legacy_2fa_migration_cursor";

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var redisDb = redis.GetDatabase();

        var cursorStr = await redisDb.StringGetAsync(CursorKey);
        DateTime cursor = DateTime.MinValue;
        if (cursorStr.HasValue)
        {
            if (!DateTime.TryParse(cursorStr!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out cursor))
            {
                logger.LogWarning("Legacy2faMigrationJob: Cursor value '{Cursor}' is not parseable. Resetting to MinValue.", cursorStr);
                cursor = DateTime.MinValue;
            }
        }

        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Organisation)
            .Where(u => u.TotpSecret == null && u.IsActive && !u.IsSuspended && u.CreatedAtUtc > cursor)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            logger.LogInformation("Legacy2faMigrationJob completed. Processed 0 users.");
            return;
        }

        var processedCount = 0;
        DateTime? lastCreatedAt = null;

        foreach (var user in users)
        {
            try
            {
                var setupUrl = user.Role == UserRoles.Vendor ? "/vendor/settings" : "/settings/2fa";
                var subject = "Action Required: Set Up Two-Factor Authentication";
                var body = $"""
                    Hi {user.FirstName},

                    Your organisation "{user.Organisation.Name}" is adopting two-factor authentication to improve security.

                    Please set up two-factor authentication by visiting:
                    {setupUrl}

                    If you have any questions, please contact your Director.
                    """;

                await emailSender.SendAsync(
                    new EmailMessage(user.Email, subject, body),
                    cancellationToken);

                await auditService.RecordAsync(
                    AuditEventTypes.TwoFactorMigrationEmailSent,
                    user.OrganisationId,
                    subjectUserId: user.Id,
                    metadata: new Dictionary<string, object?> { ["email"] = user.Email },
                    cancellationToken: cancellationToken);

                processedCount++;
                lastCreatedAt = user.CreatedAtUtc;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Legacy2faMigrationJob: Failed to send migration email to {Email}. Continuing.", user.Email);
            }
        }

        if (lastCreatedAt.HasValue)
        {
            await redisDb.StringSetAsync(CursorKey, lastCreatedAt.Value.ToString("O"));
        }

        logger.LogInformation(
            "Legacy2faMigrationJob completed. Processed {Count} users. Cursor at {Cursor}.",
            processedCount, lastCreatedAt?.ToString("O") ?? "none");
    }
}
