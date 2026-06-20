import React from 'react';
import {Linking, Pressable, RefreshControl, Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {TodayScreen} from '../src/screens/today/TodayScreen';
import * as commandStripCache from '../src/services/visits/commandStripCache';
import {visitApiService} from '../src/services/visits/VisitApiService';
import {openGoogleMaps} from '../src/services/visits/visitNavigation';

jest.mock('../src/services/visits/visitNavigation', () => ({
  openGoogleMaps: jest.fn(() => Promise.resolve(true)),
  buildGoogleMapsUrl: jest.fn(
    (lat: number, lng: number) =>
      `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`,
  ),
}));

const mockNavigate = jest.fn();
const mockParentNavigate = jest.fn();

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({
    navigate: mockNavigate,
    getParent: () => ({
      navigate: mockParentNavigate,
    }),
  }),
  useRoute: () => ({
    params: undefined,
  }),
  useFocusEffect: (callback: () => void) => {
    callback();
  },
}));

jest.mock('../src/context/AuthContext', () => ({
  useAuth: () => ({
    isFieldRole: true,
    isSupervisorRole: false,
    user: {email: 'worker@test', role: 'SocialWorker'},
  }),
}));

jest.mock('../src/hooks/useCourtCountdown', () => ({
  useCourtCountdown: () => ({
    label: null,
    refreshCourtCountdown: jest.fn(),
  }),
}));

jest.mock('../src/services/visits/VisitApiService', () => ({
  visitApiService: {
    listToday: jest.fn(),
    startVisit: jest.fn(),
    getTodayGroupingSuggestion: jest.fn(),
    extractErrorMessage: jest.fn(() => 'error'),
  },
}));

jest.mock('../src/services/visits/commandStripCache', () => ({
  readCache: jest.fn(),
  writeCache: jest.fn(),
  isStale: jest.fn(() => false),
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn().mockResolvedValue([]),
}));

import * as offlineQueue from '../src/services/sync/offlineQueue';

jest.mock('../src/services/sync/mobileSyncPushService', () => ({
  flushOfflineQueue: jest.fn().mockResolvedValue({
    appliedVisits: new Map(),
    remaining: [],
  }),
}));

jest.mock('../src/services/sync/useSyncOnForeground', () => ({
  useSyncOnForeground: jest.fn(),
}));

const sampleVisit = {
  id: '11111111-1111-4111-8111-111111111111',
  scheduledAtUtc: '2026-06-16T09:00:00Z',
  status: 'Scheduled',
  isOverdue: true,
  case: {
    id: '22222222-2222-4222-8222-222222222222',
    crimeNumber: 'CR-99',
    stNumber: 'ST-99',
    domicile: 'Urban',
    gpsVerified: false,
  },
};

beforeEach(() => {
  jest.clearAllMocks();
  (commandStripCache.readCache as jest.Mock).mockResolvedValue(null);
  (commandStripCache.writeCache as jest.Mock).mockResolvedValue(undefined);
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [sampleVisit],
    page: 1,
    pageSize: 1,
  });
  (visitApiService.startVisit as jest.Mock).mockResolvedValue({
    ...sampleVisit,
    status: 'InProgress',
  });
  (visitApiService.getTodayGroupingSuggestion as jest.Mock).mockResolvedValue({
    clusters: [],
    suggestedVisitOrder: [],
    legs: [],
    excluded: [],
    eligibleCount: 0,
    excludedCount: 0,
    message: 'At least two visits with verified GPS are required for route grouping',
  });
});

test('today strip renders visit rows from visit api', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('CR-99');
  expect(combined).toContain('ST-99');
  expect(visitApiService.listToday).toHaveBeenCalled();
});

test('cold-open cache shows cached items before fetch resolves', async () => {
  (commandStripCache.readCache as jest.Mock).mockResolvedValue({
    items: [
      {
        ...sampleVisit,
        case: {...sampleVisit.case, crimeNumber: 'CR-CACHED'},
      },
    ],
    fetchedAtUtc: new Date().toISOString(),
  });

  let resolveFetch!: (value: unknown) => void;
  const pendingFetch = new Promise(resolve => {
    resolveFetch = resolve;
  });
  (visitApiService.listToday as jest.Mock).mockReturnValue(pendingFetch);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
  });

  const midLoad = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(midLoad).toContain('CR-CACHED');

  await ReactTestRenderer.act(async () => {
    resolveFetch({items: [sampleVisit], page: 1, pageSize: 1});
    await pendingFetch;
    await Promise.resolve();
  });
});

test('pull to refresh triggers second api call', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const initialCalls = (visitApiService.listToday as jest.Mock).mock.calls.length;

  const refreshControl = tree.root.findByType(RefreshControl);
  await ReactTestRenderer.act(async () => {
    refreshControl.props.onRefresh();
    await Promise.resolve();
    await Promise.resolve();
  });

  expect((visitApiService.listToday as jest.Mock).mock.calls.length).toBeGreaterThan(
    initialCalls,
  );
});

test('empty visit list shows empty state message', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [],
    page: 1,
    pageSize: 0,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('No visits scheduled for today');
  expect(combined).toContain('Pull to refresh when your coordinator schedules visits');
});

test('fetch error keeps cached rows visible with retry', async () => {
  (commandStripCache.readCache as jest.Mock).mockResolvedValue({
    items: [
      {
        ...sampleVisit,
        case: {...sampleVisit.case, crimeNumber: 'CR-CACHED'},
      },
    ],
    fetchedAtUtc: new Date().toISOString(),
  });
  (visitApiService.listToday as jest.Mock).mockRejectedValue(new Error('network'));

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('CR-CACHED');
  expect(combined).toContain('error');
  expect(combined).toContain('Retry');
});

test('tapping case headline navigates to case detail', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const openCasePressable = tree.root
    .findAllByType(Pressable)
    .find(node => {
      try {
        const texts = node
          .findAllByType(Text)
          .map(textNode => String(textNode.props.children));
        return texts.some(text => text.includes('CR-99'));
      } catch {
        return false;
      }
    });

  await ReactTestRenderer.act(() => {
    openCasePressable?.props.onPress();
  });

  expect(mockParentNavigate).toHaveBeenCalledWith('Cases', {
    screen: 'CaseDetailPlaceholder',
    params: {caseId: sampleVisit.case.id},
  });
});

test('start visit calls api and navigates to active visit', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const startButton = tree.root
    .findAllByProps({accessibilityLabel: 'Start visit'})
    .find(node => node.props.onPress);

  await ReactTestRenderer.act(async () => {
    startButton?.props.onPress();
    await Promise.resolve();
    await Promise.resolve();
  });

  expect(visitApiService.startVisit).toHaveBeenCalledWith(sampleVisit);
  expect(mockNavigate).toHaveBeenCalledWith('ActiveVisit', {
    visit: expect.objectContaining({status: 'InProgress'}),
  });
});

test('in progress visit navigates to active visit without start call', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [{...sampleVisit, status: 'InProgress'}],
    page: 1,
    pageSize: 1,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const continueButton = tree.root
    .findAllByProps({accessibilityLabel: 'Continue visit'})
    .find(node => node.props.onPress);

  await ReactTestRenderer.act(() => {
    continueButton?.props.onPress();
  });

  expect(visitApiService.startVisit).not.toHaveBeenCalled();
  expect(mockNavigate).toHaveBeenCalledWith('ActiveVisit', {
    visit: expect.objectContaining({status: 'InProgress'}),
  });
});

test('unverified navigate opens capture landmark modal', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const navigateButton = tree.root
    .findAllByProps({accessibilityLabel: 'Navigate to visit'})
    .find(node => node.props.onPress);

  await ReactTestRenderer.act(async () => {
    navigateButton?.props.onPress();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Capture landmark before navigate');
  expect(openGoogleMaps).not.toHaveBeenCalled();
});

test('verified navigate opens google maps directly', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [
      {
        ...sampleVisit,
        case: {
          ...sampleVisit.case,
          gpsVerified: true,
          latitude: 12.9716,
          longitude: 77.5946,
        },
      },
    ],
    page: 1,
    pageSize: 1,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const navigateButton = tree.root
    .findAllByProps({accessibilityLabel: 'Navigate to visit'})
    .find(node => node.props.onPress);

  await ReactTestRenderer.act(async () => {
    navigateButton?.props.onPress();
    await Promise.resolve();
  });

  expect(openGoogleMaps).toHaveBeenCalledWith(12.9716, 77.5946);
});

const secondVisit = {
  id: '33333333-3333-4333-8333-333333333333',
  scheduledAtUtc: '2026-06-16T11:00:00Z',
  status: 'Scheduled',
  isOverdue: false,
  case: {
    id: '44444444-4444-4444-8444-444444444444',
    crimeNumber: 'CR-100',
    stNumber: 'ST-100',
    domicile: 'Urban',
    gpsVerified: true,
    latitude: 12.98,
    longitude: 77.5946,
  },
};

test('group nearby visits hidden when fewer than two visits', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  expect(
    tree.root
      .findAllByProps({accessibilityLabel: 'Group nearby visits'})
      .length,
  ).toBe(0);
});

test('shows unverified gps pre-grouping banner when needed', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [sampleVisit, secondVisit],
    page: 1,
    pageSize: 2,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain(
    'Some visits need landmark capture before they can be grouped',
  );
});

test('group nearby visits opens suggested route sheet', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [secondVisit, {...secondVisit, id: '55555555-5555-4555-8555-555555555555'}],
    page: 1,
    pageSize: 2,
  });
  (visitApiService.getTodayGroupingSuggestion as jest.Mock).mockResolvedValue({
    clusters: [{clusterIndex: 0, visitIds: [secondVisit.id, '55555555-5555-4555-8555-555555555555']}],
    suggestedVisitOrder: [secondVisit.id, '55555555-5555-4555-8555-555555555555'],
    legs: [],
    excluded: [],
    eligibleCount: 2,
    excludedCount: 0,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const groupButton = tree.root.findByProps({accessibilityLabel: 'Group nearby visits'});
  await ReactTestRenderer.act(async () => {
    groupButton.props.onPress();
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(visitApiService.getTodayGroupingSuggestion).toHaveBeenCalled();
  expect(combined).toContain('Suggested route');
});

test('cold open restores custom visit order from cache', async () => {
  (visitApiService.listToday as jest.Mock).mockResolvedValue({
    items: [secondVisit, sampleVisit],
    page: 1,
    pageSize: 2,
  });
  (commandStripCache.readCache as jest.Mock).mockResolvedValue({
    items: [secondVisit, sampleVisit],
    fetchedAtUtc: new Date().toISOString(),
    customVisitOrder: [sampleVisit.id, secondVisit.id],
    routeGroupingActive: true,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const crimeTexts = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children));
  const firstCardIndex = crimeTexts.findIndex(text => text.includes('CR-99'));
  const secondCardIndex = crimeTexts.findIndex(text => text.includes('CR-100'));
  expect(firstCardIndex).toBeGreaterThan(-1);
  expect(secondCardIndex).toBeGreaterThan(-1);
  expect(firstCardIndex).toBeLessThan(secondCardIndex);
});

test('today strip shows sync chip from offline queue', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: sampleVisit.id,
      syncStatus: 'local',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Saved on this device');
});

test('error sync chip navigates to more sync queue', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: sampleVisit.id,
      syncStatus: 'error',
      lastError: 'Server rejected',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<TodayScreen />);
    await Promise.resolve();
    await Promise.resolve();
  });

  const errorChip = tree.root.findByProps({accessibilityLabel: 'Sync failed'});
  await ReactTestRenderer.act(() => {
    errorChip.props.onPress();
  });

  expect(mockParentNavigate).toHaveBeenCalledWith('More', {screen: 'SyncQueue'});
});
