import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Pressable, Text} from 'react-native';
import {TravelClaimsListScreen} from '../src/screens/travel/TravelClaimsListScreen';
import {travelClaimApiService} from '../src/services/travel/TravelClaimApiService';
import {useAuth} from '../src/context/AuthContext';
import {readOfflineQueue} from '../src/services/sync/offlineQueue';

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({
    navigate: jest.fn(),
  }),
}));

jest.mock('../src/context/AuthContext', () => ({
  useAuth: jest.fn(() => ({
    isFieldRole: true,
    user: {email: 'worker@test', role: 'SocialWorker'},
  })),
}));

jest.mock('../src/services/travel/TravelClaimApiService', () => ({
  travelClaimApiService: {
    listMine: jest.fn(),
    extractErrorMessage: jest.fn(() => 'load failed'),
  },
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn(async () => []),
}));

jest.mock('../src/services/sync/useSyncOnForeground', () => ({
  useSyncOnForeground: jest.fn(),
}));

function screenText(tree: ReactTestRenderer.ReactTestRenderer): string {
  return tree.root
    .findAllByType(Text)
    .map(node => node.props.children)
    .flat()
    .filter((value): value is string => typeof value === 'string')
    .join(' ');
}

beforeEach(() => {
  jest.clearAllMocks();
  (useAuth as jest.Mock).mockReturnValue({
    isFieldRole: true,
    user: {email: 'worker@test', role: 'SocialWorker'},
  });
  (readOfflineQueue as jest.Mock).mockResolvedValue([]);
});

test('shows empty state copy and create CTA', async () => {
  (travelClaimApiService.listMine as jest.Mock).mockResolvedValue([]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimsListScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('No claims yet');
  expect(screenText(tree)).toContain('Create claim');
});

test('shows field worker guard for non-field roles', async () => {
  (useAuth as jest.Mock).mockReturnValue({
    isFieldRole: false,
    user: {email: 'director@test', role: 'Director'},
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimsListScreen />);
  });

  expect(screenText(tree)).toContain('Travel claims are for field workers only.');
});

test('shows error and retry', async () => {
  (travelClaimApiService.listMine as jest.Mock).mockRejectedValue(new Error('fail'));

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimsListScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('load failed');
  expect(screenText(tree)).toContain('Retry');
});

test('renders claim rows', async () => {
  (travelClaimApiService.listMine as jest.Mock).mockResolvedValue([
    {
      id: '11111111-1111-4111-8111-111111111111',
      claimDate: '2026-06-15T00:00:00Z',
      destination: 'District Court',
      transportMode: 'Bus',
      amount: 45.5,
      status: 'Draft',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimsListScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('District Court');
  expect(screenText(tree)).toContain('Draft');
});
