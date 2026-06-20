namespace MidiKaval.Api.Infrastructure.Visits;

public sealed class VisitProximityGrouper
{
    public const double MaxClusterDistanceKm = 3.0;

    public VisitProximityGrouperResult Group(IReadOnlyList<VisitGroupingPoint> eligibleVisits)
    {
        if (eligibleVisits.Count == 0)
        {
            return new VisitProximityGrouperResult([], []);
        }

        var clusters = BuildClusters(eligibleVisits);
        var orderedClusters = clusters
            .Select((members, index) => new { members, index })
            .OrderBy(c => CentroidLatitude(c.members))
            .ThenBy(c => CentroidLongitude(c.members))
            .ThenBy(c => c.index)
            .Select(c => c.members)
            .ToList();

        var suggestedOrder = BuildSuggestedOrder(orderedClusters, eligibleVisits);
        return new VisitProximityGrouperResult(orderedClusters, suggestedOrder);
    }

    private static List<List<VisitGroupingPoint>> BuildClusters(IReadOnlyList<VisitGroupingPoint> visits)
    {
        var clusters = visits
            .OrderBy(v => v.ScheduledAtUtc)
            .ThenBy(v => v.VisitId)
            .Select(v => new List<VisitGroupingPoint> { v })
            .ToList();

        var merged = true;
        while (merged)
        {
            merged = false;
            for (var i = 0; i < clusters.Count; i++)
            {
                for (var j = i + 1; j < clusters.Count; j++)
                {
                    if (MinClusterDistance(clusters[i], clusters[j]) > MaxClusterDistanceKm)
                    {
                        continue;
                    }

                    clusters[i].AddRange(clusters[j]);
                    clusters.RemoveAt(j);
                    merged = true;
                    break;
                }

                if (merged)
                {
                    break;
                }
            }
        }

        return clusters;
    }

    private static double MinClusterDistance(
        IReadOnlyList<VisitGroupingPoint> left,
        IReadOnlyList<VisitGroupingPoint> right)
    {
        var min = double.MaxValue;
        foreach (var a in left)
        {
            foreach (var b in right)
            {
                var distance = GeoDistance.DistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                if (distance < min)
                {
                    min = distance;
                }
            }
        }

        return min;
    }

    private static List<Guid> BuildSuggestedOrder(
        IReadOnlyList<List<VisitGroupingPoint>> orderedClusters,
        IReadOnlyList<VisitGroupingPoint> eligibleVisits)
    {
        if (eligibleVisits.Count == 0)
        {
            return [];
        }

        var start = eligibleVisits
            .OrderBy(v => v.ScheduledAtUtc)
            .ThenBy(v => v.VisitId)
            .First();

        var byId = eligibleVisits.ToDictionary(v => v.VisitId);
        var remaining = new HashSet<Guid>(byId.Keys);
        remaining.Remove(start.VisitId);

        var order = new List<Guid> { start.VisitId };

        while (remaining.Count > 0)
        {
            var current = byId[order[^1]];
            var next = remaining
                .Select(id => byId[id])
                .OrderBy(v => GeoDistance.DistanceKm(
                    current.Latitude,
                    current.Longitude,
                    v.Latitude,
                    v.Longitude))
                .ThenBy(v => v.ScheduledAtUtc)
                .ThenBy(v => v.VisitId)
                .First();

            order.Add(next.VisitId);
            remaining.Remove(next.VisitId);
        }

        return order;
    }

    private static double CentroidLatitude(IReadOnlyList<VisitGroupingPoint> members) =>
        members.Average(m => m.Latitude);

    private static double CentroidLongitude(IReadOnlyList<VisitGroupingPoint> members) =>
        members.Average(m => m.Longitude);
}

public sealed record VisitGroupingPoint(
    Guid VisitId,
    double Latitude,
    double Longitude,
    DateTime ScheduledAtUtc);

public sealed record VisitProximityGrouperResult(
    IReadOnlyList<IReadOnlyList<VisitGroupingPoint>> Clusters,
    IReadOnlyList<Guid> SuggestedVisitOrder);
