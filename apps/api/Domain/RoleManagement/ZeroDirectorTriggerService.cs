using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;

namespace MidiKaval.Api.Domain.RoleManagement;

public sealed record LastDirectorInfo(string? Name, DateTime? LastActiveAt);

/// <summary>
/// Self-contained service that other endpoints call AFTER completing user
/// suspension/deletion/role-change. Detects zero-Director state and triggers
/// the recovery flow (email alert + dashboard indicator).
///
/// Story 1.13 creates the trigger infrastructure but the event-driven
/// detection (AC-1) won't fire until Story 2-13 (suspension) and
/// 2-14 (deletion) wire in calls to <see cref="NotifyUserRemovedAsync"/>.
/// For Story 1.13, only the monitoring fallback (AC-4) provides the safety net.
/// </summary>
public sealed class ZeroDirectorTriggerService(
    AppDbContext db,
    LastDirectorGuard lastDirectorGuard,
    ILogger<ZeroDirectorTriggerService> logger)
{
    /// <summary>
    /// Called after a user is removed (suspended, deleted, role-changed away from Director).
    /// If the removed user was a Director and no active Directors remain, triggers recovery.
    /// </summary>
    public async Task NotifyUserRemovedAsync(Guid organisationId, Guid userId, CancellationToken ct)
    {
        // 1. Load the removed user — if they were NOT a Director, return immediately (no-op)
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.Role != UserRoles.Director)
            return;

        // 2. Check if any active Directors remain
        var hasActiveDirector = await lastDirectorGuard.HasAnyActiveDirectorAsync(organisationId, ct);
        if (hasActiveDirector)
            return;

        // 3. No active Directors remain — trigger recovery in a single DB transaction
        var org = await db.Organisations.FindAsync(new object[] { organisationId }, ct);
        if (org is null)
            return;

        org.HasPendingRecovery = true;
        await db.SaveChangesAsync(ct);

        // 4. Enqueue Hangfire fire-and-forget job
        BackgroundJob.Enqueue<ZeroDirectorAlertJob>(j => j.ExecuteAsync(organisationId, CancellationToken.None));

        logger.LogWarning(
            "Zero-Director state detected for organisation {OrganisationId}. Recovery triggered.",
            organisationId);
    }
}
