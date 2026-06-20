import {authSessionService} from '../src/services/auth/AuthSessionService';
import {flushOfflineQueue} from '../src/services/sync/mobileSyncPushService';
import * as offlineQueue from '../src/services/sync/offlineQueue';
import {setForceOfflineForTests} from '../src/services/sync/networkStatus';

jest.mock('../src/services/auth/AuthSessionService', () => ({
  authSessionService: {
    refreshSession: jest.fn(),
    postApi: jest.fn(),
    extractErrorMessage: jest.fn(() => 'sync failed'),
  },
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn(),
  updateOfflineQueue: jest.fn(),
  removeMutationsByClientIds: jest.fn(),
  markQueueSyncStatus: jest.fn(),
}));

beforeEach(() => {
  jest.clearAllMocks();
  setForceOfflineForTests(false);
});

test('flushOfflineQueue removes applied and duplicate mutations', async () => {
  const mutation = {
    clientMutationId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    type: 'visit.start' as const,
    clientTimestampUtc: '2026-06-17T09:00:00Z',
    visitId: '11111111-1111-4111-8111-111111111111',
    syncStatus: 'local' as const,
  };

  (offlineQueue.readOfflineQueue as jest.Mock)
    .mockResolvedValueOnce([mutation])
    .mockResolvedValueOnce([mutation]);
  (offlineQueue.updateOfflineQueue as jest.Mock).mockResolvedValue(undefined);
  (offlineQueue.removeMutationsByClientIds as jest.Mock).mockResolvedValue([]);
  (authSessionService.refreshSession as jest.Mock).mockResolvedValue(true);
  (authSessionService.postApi as jest.Mock).mockResolvedValue({
    data: {
      results: [{clientMutationId: mutation.clientMutationId, status: 'applied'}],
    },
  });

  const result = await flushOfflineQueue();

  expect(authSessionService.postApi).toHaveBeenCalledWith(
    '/api/v1/sync/push',
    expect.objectContaining({mutations: expect.any(Array)}),
  );
  expect(offlineQueue.removeMutationsByClientIds).toHaveBeenCalledWith([
    mutation.clientMutationId,
  ]);
  expect(result.remaining).toEqual([]);
});

test('flushOfflineQueue keeps duplicate without visit payload', async () => {
  const mutation = {
    clientMutationId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    type: 'visit.start' as const,
    clientTimestampUtc: '2026-06-17T09:00:00Z',
    visitId: '11111111-1111-4111-8111-111111111111',
    syncStatus: 'local' as const,
  };

  (offlineQueue.readOfflineQueue as jest.Mock)
    .mockResolvedValueOnce([mutation])
    .mockResolvedValueOnce([mutation]);
  (offlineQueue.updateOfflineQueue as jest.Mock).mockResolvedValue(undefined);
  (offlineQueue.removeMutationsByClientIds as jest.Mock).mockResolvedValue([mutation]);
  (offlineQueue.markQueueSyncStatus as jest.Mock).mockResolvedValue(undefined);
  (authSessionService.refreshSession as jest.Mock).mockResolvedValue(true);
  (authSessionService.postApi as jest.Mock).mockResolvedValue({
    data: {
      results: [
        {
          clientMutationId: mutation.clientMutationId,
          status: 'duplicate',
          serverMessage: 'Visit is already in progress.',
        },
      ],
    },
  });

  const result = await flushOfflineQueue();

  expect(offlineQueue.removeMutationsByClientIds).toHaveBeenCalledWith([]);
  expect(offlineQueue.markQueueSyncStatus).toHaveBeenCalledWith(
    mutation.clientMutationId,
    'error',
    'Visit is already in progress.',
  );
  expect(result.remaining).toHaveLength(1);
});

test('flushOfflineQueue skips push when offline', async () => {
  setForceOfflineForTests(true);
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T09:00:00Z',
      visitId: '11111111-1111-4111-8111-111111111111',
      syncStatus: 'local',
    },
  ]);

  const result = await flushOfflineQueue();

  expect(authSessionService.postApi).not.toHaveBeenCalled();
  expect(result.remaining).toHaveLength(1);
});
