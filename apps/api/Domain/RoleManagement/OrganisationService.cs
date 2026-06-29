using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Models.Vendor;
using StackExchange.Redis;

namespace MidiKaval.Api.Domain.RoleManagement;

public class OrganisationService(
    AppDbContext db,
    TokenService tokenService,
    IEmailSender emailSender,
    IConnectionMultiplexer multiplexer,
    IConfiguration configuration,
    LastDirectorGuard lastDirectorGuard,
    IAuditService auditService,
    ILogger<OrganisationService> logger)
{
    private static readonly TimeSpan EmailRateLimitWindow = TimeSpan.FromHours(1);
    private const int EmailRateLimitMax = 5;

    public async Task<CreateOrgResult> CreateOrganisationAsync(
        string name,
        string targetDirectorEmail,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate inputs
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
            throw new ValidationException("Organisation name must be between 1 and 256 characters.");

        if (string.IsNullOrWhiteSpace(targetDirectorEmail) || targetDirectorEmail.Length > 320)
            throw new ValidationException("Target director email must be between 1 and 320 characters.");

        if (!new EmailAddressAttribute().IsValid(targetDirectorEmail))
            throw new ValidationException("Target director email is not a valid email address.");

        // 2. Check per-email rate limit
        var isRateLimited = await IsEmailRateLimitedAsync(targetDirectorEmail);
        if (isRateLimited)
            throw new RateLimitExceededException("Too many activation requests for this email address. Please try again later.");

        // 3. Create Organisation + ActivationToken in a single transaction
        var now = DateTime.UtcNow;
        var organisation = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            IsActive = false,
            CreatedAtUtc = now,
        };
        db.Organisations.Add(organisation);

        // 4. Generate activation token
        var ttlHours = configuration.GetValue<int>("ACTIVATION_TOKEN_TTL_HOURS", 168);
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        var activationToken = new ActivationToken
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.Id,
            TokenHash = tokenHash,
            TargetEmail = targetDirectorEmail.Trim().ToLowerInvariant(),
            ExpiresAtUtc = now.AddHours(ttlHours),
            DeliveryAttempts = 1,
            CreatedAtUtc = now,
        };
        db.ActivationTokens.Add(activationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 5. Build activation URL and send email
        var baseUrl = configuration.GetValue<string>("ActivationLink:BaseUrl") ?? "http://localhost:4200";
        var activationUrl = tokenService.BuildActivationUrl(baseUrl, rawToken, signature);

        var emailContext = new ActivationEmailContext(organisation.Name, activationUrl);
        var subject = ActivationEmailTemplate.RenderSubject(emailContext);
        var body = ActivationEmailTemplate.RenderBody(emailContext);

        var emailSent = false;
        try
        {
            // Use a linked token source so email sending is not cancelled by client disconnect.
            // The email send gets a 30-second server timeout; the org/token already persisted.
            using var emailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            emailCts.CancelAfter(TimeSpan.FromSeconds(30));
            await emailSender.SendAsync(
                new EmailMessage(targetDirectorEmail.Trim(), subject, body),
                emailCts.Token);
            emailSent = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Initial activation email delivery failed for organisation {OrganisationId}, token {TokenId}. Will retry via background job.",
                organisation.Id, activationToken.Id);
        }

        return new CreateOrgResult(
            OrganisationId: organisation.Id,
            Name: organisation.Name,
            Status: emailSent ? "activation_sent" : "delivery_failed",
            ActivationTokenId: activationToken.Id);
    }

    public async Task<ReissueActivationResult> ReissueActivationAsync(
        Guid organisationId,
        string targetDirectorEmail,
        Guid? actorUserId = null,
        string? actorIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Load the organisation
        var org = await db.Organisations.FindAsync(new object[] { organisationId }, cancellationToken);
        if (org is null)
            throw new ValidationException("Organisation not found.");

        // 2. Validate email
        if (string.IsNullOrWhiteSpace(targetDirectorEmail) || targetDirectorEmail.Length > 320)
            throw new ValidationException("Target director email must be between 1 and 320 characters.");

        if (!new EmailAddressAttribute().IsValid(targetDirectorEmail))
            throw new ValidationException("Target director email is not a valid email address.");

        // 3. Check per-email rate limit
        var isRateLimited = await IsEmailRateLimitedAsync(targetDirectorEmail);
        if (isRateLimited)
            throw new RateLimitExceededException("Too many activation requests for this email address. Please try again later.");

        // 4. Serializable transaction: verify zero-Director state and persist token atomically
        var (rawToken, signature, activationToken, now) = await ExecuteReissueActivationAsync(
            organisationId, targetDirectorEmail, actorUserId, actorIpAddress, cancellationToken);

        // 5. Build activation URL and send email
        var baseUrl = configuration.GetValue<string>("ActivationLink:BaseUrl") ?? "http://localhost:4200";
        var activationUrl = tokenService.BuildActivationUrl(baseUrl, rawToken, signature);

        var emailContext = new ActivationEmailContext(org.Name, activationUrl);
        var subject = ActivationEmailTemplate.RenderSubject(emailContext);
        var body = ActivationEmailTemplate.RenderBody(emailContext);

        var emailSent = false;
        try
        {
            using var emailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            emailCts.CancelAfter(TimeSpan.FromSeconds(30));
            await emailSender.SendAsync(
                new EmailMessage(targetDirectorEmail.Trim(), subject, body),
                emailCts.Token);
            emailSent = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Reissue activation email delivery failed for organisation {OrganisationId}, token {TokenId}.",
                organisationId, activationToken.Id);
        }

        return new ReissueActivationResult(
            OrganisationId: org.Id,
            Name: org.Name,
            Status: emailSent ? "sent" : "delivery_failed",
            ActivationTokenId: activationToken.Id);
    }

    public async Task<List<VendorOrganisationSummary>> GetOrganisationListAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await db.Organisations
            .AsNoTracking()
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.IsActive,
                o.HasPendingRecovery,
                o.CreatedAtUtc,
                DirectorCount = o.Users.Count(u =>
                    u.Role == UserRoles.Director
                    && u.IsActive
                    && !u.IsSuspended),
            })
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orgs.Select(o => new VendorOrganisationSummary(
            o.Id, o.Name, o.IsActive, o.DirectorCount, o.HasPendingRecovery, o.CreatedAtUtc)).ToList();
    }

    public async Task<VendorOrganisationDetail?> GetOrganisationDetailAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var org = await db.Organisations
            .AsNoTracking()
            .Where(o => o.Id == organisationId)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.IsActive,
                o.HasPendingRecovery,
                o.CreatedAtUtc,
                DirectorCount = o.Users.Count(u =>
                    u.Role == UserRoles.Director
                    && u.IsActive
                    && !u.IsSuspended),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (org is null)
            return null;

        // Only populate last known Director info for orgs in zero-Director state
        string? lastKnownDirectorName = null;
        DateTime? lastKnownDirectorActiveAt = null;

        if (org.HasPendingRecovery || org.DirectorCount == 0)
        {
            var lastDirectorInfo = await lastDirectorGuard.GetLastKnownDirectorInfoAsync(organisationId, cancellationToken);
            if (lastDirectorInfo is not null)
            {
                lastKnownDirectorName = lastDirectorInfo.Name;
                lastKnownDirectorActiveAt = lastDirectorInfo.LastActiveAt;
            }
        }

        return new VendorOrganisationDetail(
            org.Id, org.Name, org.IsActive, org.DirectorCount, org.HasPendingRecovery,
            lastKnownDirectorName, lastKnownDirectorActiveAt, org.CreatedAtUtc);
    }

    /// <summary>
    /// Executes the transactional part of reissue activation: serializable transaction
    /// to verify zero-Director state and persist the new activation token atomically.
    /// Override in tests to bypass relational-provider requirements (InMemory does not
    /// support transactions or raw SQL).
    /// </summary>
    protected virtual async Task<(string rawToken, string signature, ActivationToken token, DateTime now)> ExecuteReissueActivationAsync(
        Guid organisationId, string targetDirectorEmail, Guid? actorUserId, string? actorIpAddress = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ttlHours = configuration.GetValue<int>("ACTIVATION_TOKEN_TTL_HOURS", 168);
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var hasDirector = await lastDirectorGuard.HasAnyActiveDirectorAsync(organisationId, cancellationToken);
        if (hasDirector)
            throw new ValidationException("This organisation already has active Directors.");

        var activationToken = new ActivationToken
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            TokenHash = tokenHash,
            TargetEmail = targetDirectorEmail.Trim().ToLowerInvariant(),
            ExpiresAtUtc = now.AddHours(ttlHours),
            DeliveryAttempts = 0,
            CreatedAtUtc = now,
        };
        db.ActivationTokens.Add(activationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Record audit event for compliance
        if (actorUserId.HasValue)
        {
            await auditService.RecordAsync(
                AuditEventTypes.ActivationReissued,
                organisationId,
                actorUserId: actorUserId,
                actorIpAddress: actorIpAddress,
                cancellationToken: cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return (rawToken, signature, activationToken, now);
    }

    protected virtual async Task<bool> IsEmailRateLimitedAsync(string email)
    {
        var redis = multiplexer.GetDatabase();
        var key = $"ratelimit:email:{email.ToLowerInvariant()}";

        // Use a Lua script for atomic INCR + EXPIRE to avoid crash between calls
        var script = @"
            local count = redis.call('INCR', KEYS[1])
            redis.call('EXPIRE', KEYS[1], ARGV[1])
            return count";

        var count = (long)await redis.ScriptEvaluateAsync(script, [key], [(int)EmailRateLimitWindow.TotalSeconds]);
        return count > EmailRateLimitMax;
    }
}

public sealed record CreateOrgResult(
    Guid OrganisationId,
    string Name,
    string Status,
    Guid ActivationTokenId
);

public sealed record ReissueActivationResult(
    Guid OrganisationId,
    string Name,
    string Status,
    Guid ActivationTokenId
);

public sealed class RateLimitExceededException(string message) : Exception(message);
