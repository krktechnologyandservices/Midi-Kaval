import {VisitListItemDto} from './visit.models';
import {distanceKm, roundKmOneDecimal} from './visitGeo';

export function applyDisplayOrder(
  items: VisitListItemDto[],
  customVisitOrder: string[] | null | undefined,
): VisitListItemDto[] {
  if (!customVisitOrder?.length) {
    return items;
  }

  const byId = new Map(
    items
      .filter(item => item.id)
      .map(item => [item.id as string, item]),
  );

  const ordered: VisitListItemDto[] = [];
  const seen = new Set<string>();

  for (const visitId of customVisitOrder) {
    const visit = byId.get(visitId);
    if (!visit) {
      continue;
    }

    ordered.push(visit);
    seen.add(visitId);
  }

  const unverifiedTail = items.filter(
    item => item.id && !seen.has(item.id) && !item.case?.gpsVerified,
  );
  const remaining = items.filter(item => item.id && !seen.has(item.id) && item.case?.gpsVerified);

  return [...ordered, ...remaining, ...unverifiedTail];
}

export function buildCustomVisitOrder(orderedVisitIds: string[]): string[] {
  return [...orderedVisitIds];
}

export function buildRouteDistanceMap(
  orderedVisitIds: string[],
  items: VisitListItemDto[],
): Map<string, number | null> {
  const byId = new Map(
    items
      .filter(item => item.id)
      .map(item => [item.id as string, item]),
  );

  const distances = new Map<string, number | null>();

  orderedVisitIds.forEach((visitId, index) => {
    if (index === 0) {
      distances.set(visitId, null);
      return;
    }

    const previous = byId.get(orderedVisitIds[index - 1]);
    const current = byId.get(visitId);
    const previousLat = previous?.case?.latitude;
    const previousLng = previous?.case?.longitude;
    const currentLat = current?.case?.latitude;
    const currentLng = current?.case?.longitude;

    if (
      previousLat === null ||
      previousLat === undefined ||
      previousLng === null ||
      previousLng === undefined ||
      currentLat === null ||
      currentLat === undefined ||
      currentLng === null ||
      currentLng === undefined
    ) {
      distances.set(visitId, null);
      return;
    }

    distances.set(
      visitId,
      roundKmOneDecimal(
        distanceKm(previousLat, previousLng, currentLat, currentLng),
      ),
    );
  });

  return distances;
}
