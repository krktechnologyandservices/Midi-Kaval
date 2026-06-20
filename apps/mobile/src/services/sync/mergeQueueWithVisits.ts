import {VisitListItemDto} from '../visits/visit.models';
import {isVisitMutation, QueuedMutation} from './syncMutationTypes';

function applyMutationToVisit(
  visit: VisitListItemDto,
  mutation: QueuedMutation,
): VisitListItemDto {
  if (mutation.type === 'visit.start') {
    return {
      ...visit,
      status: 'InProgress',
      startedAtUtc: mutation.clientTimestampUtc,
    };
  }

  return {
    ...visit,
    status: 'Completed',
    completedAtUtc: mutation.noteClientTimestampUtc ?? mutation.clientTimestampUtc,
    completionNote: mutation.note ?? visit.completionNote,
  };
}

export function mergeQueueWithVisits(
  items: VisitListItemDto[],
  queue: QueuedMutation[],
): VisitListItemDto[] {
  const activeQueue = queue.filter(
    mutation =>
      isVisitMutation(mutation) &&
      (mutation.syncStatus === 'local' || mutation.syncStatus === 'pending'),
  );

  if (!activeQueue.length) {
    return items;
  }

  const byVisitId = new Map<string, QueuedMutation[]>();
  for (const mutation of activeQueue) {
    const existing = byVisitId.get(mutation.visitId) ?? [];
    existing.push(mutation);
    byVisitId.set(mutation.visitId, existing);
  }

  return items.map(item => {
    if (!item.id) {
      return item;
    }

    const mutations = byVisitId.get(item.id);
    if (!mutations?.length) {
      return item;
    }

    return mutations.reduce(
      (visit, mutation) => applyMutationToVisit(visit, mutation),
      item,
    );
  });
}

export function buildOptimisticStartedVisit(
  visit: VisitListItemDto,
): VisitListItemDto {
  return {
    ...visit,
    status: 'InProgress',
    startedAtUtc: new Date().toISOString(),
  };
}

export function buildOptimisticCompletedVisit(
  visit: VisitListItemDto,
  note: string,
): VisitListItemDto {
  const completedAtUtc = new Date().toISOString();
  return {
    ...visit,
    status: 'Completed',
    completedAtUtc,
    completionNote: note,
  };
}
