import React from 'react';
import {Pressable, Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {SyncQueueScreen} from '../src/screens/more/SyncQueueScreen';
import * as commandStripCache from '../src/services/visits/commandStripCache';
import * as offlineQueue from '../src/services/sync/offlineQueue';
import * as mobileSyncPushService from '../src/services/sync/mobileSyncPushService';

const focusCallbacks: Array<() => void> = [];

jest.mock('@react-navigation/native', () => {
  const React = require('react');
  return {
    useFocusEffect: (callback: () => void | (() => void)) => {
      React.useEffect(() => {
        focusCallbacks.push(callback);
        return callback();
      }, [callback]);
    },
  };
});

jest.mock('../src/services/visits/commandStripCache', () => ({
  readCache: jest.fn(),
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn(),
}));

jest.mock('../src/services/sync/mobileSyncPushService', () => ({
  flushOfflineQueue: jest.fn(),
}));

const visitId = '11111111-1111-4111-8111-111111111111';

beforeEach(() => {
  jest.clearAllMocks();
  focusCallbacks.length = 0;
  (commandStripCache.readCache as jest.Mock).mockResolvedValue({
    items: [
      {
        id: visitId,
        case: {crimeNumber: 'CR-99', stNumber: 'ST-99'},
      },
    ],
  });
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([]);
  (mobileSyncPushService.flushOfflineQueue as jest.Mock).mockResolvedValue({
    appliedVisits: new Map(),
    remaining: [],
  });
});

test('sync queue screen shows empty state when queue is empty', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<SyncQueueScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('All queued changes are synced.');
});

test('sync queue screen lists error mutation with lastError', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId,
      syncStatus: 'error',
      lastError: 'Conflict with server state',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<SyncQueueScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('CR-99');
  expect(combined).toContain('ST-99');
  expect(combined).toContain('Start visit');
  expect(combined).toContain('Sync failed');
  expect(combined).toContain('Conflict with server state');
});

test('sync queue screen shows fallback when error has no lastError', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId,
      syncStatus: 'error',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<SyncQueueScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Sync failed — try again.');
});

test('retry sync invokes flushOfflineQueue and refreshes queue', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock)
    .mockResolvedValueOnce([
      {
        clientMutationId: 'mut-1',
        type: 'visit.complete',
        clientTimestampUtc: '2026-06-17T10:00:00Z',
        visitId,
        syncStatus: 'pending',
      },
    ])
    .mockResolvedValueOnce([]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<SyncQueueScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const retryButton = tree.root.findByProps({accessibilityLabel: 'Retry sync'});
  await ReactTestRenderer.act(async () => {
    retryButton.props.onPress();
    await Promise.resolve();
    await Promise.resolve();
  });

  expect(mobileSyncPushService.flushOfflineQueue).toHaveBeenCalled();
  expect(offlineQueue.readOfflineQueue).toHaveBeenCalledTimes(2);
});

test('sync queue refreshes on focus', async () => {
  await ReactTestRenderer.act(async () => {
    ReactTestRenderer.create(<SyncQueueScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  expect(focusCallbacks.length).toBeGreaterThan(0);
  expect(offlineQueue.readOfflineQueue).toHaveBeenCalled();
});
