using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models.Admin;

namespace MidiKaval.Api.Domain.RoleManagement;

public class InvitationService(
    AppDbContext db,
    TokenService tokenService,
    IAuditService auditService,
    IConfiguration configuration)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<SendInvitationResponse> SendInvitationAsync(
        Guid organisationId,
        Guid invitedByUserId,
        SendInvitationRequest request,
        CancellationToken ct)
    {
        var invitableRoles = new[] { UserRoles.Coordinator, UserRoles.SocialWorker, UserRoles.CaseWorker, UserRoles.Accountant };
        if (!invitableRoles.Contains(request.Role))
        {
            throw new InvalidOperationException("Invalid role. Allowed: Coordinator, SocialWorker, CaseWorker, Accountant.");
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
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync(ct);

            EnqueueEmailJob(invitation.Id, rawToken, signature, request.Email, request.Role);

            await auditService.RecordAsync(
                AuditEventTypes.InvitationSent,
                organisationId,
                actorUserId: invitedByUserId,
                subjectUserId: null,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = request.Email,
                    ["role"] = request.Role,
                    ["invited_by_user_id"] = invitedByUserId.ToString(),
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
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
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

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
                i.Status,
                i.CreatedAtUtc,
                i.ExpiresAtUtc,
                i.ConfirmedAtUtc))
            .ToListAsync(ct);

        return new InvitationListResult(items, totalCount, page, pageSize);
    }

    public async Task<ResendInvitationResponse> ResendInvitationAsync(
        Guid organisationId,
        Guid invitationId,
        Guid resendByUserId,
        CancellationToken ct)
    {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganisationId == organisationId, ct);
        if (invitation is null)
        {
            throw new KeyNotFoundException("Invitation not found.");
        }

        if (invitation.Status == InvitationStatus.Confirmed)
        {
            throw new InvalidOperationException("Cannot resend a confirmed invitation.");
        }

        var tokenHours = configuration.GetValue<int>("INVITATION_TOKEN_TTL_HOURS", 24);
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            invitation.TokenHash = tokenHash;
            invitation.ExpiresAtUtc = DateTime.UtcNow.AddHours(tokenHours);
            await db.SaveChangesAsync(ct);

            EnqueueEmailJob(invitationId, rawToken, signature, invitation.TargetEmail, invitation.Role);

            await auditService.RecordAsync(
                AuditEventTypes.InvitationResent,
                organisationId,
                actorUserId: resendByUserId,
                subjectUserId: null,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = invitation.TargetEmail,
                    ["role"] = invitation.Role,
                    ["invitation_id"] = invitationId.ToString(),
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return new ResendInvitationResponse(
            invitation.Id,
            invitation.TargetEmail,
            invitation.ExpiresAtUtc,
            $"New invitation sent to {invitation.TargetEmail}.");
    }

    protected virtual void EnqueueEmailJob(Guid invitationId, string rawToken, string signature, string email, string role)
    {
        BackgroundJob.Enqueue<InvitationEmailDeliveryJob>(j =>
            j.ExecuteAsync(invitationId, rawToken, signature, email, role, CancellationToken.None));
    }
}
