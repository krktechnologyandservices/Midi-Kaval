import {TravelClaimDto} from '../travel/travel.models';
import {isTravelClaimMutation, QueuedMutation} from './syncMutationTypes';

export type TravelClaimListItem = TravelClaimDto & {
  localDraftKey?: string;
  isLocalOnly?: boolean;
};

function queuedToListItem(mutation: QueuedMutation): TravelClaimListItem | null {
  if (!isTravelClaimMutation(mutation)) {
    return null;
  }

  if (mutation.syncStatus !== 'local' && mutation.syncStatus !== 'pending') {
    return null;
  }

  return {
    id: undefined,
    localDraftKey: mutation.localDraftKey,
    isLocalOnly: true,
    claimDate: mutation.claimDate,
    startLocation: mutation.startLocation,
    destination: mutation.destination,
    transportMode: mutation.transportMode,
    amount: mutation.amount,
    autoNumber: mutation.autoNumber,
    notes: mutation.notes,
    status: 'Draft',
    caseIds: mutation.caseIds,
    attachments: [],
  };
}

export function mergeQueueWithTravelClaims(
  items: TravelClaimDto[],
  queue: QueuedMutation[],
): TravelClaimListItem[] {
  const localItems = queue
    .map(queuedToListItem)
    .filter((item): item is TravelClaimListItem => item !== null);

  if (!localItems.length) {
    return items;
  }

  return [...localItems, ...items];
}
