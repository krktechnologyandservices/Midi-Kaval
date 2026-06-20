import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  QueuedMutation,
  SyncStatus,
  TravelClaimQueuedMutation,
  VisitMutationType,
} from './syncMutationTypes';

const QUEUE_KEY = 'midi-kaval:offline-sync-queue:v1';

function createMutationId(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, char => {
    const random = Math.floor(Math.random() * 16);
    const value = char === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}

export function createLocalDraftKey(): string {
  return createMutationId();
}

export async function readOfflineQueue(): Promise<QueuedMutation[]> {
  const raw = await AsyncStorage.getItem(QUEUE_KEY);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as QueuedMutation[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function writeOfflineQueue(mutations: QueuedMutation[]): Promise<void> {
  if (mutations.length === 0) {
    await AsyncStorage.removeItem(QUEUE_KEY);
    return;
  }

  await AsyncStorage.setItem(QUEUE_KEY, JSON.stringify(mutations));
}

export async function enqueueOfflineMutation(input: {
  type: VisitMutationType;
  visitId: string;
  note?: string;
  noteClientTimestampUtc?: string;
}): Promise<QueuedMutation> {
  const queue = await readOfflineQueue();
  const mutation: QueuedMutation = {
    clientMutationId: createMutationId(),
    type: input.type,
    clientTimestampUtc: new Date().toISOString(),
    visitId: input.visitId,
    note: input.note,
    noteClientTimestampUtc: input.noteClientTimestampUtc,
    syncStatus: 'local',
  };

  queue.push(mutation);
  await writeOfflineQueue(queue);
  return mutation;
}

export async function enqueueTravelClaimDraft(input: {
  localDraftKey?: string;
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
}): Promise<TravelClaimQueuedMutation> {
  const queue = await readOfflineQueue();
  const mutation: TravelClaimQueuedMutation = {
    clientMutationId: createMutationId(),
    type: 'travel.claim.create',
    clientTimestampUtc: new Date().toISOString(),
    localDraftKey: input.localDraftKey ?? createLocalDraftKey(),
    claimDate: input.claimDate,
    startLocation: input.startLocation,
    destination: input.destination,
    transportMode: input.transportMode,
    amount: input.amount,
    autoNumber: input.autoNumber,
    notes: input.notes,
    caseIds: input.caseIds,
    localReceiptUri: input.localReceiptUri,
    receiptFileName: input.receiptFileName,
    receiptContentType: input.receiptContentType,
    syncStatus: 'local',
  };

  queue.push(mutation);
  await writeOfflineQueue(queue);
  return mutation;
}

export async function updateOfflineQueue(
  mutations: QueuedMutation[],
): Promise<void> {
  await writeOfflineQueue(mutations);
}

export async function removeMutationsByClientIds(
  clientMutationIds: string[],
): Promise<QueuedMutation[]> {
  const remove = new Set(clientMutationIds);
  const next = (await readOfflineQueue()).filter(
    item => !remove.has(item.clientMutationId),
  );
  await writeOfflineQueue(next);
  return next;
}

export async function markQueueSyncStatus(
  clientMutationId: string,
  syncStatus: SyncStatus,
  lastError?: string,
): Promise<void> {
  const queue = await readOfflineQueue();
  const next = queue.map(item =>
    item.clientMutationId === clientMutationId
      ? {...item, syncStatus, lastError}
      : item,
  );
  await writeOfflineQueue(next);
}

export async function findTravelDraftByKey(
  localDraftKey: string,
): Promise<TravelClaimQueuedMutation | undefined> {
  const queue = await readOfflineQueue();
  return queue.find(
    (item): item is TravelClaimQueuedMutation =>
      item.type === 'travel.claim.create' && item.localDraftKey === localDraftKey,
  );
}
