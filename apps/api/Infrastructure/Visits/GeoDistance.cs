namespace MidiKaval.Api.Infrastructure.Visits;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static double DistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2) =>
        DistanceKm((double)lat1, (double)lon1, (double)lat2, (double)lon2);

    public static double RoundKmOneDecimal(double distanceKm) =>
        Math.Round(distanceKm, 1, MidpointRounding.AwayFromZero);

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
