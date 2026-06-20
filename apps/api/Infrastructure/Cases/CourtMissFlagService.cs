using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CourtMissFlagService(AppDbContext db)
{
    public async Task RecalculateCaseCourtMissFlagAsync(
        Guid caseId,
        Guid organisationId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var hasQualifyingSitting = await db.CourtSittings.AnyAsync(
            s => s.CaseId == caseId
                && s.OrganisationId == organisationId
                && s.Status == CourtSittingStatus.Upcoming
                && s.ScheduledAtUtc < now
                && s.MissEscalatedAtUtc != null,
            cancellationToken);

        var caseEntity = await db.Cases.SingleAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        caseEntity.CourtMissFlaggedAtUtc = hasQualifyingSitting
            ? caseEntity.CourtMissFlaggedAtUtc ?? now
            : null;
    }

    public static void SetCaseFlagOnEscalation(Domain.Entities.Case caseEntity, DateTime nowUtc)
    {
        if (caseEntity.CourtMissFlaggedAtUtc is null)
        {
            caseEntity.CourtMissFlaggedAtUtc = nowUtc;
        }
    }
}
