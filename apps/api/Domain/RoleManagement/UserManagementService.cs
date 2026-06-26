using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models.Admin;

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

    public async Task<SuspendUserResponse> SuspendAsync(
        Guid organisationId,
        Guid actorUserId,
        Guid targetUserId,
        string? reason,
        CancellationToken ct)
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
                metadata: new Dictionary<string, object?>
                {
                    ["reason"] = reason,
                    ["target_email"] = user.Email,
                    ["target_name"] = $"{user.FirstName} {user.LastName}".Trim(),
                    ["target_role"] = user.Role,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            try { await NotifyUserRemovedAsync(organisationId, targetUserId, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Zero-Director detection failed after suspending user {TargetUserId}.", targetUserId);
            }

            EnqueueStatusEmailJob(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), "suspended", reason, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(ct); }
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
        CancellationToken ct)
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

            user.IsSuspended = false;
            user.SuspendedAtUtc = null;
            user.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.UserReactivated,
                organisationId,
                actorUserId: actorUserId,
                subjectUserId: targetUserId,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = user.Email,
                    ["target_name"] = $"{user.FirstName} {user.LastName}".Trim(),
                    ["target_role"] = user.Role,
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            EnqueueStatusEmailJob(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), "reactivated", null, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(ct); }
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
        CancellationToken ct)
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
            var snapshotFirstName = user.FirstName;
            var snapshotLastName = user.LastName;
            var snapshotEmail = user.Email;
            var snapshotRole = user.Role;
            var snapshotName = $"{snapshotFirstName} {snapshotLastName}".Trim();

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
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.UserDeleted,
                organisationId,
                actorUserId: actorUserId,
                subjectUserId: null,
                metadata: new Dictionary<string, object?>
                {
                    ["target_email"] = snapshotEmail,
                    ["target_name"] = snapshotName,
                    ["target_role"] = snapshotRole,
                    ["target_user_snapshot"] = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        firstName = snapshotFirstName,
                        lastName = snapshotLastName,
                        email = snapshotEmail,
                        role = snapshotRole,
                    }),
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            try { await NotifyUserRemovedAsync(organisationId, targetUserId, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Zero-Director detection failed after deleting user {TargetUserId}.", targetUserId);
            }

            EnqueueDeletionEmailJob(snapshotEmail, snapshotName, ct);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
        {
            try { await transaction.RollbackAsync(ct); }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "Failed to rollback transaction for user deletion {TargetUserId}.", targetUserId);
            }
            throw;
        }

        return new DeleteUserResponse(user.Id, now, "User has been permanently deleted.");
    }

    protected virtual void EnqueueStatusEmailJob(Guid userId, string email, string name, string actionType, string? reason, CancellationToken ct)
    {
        BackgroundJob.Enqueue<UserStatusEmailJob>(j =>
            j.ExecuteAsync(userId, email, name, actionType, reason, ct));
    }

    protected virtual void EnqueueDeletionEmailJob(string originalEmail, string originalName, CancellationToken ct)
    {
        BackgroundJob.Enqueue<UserStatusEmailJob>(j =>
            j.ExecuteAsync(Guid.Empty, originalEmail, originalName, "deleted", null, ct));
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
