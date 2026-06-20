import {QueuedMutation, SyncStatus, isVisitMutation} from './syncMutationTypes';

export const SYNC_CHIP_LABELS = {
  local: 'Saved on this device',
  pending: 'Uploading',
  synced: 'Synced',
  error: 'Sync failed',
} as const;

export type SyncChipState = keyof typeof SYNC_CHIP_LABELS;

export type SyncChipPresentation = {
  state: SyncChipState;
  label: string;
  backgroundColor: string;
  color: string;
};

const SYNC_CHIP_STYLES: Record<
  SyncChipState,
  {backgroundColor: string; color: string}
> = {
  local: {backgroundColor: '#F4F3FF', color: '#5925DC'},
  pending: {backgroundColor: '#FFFAEB', color: '#B54708'},
  synced: {backgroundColor: '#ECFDF3', color: '#027A48'},
  error: {backgroundColor: '#FEF3F2', color: '#B42318'},
};

const STATUS_PRIORITY: SyncStatus[] = ['error', 'pending', 'local'];

export function presentationForState(state: SyncChipState): SyncChipPresentation {
  return {
    state,
    label: SYNC_CHIP_LABELS[state],
    ...SYNC_CHIP_STYLES[state],
  };
}

export function resolveVisitSyncChip(
  visitId: string | undefined,
  queue: QueuedMutation[],
): SyncChipPresentation {
  if (!visitId) {
    return presentationForState('synced');
  }

  const forVisit = queue.filter(
    mutation => isVisitMutation(mutation) && mutation.visitId === visitId,
  );
  if (!forVisit.length) {
    return presentationForState('synced');
  }

  for (const status of STATUS_PRIORITY) {
    if (forVisit.some(mutation => mutation.syncStatus === status)) {
      return presentationForState(status as SyncChipState);
    }
  }

  return presentationForState('local');
}

export function mutationTypeLabel(type: QueuedMutation['type']): string {
  if (type === 'visit.start') {
    return 'Start visit';
  }

  if (type === 'visit.complete') {
    return 'Complete visit';
  }

  return 'Create travel claim';
}

export function resolveVisitTitleFromCache(
  visitId: string,
  items: {id?: string; case?: {crimeNumber?: string; stNumber?: string}}[],
): string {
  const visit = items.find(item => item.id === visitId);
  if (visit?.case) {
    return `${visit.case.crimeNumber ?? '—'} · ${visit.case.stNumber ?? '—'}`;
  }

  return `Visit ${visitId.slice(0, 8)}`;
}
