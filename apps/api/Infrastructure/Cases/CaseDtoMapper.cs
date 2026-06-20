using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public static class CaseDtoMapper
{
    public static CaseSummaryDto ToCaseSummary(Case entity, bool redactPocsoForFieldWorker) => new()
    {
        Id = entity.Id,
        CrimeNumber = entity.CrimeNumber,
        StNumber = entity.StNumber,
        BeneficiaryName = BeneficiaryDisplayFormatter.FormatBeneficiaryName(entity, redactPocsoForFieldWorker),
        CurrentStage = entity.CurrentStage.ToString(),
        TypeOfOffence = entity.TypeOfOffence,
        OffenceClassification = entity.OffenceClassification.ToString(),
        Domicile = entity.Domicile.ToString(),
        VisitCount = entity.VisitCount,
        CreatedByUserId = entity.CreatedByUserId,
        AssignedWorkerUserId = entity.AssignedWorkerId,
        AssignedAtUtc = entity.AssignedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        NextVisitDueAtUtc = entity.NextVisitDueAtUtc,
        GpsVerified = entity.GpsVerified,
        Latitude = entity.Latitude,
        Longitude = entity.Longitude,
        Landmark = entity.Landmark,
        SensitivityLevel = entity.SensitivityLevel.ToString(),
    };
}
