using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Admin;
using MidiKaval.Api.Models.Audit;
using StackExchange.Redis;

namespace MidiKaval.Api.Domain.RoleManagement;

public class AdminTwoFactorService(
    AppDbContext db,
    IConnectionMultiplexer redis,
    TwoFactorService twoFactorService,
    BackupCodeService backupCodeService,
    LastDirectorGuard lastDirectorGuard,
    IEmailSender emailSender,
    IAuditService auditService,
    ILogger<AdminTwoFactorService> logger)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int BypassCodeLimit = 2;
    private const int BypassCodeExpirySeconds = 1800; // 30 min
    private static readonly TimeSpan BypassCodeTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    public virtual async Task<ResetTwoFactorResponse> ResetTwoFactorAsync(
        Guid actorUserId, Guid targetUserId, Guid organisationId,
        string? actorRole, string? actorIpAddress, CancellationToken ct)
    {
        // Check if target user is the last active Director
        var isLastDirector = await lastDirectorGuard.IsLastActiveDirectorAsync(organisationId, targetUserId, ct);
        if (isLastDirector)
        {
            throw new InvalidOperationException("Cannot reset 2FA for the last active Director.");
        }

        // Coordinator scope enforcement
        if (string.Equals(actorRole, UserRoles.Coordinator, StringComparison.OrdinalIgnoreCase))
        {
            var redisDb = redis.GetDatabase();
            var delegationKey = $"delegate_2fa_reset:{organisationId}";
            var delegationEnabled = await redisDb.KeyExistsAsync(delegationKey)
                && (bool)await redisDb.StringGetAsync(delegationKey);

            if (!delegationEnabled)
            {
                throw new InvalidOperationException("2FA reset delegation is not enabled for this organisation.");
            }

            // Verify target role is SocialWorker or CaseWorker
            var targetUser = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == targetUserId && u.OrganisationId == organisationId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (targetUser is null)
                throw new KeyNotFoundException("User not found.");

            if (targetUser != UserRoles.SocialWorker && targetUser != UserRoles.CaseWorker)
            {
                throw new InvalidOperationException("Coordinators can only reset 2FA for Social Workers and Case Workers.");
            }
        }

        // Execute reset
        await twoFactorService.ResetTwoFactorAsync(actorUserId, targetUserId, organisationId, actorIpAddress, ct);
        await backupCodeService.RevokeAllAsync(targetUserId);

        return new ResetTwoFactorResponse(targetUserId, "Two-factor authentication has been reset.");
    }

    public virtual async Task SendReminderAsync(
        Guid actorUserId, Guid targetUserId, Guid organisationId,
        string? actorIpAddress, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Organisation)
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var setupUrl = user.Role == UserRoles.Vendor ? "/vendor/settings" : "/settings/2fa";
        var orgName = user.Organisation?.Name ?? "your organisation";

        var subject = "Action Required: Set Up Two-Factor Authentication";
        var body = $"""
            Hi {user.FirstName},

            Your organisation "{orgName}" requires you to set up two-factor authentication (2FA) to improve security.

            Please set up two-factor authentication by visiting:
            {setupUrl}

            If you have any questions, please contact your Director.
            """;

        await emailSender.SendAsync(new EmailMessage(user.Email, subject, body), ct);

        await auditService.RecordAsync(
            AuditEventTypes.TwoFactorReminderSent,
            organisationId,
            actorUserId: actorUserId,
            subjectUserId: targetUserId,
            metadata: new Dictionary<string, object?> { ["email"] = user.Email },
            cancellationToken: ct);
    }

    public virtual async Task<BypassCodeResult> GenerateBypassCodeAsync(
        Guid directorUserId, Guid targetUserId, Guid organisationId,
        string? actorIpAddress, CancellationToken ct)
    {
        // Verify target user exists in org
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var redisDb = redis.GetDatabase();

        // Atomic rate limit check — increment and check post-increment value
        var hourWindow = DateTime.UtcNow.ToString("yyyyMMddHH");
        var rateLimitKey = $"bypass_count:{directorUserId}:{hourWindow}";
        var count = await redisDb.StringIncrementAsync(rateLimitKey);

        if (count > BypassCodeLimit)
        {
            await redisDb.StringDecrementAsync(rateLimitKey);
            throw new InvalidOperationException("Rate limit exceeded. Maximum 2 bypass codes per hour.");
        }

        // Set TTL on first creation
        await redisDb.KeyExpireAsync(rateLimitKey, RateLimitWindow);

        // Generate 12-char code
        var plaintext = GenerateBypassCode();
        var hash = ComputeSha256Hex(plaintext);

        // Store hash in Redis with 30-min TTL
        var storageKey = $"bypass_code:{targetUserId}:{hash}";
        await redisDb.StringSetAsync(storageKey, "1", BypassCodeTtl);

        // Record audit
        await auditService.RecordAsync(
            AuditEventTypes.TwoFactorBypassGenerated,
            organisationId,
            actorUserId: directorUserId,
            subjectUserId: targetUserId,
            actorIpAddress: actorIpAddress,
            cancellationToken: ct);

        return new BypassCodeResult(plaintext, BypassCodeExpirySeconds);
    }

    public virtual async Task<(AuditListResultDto Dto, int TotalCount, int Page, int PageSize)> GetAuditLogAsync(
        Guid organisationId,
        string? eventType,
        Guid? userId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;
        if (pageSize < 1)
            pageSize = DefaultPageSize;
        if (page < 1)
            page = 1;

        var query = db.AuditEvents
            .Where(e => e.OrganisationId == organisationId && e.EventType.StartsWith("2fa_"));

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (userId.HasValue)
        {
            query = query.Where(e => e.ActorUserId == userId.Value || e.SubjectUserId == userId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc <= to.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var rawItems = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.EventType,
                e.CreatedAtUtc,
                e.ActorUserId,
                ActorEmail = e.ActorUser != null ? e.ActorUser.Email : null,
                ActorName = e.ActorUser != null ? e.ActorUser.FirstName + " " + e.ActorUser.LastName : null,
                e.SubjectUserId,
                SubjectEmail = e.SubjectUser != null ? e.SubjectUser.Email : null,
                SubjectName = e.SubjectUser != null ? e.SubjectUser.FirstName + " " + e.SubjectUser.LastName : null,
                e.TargetUserSnapshot,
                e.ActorIpAddress,
                e.MetadataJson,
            })
            .ToListAsync(ct);

        var items = rawItems.Select(r => new AuditEventDto(
            r.Id, r.EventType, r.CreatedAtUtc,
            r.ActorUserId, r.ActorEmail, r.ActorName,
            r.SubjectUserId, r.SubjectEmail, r.SubjectName,
            r.TargetUserSnapshot is not null
                ? System.Text.Json.JsonSerializer.Deserialize<TargetUserSnapshotDto>(r.TargetUserSnapshot)
                : null,
            r.ActorIpAddress,
            r.MetadataJson is not null
                ? System.Text.Json.JsonSerializer.Deserialize<object>(r.MetadataJson)
                : null
        )).ToList();

        return (new AuditListResultDto(items), totalCount, page, pageSize);
    }

    public virtual async Task<bool> SetRequire2faAsync(Guid organisationId, Guid actorUserId, bool require2fa, CancellationToken ct)
    {
        var org = await db.Organisations.FirstOrDefaultAsync(o => o.Id == organisationId, ct)
            ?? throw new KeyNotFoundException("Organisation not found.");

        org.Require2fa = require2fa;
        await db.SaveChangesAsync(ct);

        var auditEventType = require2fa
            ? AuditEventTypes.TwoFactorMandateEnabled
            : AuditEventTypes.TwoFactorMandateDisabled;

        await auditService.RecordAsync(
            auditEventType,
            organisationId,
            actorUserId: actorUserId,
            metadata: new Dictionary<string, object?> { ["require2fa"] = require2fa },
            cancellationToken: ct);

        return org.Require2fa;
    }

    public virtual async Task<bool> SetDelegationAsync(Guid organisationId, Guid actorUserId, bool enabled, CancellationToken ct)
    {
        var redisDb = redis.GetDatabase();
        var key = $"delegate_2fa_reset:{organisationId}";

        if (enabled)
        {
            await redisDb.StringSetAsync(key, true);
        }
        else
        {
            await redisDb.KeyDeleteAsync(key);
        }

        var auditEventType = enabled
            ? AuditEventTypes.TwoFactorDelegationEnabled
            : AuditEventTypes.TwoFactorDelegationDisabled;

        await auditService.RecordAsync(
            auditEventType,
            organisationId,
            actorUserId: actorUserId,
            metadata: new Dictionary<string, object?> { ["enabled"] = enabled },
            cancellationToken: ct);

        return enabled;
    }

    private static string GenerateBypassCode()
    {
        var bytes = new byte[9];
        RandomNumberGenerator.Fill(bytes);

        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var codeChars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            codeChars[i] = chars[bytes[i] % chars.Length];
        }

        return $"{new string(codeChars, 0, 4)}-{new string(codeChars, 4, 4)}-{new string(codeChars, 8, 4)}";
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record BypassCodeResult(string BypassCode, int ExpiresInSeconds);
