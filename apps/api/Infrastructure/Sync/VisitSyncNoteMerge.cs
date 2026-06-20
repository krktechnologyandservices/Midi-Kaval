namespace MidiKaval.Api.Infrastructure.Sync;

public static class VisitSyncNoteMerge
{
    public static bool ShouldMergeNote(DateTime existingCreatedAtUtc, DateTime noteClientTimestampUtc) =>
        noteClientTimestampUtc > existingCreatedAtUtc;
}
