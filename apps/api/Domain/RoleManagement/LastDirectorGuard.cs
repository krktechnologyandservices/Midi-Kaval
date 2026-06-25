using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Domain.RoleManagement;

public sealed class LastDirectorGuard(AppDbContext db)
{
    /// <summary>
    /// Returns true if the specified user is the only active Director in their organisation.
    /// </summary>
    public async Task<bool> IsLastActiveDirectorAsync(Guid organisationId, Guid userId, CancellationToken ct)
    {
        var directorCount = await db.Users
            .AsNoTracking()
            .CountAsync(u =>
                u.OrganisationId == organisationId
                && u.Role == UserRoles.Director
                && u.IsActive
                && !u.IsSuspended, ct);

    return directorCount == 1
        && await db.Users.AsNoTracking().AnyAsync(u =>
            u.OrganisationId == organisationId
            && u.Id == userId
            && u.Role == UserRoles.Director
            && u.IsActive
            && !u.IsSuspended, ct);
    }

    /// <summary>
    /// Returns true if at least one active Director exists in the organisation.
    /// </summary>
    public async Task<bool> HasAnyActiveDirectorAsync(Guid organisationId, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.OrganisationId == organisationId
                && u.Role == UserRoles.Director
                && u.IsActive
                && !u.IsSuspended, ct);
    }

    /// <summary>
    /// Returns the last known Director name and last active time for zero-Director detection.
    /// Queries audit events for Director-related activity.
    /// Returns null when org has never had a Director (initial bootstrap scenario).
    /// </summary>
    public async Task<LastDirectorInfo?> GetLastKnownDirectorInfoAsync(Guid organisationId, CancellationToken ct)
    {
        // Look for the most recent Director-related audit event
        var lastDirectorEvent = await db.AuditEvents
            .AsNoTracking()
            .Where(e =>
                e.OrganisationId == organisationId
                && (e.EventType == "user_created" || e.EventType == "user_deleted"))
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new
            {
                e.EventType,
                e.CreatedAtUtc,
                ActorUser = e.ActorUser == null ? null : new { e.ActorUser.FirstName, e.ActorUser.LastName },
                SubjectUser = e.SubjectUser == null ? null : new { e.SubjectUser.FirstName, e.SubjectUser.LastName },
            })
            .FirstOrDefaultAsync(ct);

        if (lastDirectorEvent is null)
            return null;

        // Prefer subject user (the user affected by the action) over actor user (who performed it)
        var name = lastDirectorEvent.SubjectUser is not null
            ? $"{lastDirectorEvent.SubjectUser.FirstName} {lastDirectorEvent.SubjectUser.LastName}".Trim()
            : lastDirectorEvent.ActorUser is not null
                ? $"{lastDirectorEvent.ActorUser.FirstName} {lastDirectorEvent.ActorUser.LastName}".Trim()
                : null;

        if (string.IsNullOrWhiteSpace(name))
            name = null;

        return new LastDirectorInfo(name, lastDirectorEvent.CreatedAtUtc);
    }
}
