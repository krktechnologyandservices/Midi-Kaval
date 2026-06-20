import {QueuedMutation, isTravelClaimMutation} from './syncMutationTypes';
import {
  SyncChipPresentation,
  SYNC_CHIP_LABELS,
  presentationForState,
} from './resolveVisitSyncChip';

const STATUS_PRIORITY = ['error', 'pending', 'local'] as const;

export function resolveTravelSyncChip(
  localDraftKey: string | undefined,
  queue: QueuedMutation[],
): SyncChipPresentation {
  if (!localDraftKey) {
    return presentationForState('synced');
  }

  const forDraft = queue.filter(
    mutation =>
      isTravelClaimMutation(mutation) && mutation.localDraftKey === localDraftKey,
  );

  if (!forDraft.length) {
    return presentationForState('synced');
  }

  for (const status of STATUS_PRIORITY) {
    if (forDraft.some(mutation => mutation.syncStatus === status)) {
      return presentationForState(status);
    }
  }

  return presentationForState('local');
}

export {SYNC_CHIP_LABELS};
