using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models.Admin;
using MidiKaval.Api.Models.Audit;

namespace MidiKaval.Api.Domain.RoleManagement;

public class UserManagementService(
    AppDbContext db,
    IAuditService auditService,
    LastDirectorGuard lastDirectorGuard,
    ZeroDirectorTriggerService zeroDirectorTrigger,
    ILogger<UserManagementService> logger)
{
    public async Task<AdminUserListResult> GetUserListAsync(
        Guid organisationId,
        int page = 1,
        int pageSize = 25,
        string? searchTerm = null,
        string? roles = null,
        string? statusFilter = null,
        string? sortBy = null,
        bool sortDescending = true,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<User> query = db.Users
            .AsNoTracking()
            .Where(u => u.OrganisationId == organisationId);

        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = EscapeLikePattern(searchTerm);
            query = ApplySearchFilter(query, term);
        }

        // Role filter (comma-separated, case-insensitive)
        if (!string.IsNullOrWhiteSpace(roles))
        {
            var roleList = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roleList.Length > 0)
            {
                var loweredRoles = roleList.Select(r => r.ToLowerInvariant()).ToList();
                query = query.Where(u => loweredRoles.Contains(u.Role.ToLower()));
            }
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = statusFilter.ToLowerInvariant() switch
            {
                "active" => query.Where(u => u.IsActive && !u.IsSuspended && !u.Email.StartsWith("deleted-")),
                "suspended" => query.Where(u => u.IsSuspended),
                "deleted" => query.Where(u => !u.IsActive && u.Email.StartsWith("deleted-")),
                var unknown => throw new ArgumentOutOfRangeException(
                    nameof(statusFilter),
                    $"Unknown status filter: '{unknown}'. Expected 'active', 'suspended', or 'deleted'.")
            };
        }

        // Dynamic sort with tiebreaker for deterministic pagination
        IOrderedQueryable<User> orderedQuery = (sortBy, sortDescending) switch
        {
            ("name", false) => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id),
            ("name", true) => query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName).ThenBy(u => u.Id),
            ("email", false) => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            ("email", true) => query.OrderByDescending(u => u.Email).ThenBy(u => u.Id),
            ("role", false) => query.OrderBy(u => u.Role).ThenBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("role", true) => query.OrderByDescending(u => u.Role).ThenByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("status", false) => query.OrderBy(u => u.IsSuspended).ThenBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("status", true) => query.OrderByDescending(u => u.IsSuspended).ThenByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            _ when sortDescending => query.OrderByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            _ => query.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
        };

        var totalCount = await orderedQuery.CountAsync(ct);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummary(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.IsActive,
                u.IsSuspended,
                u.SuspendedAtUtc,
                u.CreatedAtUtc))
            .ToListAsync(ct);

        return new AdminUserListResult(items, totalCount, page, pageSize);
    }

    private static string GetDisplayName(string? firstName, string? lastName)
    {
        var combined = $"{firstName ?? ""} {lastName ?? ""}".Trim();
        return string.IsNullOrWhiteSpace(combined) ? "User" : combined;
    }

    public async Task<SuspendUserResponse> SuspendAsync(
        Guid organisationId,
        Guid actorUserId,
        Guid targetUserId,
        string? reason,
        string? actorIpAddress = null,
        CancellationToken ct = default)
    {
        if (targetUserId == actorUserId)
            throw new InvalidOperationException("You cannot suspend your own account. Another Director must perform this action.");

        var now = DateTime.UtcNow;
        User user;

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (user.IsSuspended)
                throw new InvalidOperationException("User is already suspended.");

            if (!user.IsActive && user.Email.StartsWith("deleted-"))
                throw new InvalidOperationException("Cannot suspend a deleted user.");

            var isLastDirector = await lastDirectorGuard.IsLastActiveDirectorAsync(organisationId, targetUserId, ct);
            if (isLastDirector)
                throw new InvalidOperationException("Cannot deactivate — no other active Director remains in the organisation.");

            // Snapshot identity before mutation
            var snapshot = new TargetUserSnapshotDto(
                user.Email,
                GetDisplayName(user.FirstName, user.LastName),
                user.Role);

            user.IsSuspended = true;
            user.SuspendedAtUtc = now;
            user.TokenVersion++;
            user.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.UserSuspended,
                organisationId,
                actorUserId: actorUserId,
                subjectUserId: targetUserId,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["reason"] = reason,
                    ["target_email"] = user.Email,
                    ["target_name"] = GetDisplayName(user.FirstName, user.LastName),
                    ["target_role"] = user.Role,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            try { await NotifyUserRemovedAsync(organisationId, targetUserId, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Zero-Director detection failed after suspending user {TargetUserId}.", targetUserId);
            }

            EnqueueStatusEmailJob(user.Id, organisationId, user.Email, GetDisplayName(user.FirstName, user.LastName), "suspended", reason, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "Failed to rollback transaction for user suspension {TargetUserId}.", targetUserId);
            }
            throw;
        }

        return new SuspendUserResponse(user.Id, true, now, "User has been suspended.");
    }

    public async Task<ReactivateUserResponse> ReactivateAsync(
        Guid organisationId,
        Guid actorUserId,
        Guid targetUserId,
        string? actorIpAddress = null,
        CancellationToken ct = default)
    {
        if (targetUserId == actorUserId)
            throw new InvalidOperationException("You cannot reactivate your own account. Another Director must perform this action.");

        var now = DateTime.UtcNow;
        User user;

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (!user.IsSuspended)
                throw new InvalidOperationException("User is not suspended.");

            if (!user.IsActive && user.Email.StartsWith("deleted-"))
                throw new InvalidOperationException("Cannot reactivate a deleted user.");

            // Snapshot identity before mutation
            var snapshot = new TargetUserSnapshotDto(
                user.Email,
                GetDisplayName(user.FirstName, user.LastName),
                user.Role);

            user.IsSuspended = false;
            user.SuspendedAtUtc = null;
            user.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.UserReactivated,
                organisationId,
                actorUserId: actorUserId,
                subjectUserId: targetUserId,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = user.Email,
                    ["target_name"] = GetDisplayName(user.FirstName, user.LastName),
                    ["target_role"] = user.Role,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            EnqueueStatusEmailJob(user.Id, organisationId, user.Email, GetDisplayName(user.FirstName, user.LastName), "reactivated", null, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "Failed to rollback transaction for user reactivation {TargetUserId}.", targetUserId);
            }
            throw;
        }

        return new ReactivateUserResponse(user.Id, false, now, "User has been reactivated.");
    }

    public async Task<DeleteUserResponse> DeleteAsync(
        Guid organisationId,
        Guid actorUserId,
        Guid targetUserId,
        string confirmationEmail,
        string? actorIpAddress = null,
        CancellationToken ct = default)
    {
        if (targetUserId == actorUserId)
            throw new InvalidOperationException("You cannot delete your own account. Another Director must perform this action.");

        var now = DateTime.UtcNow;
        User user;

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (!user.IsActive && user.Email.StartsWith("deleted-"))
                throw new InvalidOperationException("User has already been deleted.");

            if (!string.Equals(confirmationEmail, user.Email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Email confirmation does not match.");

            var isLastDirector = await lastDirectorGuard.IsLastActiveDirectorAsync(organisationId, targetUserId, ct);
            if (isLastDirector)
                throw new InvalidOperationException("Cannot deactivate — no other active Director remains in the organisation.");

            // Snapshot identity before anonymisation
            var snapshotEmail = user.Email;
            var snapshotRole = user.Role;
            var snapshotName = GetDisplayName(user.FirstName, user.LastName);
            var snapshot = new TargetUserSnapshotDto(snapshotEmail, snapshotName, snapshotRole);

            // Anonymise the user
            user.FirstName = "Deleted";
            user.LastName = "User";
            user.Email = $"deleted-{Guid.NewGuid():N}@anonymised.local";
            user.PasswordHash = string.Empty;
            user.IsSuspended = false;
            user.IsActive = false;
            user.PhoneNumber = null;
            user.TotpSecret = null;
            user.TotpEnrolledAt = null;
            user.SuspendedAtUtc = null;
            user.TokenVersion++;
            user.UpdatedAtUtc = now;

            // Invalidate any outstanding email-confirmation links for this user. Without this,
            // an old confirmation email still sitting in someone's inbox from before this user
            // was deleted stays clickable — it silently reactivates the now-anonymised account
            // instead of erroring, which is especially confusing if the same email address is
            // later re-invited under a new account (the stale link has nothing to do with it).
            var outstandingConfirmationTokens = await db.ConfirmationTokens
                .Where(t => t.UserId == targetUserId && t.ConsumedAtUtc == null)
                .ToListAsync(ct);
            foreach (var token in outstandingConfirmationTokens)
            {
                token.ExpiresAtUtc = now;
            }

            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.UserDeleted,
                organisationId,
                actorUserId: actorUserId,
                subjectUserId: targetUserId,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = snapshotEmail,
                    ["target_name"] = snapshotName,
                    ["target_role"] = snapshotRole,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            try { await NotifyUserRemovedAsync(organisationId, targetUserId, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Zero-Director detection failed after deleting user {TargetUserId}.", targetUserId);
            }

            EnqueueDeletionEmailJob(targetUserId, organisationId, snapshotEmail, snapshotName, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "Failed to rollback transaction for user deletion {TargetUserId}.", targetUserId);
            }
            throw;
        }

        return new DeleteUserResponse(user.Id, now, "User has been permanently deleted.");
    }

    protected virtual void EnqueueStatusEmailJob(Guid userId, Guid organisationId, string email, string name, string actionType, string? reason, CancellationToken ct)
    {
        BackgroundJob.Enqueue<UserStatusEmailJob>(j =>
            j.ExecuteAsync(userId, organisationId, email, name, actionType, reason, CancellationToken.None));
    }

    protected virtual void EnqueueDeletionEmailJob(Guid userId, Guid organisationId, string originalEmail, string originalName, CancellationToken ct)
    {
        BackgroundJob.Enqueue<UserStatusEmailJob>(j =>
            j.ExecuteAsync(userId, organisationId, originalEmail, originalName, "deleted", null, CancellationToken.None));
    }

    protected virtual Task NotifyUserRemovedAsync(Guid organisationId, Guid userId, CancellationToken ct)
        => zeroDirectorTrigger.NotifyUserRemovedAsync(organisationId, userId, ct);

    protected virtual IQueryable<User> ApplySearchFilter(IQueryable<User> query, string term)
    {
        return query.Where(u =>
            EF.Functions.ILike(u.Email, $"%{term}%", @"\") ||
            EF.Functions.ILike(u.FirstName + " " + u.LastName, $"%{term}%", @"\"));
    }

    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
