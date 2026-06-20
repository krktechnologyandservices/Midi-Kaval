using MidiKaval.Api.Domain.Enums;

namespace MidiKaval.Api.Infrastructure.Cases;

public static class CaseStageTransitionRules
{
    public static readonly CaseStage[] OrderedStages =
    [
        CaseStage.ProcessInitiation,
        CaseStage.MaintainAndDevelopment,
        CaseStage.InterSectoralApproach,
        CaseStage.Rehabilitation,
        CaseStage.Reintegration,
        CaseStage.TerminationExclusion,
    ];

    public static bool TryGetNextStage(CaseStage current, out CaseStage next)
    {
        next = default;
        var index = Array.IndexOf(OrderedStages, current);
        if (index < 0 || index >= OrderedStages.Length - 1)
        {
            return false;
        }

        next = OrderedStages[index + 1];
        return true;
    }

    public static bool IsValidForwardTransition(CaseStage from, CaseStage to)
    {
        if (from == CaseStage.TerminationExclusion)
        {
            return false;
        }

        return TryGetNextStage(from, out var expectedNext) && to == expectedNext;
    }
}
