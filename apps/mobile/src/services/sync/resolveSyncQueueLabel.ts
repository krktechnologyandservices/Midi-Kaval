import {QueuedMutation, isTravelClaimMutation, isVisitMutation} from './syncMutationTypes';

export function mutationTypeLabel(type: QueuedMutation['type']): string {
  if (type === 'visit.start') {
    return 'Start visit';
  }

  if (type === 'visit.complete') {
    return 'Complete visit';
  }

  return 'Create travel claim';
}

export function resolveSyncQueueRowTitle(
  item: QueuedMutation,
  cacheTitles: {id?: string; case?: {crimeNumber?: string; stNumber?: string}}[],
): string {
  if (isVisitMutation(item)) {
    const visit = cacheTitles.find(entry => entry.id === item.visitId);
    if (visit?.case) {
      return `${visit.case.crimeNumber ?? '—'} · ${visit.case.stNumber ?? '—'}`;
    }

    return `Visit ${item.visitId.slice(0, 8)}`;
  }

  if (isTravelClaimMutation(item)) {
    const date = item.claimDate?.slice(0, 10) ?? '—';
    return `${item.destination} · ${date}`;
  }

  return 'Queued item';
}
