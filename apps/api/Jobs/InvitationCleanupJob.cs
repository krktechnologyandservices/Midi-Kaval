using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Jobs;

public sealed class InvitationCleanupJob(
    AppDbContext db,
    ILogger<InvitationCleanupJob> logger)
{
    private const int BatchSize = 100;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var expiredCount = 0;
        var processed = 0;

        do
        {
            var expiredBatch = await db.Invitations
                .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAtUtc < DateTime.UtcNow)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            processed = expiredBatch.Count;

            foreach (var invitation in expiredBatch)
            {
                invitation.Status = InvitationStatus.Expired;
            }

            if (processed > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                expiredCount += processed;
            }
        }
        while (processed == BatchSize);

        if (expiredCount > 0)
        {
            logger.LogInformation("Invitation cleanup: {Count} expired invitations marked as expired.", expiredCount);
        }
    }
}
