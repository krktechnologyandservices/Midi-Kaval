import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  enqueueOfflineMutation,
  enqueueTravelClaimDraft,
  readOfflineQueue,
  removeMutationsByClientIds,
} from '../src/services/sync/offlineQueue';

const storage: Record<string, string> = {};

jest.mock('@react-native-async-storage/async-storage', () => ({
  getItem: jest.fn((key: string) => Promise.resolve(storage[key] ?? null)),
  setItem: jest.fn((key: string, value: string) => {
    storage[key] = value;
    return Promise.resolve();
  }),
  removeItem: jest.fn((key: string) => {
    delete storage[key];
    return Promise.resolve();
  }),
}));

beforeEach(() => {
  jest.clearAllMocks();
  Object.keys(storage).forEach(key => delete storage[key]);
});

test('enqueueOfflineMutation appends fifo queue entry', async () => {
  const mutation = await enqueueOfflineMutation({
    type: 'visit.start',
    visitId: '11111111-1111-4111-8111-111111111111',
  });

  expect(mutation.syncStatus).toBe('local');
  expect(AsyncStorage.setItem).toHaveBeenCalled();
  const queue = await readOfflineQueue();
  expect(queue).toHaveLength(1);
  expect(queue[0].type).toBe('visit.start');
});

test('removeMutationsByClientIds drops applied items', async () => {
  const first = await enqueueOfflineMutation({
    type: 'visit.start',
    visitId: '11111111-1111-4111-8111-111111111111',
  });
  await enqueueOfflineMutation({
    type: 'visit.complete',
    visitId: '11111111-1111-4111-8111-111111111111',
    note: 'Done',
    noteClientTimestampUtc: '2026-06-17T10:00:00Z',
  });

  const remaining = await removeMutationsByClientIds([first.clientMutationId]);
  expect(remaining).toHaveLength(1);
  expect(remaining[0].type).toBe('visit.complete');
});

test('enqueueTravelClaimDraft appends travel mutation', async () => {
  const mutation = await enqueueTravelClaimDraft({
    claimDate: '2026-06-15T00:00:00Z',
    startLocation: 'Office',
    destination: 'District Court',
    transportMode: 'Bus',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
  });

  expect(mutation.type).toBe('travel.claim.create');
  expect(mutation.localDraftKey).toBeTruthy();
  const queue = await readOfflineQueue();
  expect(queue.some(item => item.type === 'travel.claim.create')).toBe(true);
});
