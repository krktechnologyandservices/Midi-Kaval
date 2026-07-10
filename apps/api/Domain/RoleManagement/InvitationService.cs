using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models.Admin;
using MidiKaval.Api.Models.Audit;

namespace MidiKaval.Api.Domain.RoleManagement;

public class InvitationService(
    AppDbContext db,
    TokenService tokenService,
    IAuditService auditService,
    IConfiguration configuration,
    ILogger<InvitationService> logger)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<SendInvitationResponse> SendInvitationAsync(
        Guid organisationId,
        Guid invitedByUserId,
        SendInvitationRequest request,
        string? actorIpAddress = null,
        CancellationToken ct = default)
    {
        var invitableRoles = new[] { UserRoles.Director, UserRoles.Coordinator, UserRoles.SocialWorker, UserRoles.CaseWorker, UserRoles.Accountant };
        if (!invitableRoles.Contains(request.Role))
        {
            throw new InvalidOperationException("Invalid role. Allowed: Director, Coordinator, SocialWorker, CaseWorker, Accountant.");
        }

        var emailAlreadyRegistered = await db.Users
            .AnyAsync(u => u.Email == request.Email && u.OrganisationId == organisationId, ct);
        if (emailAlreadyRegistered)
        {
            throw new InvalidOperationException("This email address is already registered.");
        }

        var pendingExists = await db.Invitations
            .AnyAsync(i => i.TargetEmail == request.Email
                && i.OrganisationId == organisationId
                && i.Status == InvitationStatus.Pending, ct);
        if (pendingExists)
        {
            throw new InvalidOperationException("An invitation is already pending for this email. Use the resend option to send a new invitation.");
        }

        var tokenHours = configuration.GetValue<int>("INVITATION_TOKEN_TTL_HOURS", 24);
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            InvitedByUserId = invitedByUserId,
            TargetEmail = request.Email,
            Role = request.Role,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(tokenHours),
            Status = InvitationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Load inviter for snapshot inside the transaction to avoid TOCTOU race
            var inviter = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == invitedByUserId, ct);
            TargetUserSnapshotDto? inviterSnapshot = inviter is not null
                ? new TargetUserSnapshotDto(inviter.Email, $"{inviter.FirstName} {inviter.LastName}".Trim(), inviter.Role)
                : null;

            db.Invitations.Add(invitation);
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.InvitationSent,
                organisationId,
                actorUserId: invitedByUserId,
                subjectUserId: null,
                targetUserSnapshot: inviterSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = request.Email,
                    ["role"] = request.Role,
                    ["invited_by_user_id"] = invitedByUserId.ToString(),
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            EnqueueEmailJob(invitation.Id, rawToken, signature, request.Email, request.Role, request.Include2faInstructions);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new SendInvitationResponse(
            invitation.Id,
            request.Email,
            request.Role,
            $"Invitation sent to {request.Email}.");
    }

    public async Task<InvitationListResult> GetInvitationListAsync(
        Guid organisationId,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        if (pageSize < 1 || pageSize > MaxPageSize)
            pageSize = DefaultPageSize;

        if (page < 1)
            page = 1;

        var query = db.Invitations
            .AsNoTracking()
            .Where(i => i.OrganisationId == organisationId)
            .OrderByDescending(i => i.CreatedAtUtc);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvitationSummary(
                i.Id,
                i.TargetEmail,
                i.Role,
                i.Status.ToString(),
                i.CreatedAtUtc,
                i.ExpiresAtUtc,
                i.ConfirmedAtUtc,
                i.InvitedByUser != null ? i.InvitedByUser.Email : null,
                i.InvitedByUser != null
                    ? ((i.InvitedByUser.FirstName ?? "") + " " + (i.InvitedByUser.LastName ?? "")).Trim()
                    : null,
                db.ConfirmationTokens
                    .Where(t => t.InvitationId == i.Id)
                    .Select(t => t.ConsumedAtUtc)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new InvitationListResult(items, totalCount, page, pageSize);
    }

    public async Task<ResendInvitationResponse> ResendInvitationAsync(
        Guid organisationId,
        Guid invitationId,
        Guid resendByUserId,
        string? actorIpAddress = null,
        CancellationToken ct = default)
    {
        var tokenHours = configuration.GetValue<int>("INVITATION_TOKEN_TTL_HOURS", 24);
        if (tokenHours <= 0) tokenHours = 24;

        var invitation = await db.Invitations
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganisationId == organisationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.Status == InvitationStatus.Confirmed)
            throw new InvalidOperationException("Cannot resend a confirmed invitation.");

        // Create snapshot of original inviter for the notification event
        TargetUserSnapshotDto? originalInviterSnapshot = invitation.InvitedByUser is not null
            ? new TargetUserSnapshotDto(
                invitation.InvitedByUser.Email,
                $"{invitation.InvitedByUser.FirstName} {invitation.InvitedByUser.LastName}".Trim(),
                invitation.InvitedByUser.Role)
            : null;

        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Load resendByUser inside the transaction to avoid TOCTOU race
            var resendByUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == resendByUserId, ct);
            var resendByUserName = resendByUser is not null ? $"{resendByUser.FirstName} {resendByUser.LastName}".Trim() : "Unknown";

            // Create snapshot of the resending user
            TargetUserSnapshotDto? resenderSnapshot = resendByUser is not null
                ? new TargetUserSnapshotDto(resendByUser.Email, resendByUserName, resendByUser.Role)
                : null;

            invitation.TokenHash = tokenHash;
            invitation.ExpiresAtUtc = DateTime.UtcNow.AddHours(tokenHours);
            await db.SaveChangesAsync(ct);

            // Record resend audit event with inviter metadata
            await auditService.RecordAsync(
                AuditEventTypes.InvitationResent,
                organisationId,
                actorUserId: resendByUserId,
                subjectUserId: null,
                targetUserSnapshot: resenderSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = invitation.TargetEmail,
                    ["role"] = invitation.Role,
                    ["invitation_id"] = invitationId.ToString(),
                    ["original_invited_by_user_id"] = invitation.InvitedByUserId.ToString(),
                    ["resent_by_user_id"] = resendByUserId.ToString(),
                },
                cancellationToken: ct);

            // Record notification audit event
            await auditService.RecordAsync(
                AuditEventTypes.InvitationResentNotified,
                organisationId,
                actorUserId: resendByUserId,
                subjectUserId: invitation.InvitedByUserId,
                targetUserSnapshot: originalInviterSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = invitation.TargetEmail,
                    ["original_inviter_user_id"] = invitation.InvitedByUserId.ToString(),
                    ["original_inviter_email"] = invitation.InvitedByUser?.Email,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            // Enqueue email to target recipient (after commit)
            EnqueueEmailJob(invitationId, rawToken, signature, invitation.TargetEmail, invitation.Role, include2faInfo: true);

            // Enqueue notification to original inviter (after commit)
            if (invitation.InvitedByUser is not null)
            {
                var originalInviterName = $"{invitation.InvitedByUser.FirstName} {invitation.InvitedByUser.LastName}".Trim();
                EnqueueResendNotificationJob(
                    invitation.InvitedByUser.Email,
                    originalInviterName,
                    invitation.TargetEmail,
                    resendByUserName);
            }
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation(
            "Invitation {InvitationId} resent by user {ResendByUserId} (original inviter: {InvitedByUserId}).",
            invitationId, resendByUserId, invitation.InvitedByUserId);

        return new ResendInvitationResponse(
            invitation.Id,
            invitation.TargetEmail,
            invitation.ExpiresAtUtc,
            $"Invitation resent to {invitation.TargetEmail}.");
    }

    protected virtual void EnqueueEmailJob(Guid invitationId, string rawToken, string signature, string email, string role, bool include2faInfo = false)
    {
        BackgroundJob.Enqueue<InvitationEmailDeliveryJob>(j =>
            j.ExecuteAsync(invitationId, rawToken, signature, email, role, include2faInfo, CancellationToken.None));
    }

    protected virtual void EnqueueResendNotificationJob(
        string originalInviterEmail,
        string originalInviterName,
        string targetEmail,
        string resentByUserName)
    {
        BackgroundJob.Enqueue<InvitationResendNotificationJob>(j =>
            j.ExecuteAsync(originalInviterEmail, originalInviterName, targetEmail, resentByUserName, CancellationToken.None));
    }
}
