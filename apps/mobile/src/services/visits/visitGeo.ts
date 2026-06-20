const EARTH_RADIUS_KM = 6371;

function degreesToRadians(degrees: number): number {
  return (degrees * Math.PI) / 180;
}

export function distanceKm(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number,
): number {
  const dLat = degreesToRadians(lat2 - lat1);
  const dLon = degreesToRadians(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(degreesToRadians(lat1)) *
      Math.cos(degreesToRadians(lat2)) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return EARTH_RADIUS_KM * c;
}

export function roundKmOneDecimal(distanceKm: number): number {
  return Math.round(distanceKm * 10) / 10;
}

export function formatDistanceKm(distanceKm: number | null | undefined): string | null {
  if (distanceKm === null || distanceKm === undefined) {
    return null;
  }

  return `${roundKmOneDecimal(distanceKm).toFixed(1)} km`;
}
