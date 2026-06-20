export type SyncStatus = 'local' | 'pending' | 'error';

export type VisitQueuedMutation = {
  clientMutationId: string;
  type: 'visit.start' | 'visit.complete';
  clientTimestampUtc: string;
  visitId: string;
  note?: string;
  noteClientTimestampUtc?: string;
  syncStatus: SyncStatus;
  lastError?: string;
};

export type TravelClaimQueuedMutation = {
  clientMutationId: string;
  type: 'travel.claim.create';
  clientTimestampUtc: string;
  localDraftKey: string;
  claimDate: string;
  startLocation: string;
  destination: string;
  transportMode: string;
  amount: number;
  autoNumber?: string | null;
  notes?: string | null;
  caseIds: string[];
  localReceiptUri?: string;
  receiptFileName?: string;
  receiptContentType?: string;
  syncStatus: SyncStatus;
  lastError?: string;
};

export type QueuedMutation = VisitQueuedMutation | TravelClaimQueuedMutation;

export type VisitMutationType = VisitQueuedMutation['type'];

export function isVisitMutation(mutation: QueuedMutation): mutation is VisitQueuedMutation {
  return mutation.type === 'visit.start' || mutation.type === 'visit.complete';
}

export function isTravelClaimMutation(
  mutation: QueuedMutation,
): mutation is TravelClaimQueuedMutation {
  return mutation.type === 'travel.claim.create';
}

export type SyncMutationResult = {
  clientMutationId: string;
  status: 'applied' | 'duplicate' | 'rejected';
  serverMessage?: string | null;
  visit?: Record<string, unknown> | null;
  travelClaim?: Record<string, unknown> | null;
};

export type SyncPushResult = {
  results: SyncMutationResult[];
};
