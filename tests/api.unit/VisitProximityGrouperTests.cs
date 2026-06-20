using MidiKaval.Api.Infrastructure.Visits;

namespace MidiKaval.Api.UnitTests;

public class VisitProximityGrouperTests
{
    private static readonly DateTime BaseTime = new(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Group_PairsWithinThreeKm_FormSingleCluster()
    {
        var visitA = new VisitGroupingPoint(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            12.9716,
            77.5946,
            BaseTime);
        var visitB = new VisitGroupingPoint(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            12.9800,
            77.5946,
            BaseTime.AddHours(1));

        var result = new VisitProximityGrouper().Group([visitA, visitB]);

        Assert.Single(result.Clusters);
        Assert.Equal(2, result.Clusters[0].Count);
        Assert.Equal(2, result.SuggestedVisitOrder.Count);
    }

    [Fact]
    public void Group_VisitsEightKmApart_FormSeparateClusters()
    {
        var visitA = new VisitGroupingPoint(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            12.9716,
            77.5946,
            BaseTime);
        var visitB = new VisitGroupingPoint(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            12.9800,
            77.5946,
            BaseTime.AddHours(1));
        var visitC = new VisitGroupingPoint(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            13.0500,
            77.5946,
            BaseTime.AddHours(2));

        var result = new VisitProximityGrouper().Group([visitA, visitB, visitC]);

        Assert.Equal(2, result.Clusters.Count);
        Assert.Contains(result.Clusters, cluster => cluster.Count == 2);
        Assert.Contains(result.Clusters, cluster => cluster.Count == 1);
        Assert.Equal(visitA.VisitId, result.SuggestedVisitOrder[0]);
        Assert.Equal(visitB.VisitId, result.SuggestedVisitOrder[1]);
        Assert.Equal(visitC.VisitId, result.SuggestedVisitOrder[2]);
    }

    [Fact]
    public void GeoDistance_KnownPoints_ApproximatelyOneKilometer()
    {
        var distance = GeoDistance.DistanceKm(12.9716, 77.5946, 12.9800, 77.5946);
        Assert.InRange(distance, 0.8, 1.2);
    }
}
