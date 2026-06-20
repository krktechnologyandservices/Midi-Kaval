using MidiKaval.Api.Infrastructure.Sync;

namespace MidiKaval.Api.UnitTests;

public class VisitSyncNoteMergeTests
{
    [Fact]
    public void ShouldMergeNote_WhenClientTimestampIsNewer_ReturnsTrue()
    {
        var existing = new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc);
        var client = existing.AddMinutes(5);

        Assert.True(VisitSyncNoteMerge.ShouldMergeNote(existing, client));
    }

    [Fact]
    public void ShouldMergeNote_WhenClientTimestampIsOlder_ReturnsFalse()
    {
        var existing = new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc);
        var client = existing.AddMinutes(-5);

        Assert.False(VisitSyncNoteMerge.ShouldMergeNote(existing, client));
    }

    [Fact]
    public void ShouldMergeNote_WhenTimestampsEqual_ReturnsFalse()
    {
        var existing = new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc);

        Assert.False(VisitSyncNoteMerge.ShouldMergeNote(existing, existing));
    }
}
