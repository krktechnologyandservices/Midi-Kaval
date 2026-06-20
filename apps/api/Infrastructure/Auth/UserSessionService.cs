using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class UserSessionService(
    AppDbContext db,
    RefreshTokenStore refreshTokenStore,
    AuthVerifiedStore authVerifiedStore) : IUserSessionService
{
    public async Task InvalidateUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        await refreshTokenStore.RevokeAllForUserAsync(userId, cancellationToken);
        await authVerifiedStore.ClearVerificationAsync(userId, cancellationToken);

        var devices = await db.UserDevices
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);
        if (devices.Count > 0)
        {
            db.UserDevices.RemoveRange(devices);
        }

        user.TokenVersion++;
        user.UpdatedAtUtc = DateTime.UtcNow;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = user.OrganisationId,
            SubjectUserId = userId,
            EventType = AuditEventTypes.SessionInvalidated,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
