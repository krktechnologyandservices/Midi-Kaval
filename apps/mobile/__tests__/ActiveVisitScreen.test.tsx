import React from 'react';
import {Linking, Pressable, Text, TextInput} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {ActiveVisitScreen} from '../src/screens/today/ActiveVisitScreen';
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

const defaultVisit = {
  id: '11111111-1111-4111-8111-111111111111',
  status: 'InProgress',
  case: {
    id: '22222222-2222-4222-8222-222222222222',
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    domicile: 'Urban',
    gpsVerified: false,
  },
};

const mockUseRoute = jest.fn(() => ({params: {visit: defaultVisit}}));

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({
    navigate: mockNavigate,
    getParent: () => ({
      navigate: mockParentNavigate,
    }),
  }),
  useRoute: () => mockUseRoute(),
  useFocusEffect: (callback: () => void) => {
    callback();
  },
}));

jest.mock('../src/services/sync/useSyncOnForeground', () => ({
  useSyncOnForeground: jest.fn(),
}));

jest.mock('../src/services/visits/VisitApiService', () => ({
  visitApiService: {
    completeVisit: jest.fn(),
    rescheduleVisit: jest.fn(),
    extractErrorMessage: jest.fn(() => 'complete failed'),
  },
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn().mockResolvedValue([]),
}));

jest.mock('../src/services/visits/commandStripCache', () => ({
  readCache: jest.fn().mockResolvedValue(null),
  writeCache: jest.fn().mockResolvedValue(undefined),
}));

jest.mock('../src/services/auth/AuthSessionService', () => ({
  authSessionService: {
    getLastLoginOtpVerifiedAtUtc: jest.fn(() => new Date().toISOString()),
    stepUp: jest.fn().mockResolvedValue({challengeId: 'challenge-1'}),
    verifyStepUp: jest.fn().mockResolvedValue(undefined),
    extractErrorMessage: jest.fn(() => 'step-up failed'),
  },
}));

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    revealCasePii: jest.fn().mockResolvedValue({
      beneficiaryName: 'Ravi Kumar',
      beneficiaryAge: 14,
    }),
  },
}));

import * as offlineQueue from '../src/services/sync/offlineQueue';
import {authSessionService} from '../src/services/auth/AuthSessionService';
import {caseApiService} from '../src/services/cases/CaseApiService';

beforeEach(() => {
  jest.clearAllMocks();
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([]);
  mockUseRoute.mockReturnValue({params: {visit: defaultVisit}});
  (authSessionService.getLastLoginOtpVerifiedAtUtc as jest.Mock).mockReturnValue(
    new Date().toISOString(),
  );
});

test('complete visit disabled until note entered', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const completeButton = tree.root.findByProps({accessibilityLabel: 'Complete visit'});
  expect(completeButton.props.disabled).toBe(true);
});

test('complete visit enabled with note and navigates back to today', async () => {
  (visitApiService.completeVisit as jest.Mock).mockResolvedValue({
    id: '11111111-1111-4111-8111-111111111111',
    status: 'Completed',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const noteInput = tree.root.findByProps({accessibilityLabel: 'Visit note'});
  ReactTestRenderer.act(() => {
    noteInput.props.onChangeText('Family was home.');
  });

  const completeButton = tree.root.findByProps({accessibilityLabel: 'Complete visit'});
  await ReactTestRenderer.act(async () => {
    completeButton.props.onPress();
    await Promise.resolve();
  });

  expect(visitApiService.completeVisit).toHaveBeenCalledWith(
    expect.objectContaining({id: '11111111-1111-4111-8111-111111111111'}),
    'Family was home.',
  );
  expect(mockNavigate).toHaveBeenCalledWith(
    'TodayHome',
    expect.objectContaining({refreshToken: expect.any(Number)}),
  );
});

test('reschedule requires reason before submit', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const rescheduleButton = tree.root
    .findAllByType(Pressable)
    .find(node => {
      const text = node.findAllByType(Text).map(t => String(t.props.children)).join(' ');
      return text.includes('Reschedule');
    });

  ReactTestRenderer.act(() => {
    rescheduleButton?.props.onPress();
  });

  const modalSubmit = tree.root
    .findAllByType(Pressable)
    .find(node => {
      const text = node.findAllByType(Text).map(t => String(t.props.children)).join(' ');
      return text.includes('Reschedule') && node.props.disabled === true;
    });

  expect(modalSubmit).toBeTruthy();
});

test('complete failure shows inline error and keeps note draft', async () => {
  (visitApiService.completeVisit as jest.Mock).mockRejectedValue(new Error('network'));

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const noteInput = tree.root.findByProps({accessibilityLabel: 'Visit note'});
  ReactTestRenderer.act(() => {
    noteInput.props.onChangeText('Family was home.');
  });

  const completeButton = tree.root.findByProps({accessibilityLabel: 'Complete visit'});
  await ReactTestRenderer.act(async () => {
    completeButton.props.onPress();
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('complete failed');
  expect(combined).toContain('Retry');
  expect(mockNavigate).not.toHaveBeenCalled();
  expect(noteInput.props.value).toBe('Family was home.');
});

test('navigate opens capture modal when gps unverified', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const navigateButton = tree.root
    .findAllByType(Pressable)
    .find(node => {
      const text = node.findAllByType(Text).map(t => String(t.props.children)).join(' ');
      return text === 'Navigate';
    });

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

test('successful reschedule navigates back to today', async () => {
  (visitApiService.rescheduleVisit as jest.Mock).mockResolvedValue({
    id: '11111111-1111-4111-8111-111111111111',
    status: 'Scheduled',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
  });

  const rescheduleButton = tree.root
    .findAllByType(Pressable)
    .find(node => {
      const text = node.findAllByType(Text).map(t => String(t.props.children)).join(' ');
      return text === 'Reschedule';
    });

  ReactTestRenderer.act(() => {
    rescheduleButton?.props.onPress();
  });

  const reasonInput = tree.root.findByProps({accessibilityLabel: 'Reschedule reason'});
  ReactTestRenderer.act(() => {
    reasonInput.props.onChangeText('Beneficiary unavailable');
  });

  const modalSubmit = tree.root.findByProps({accessibilityLabel: 'Confirm reschedule'});

  await ReactTestRenderer.act(async () => {
    modalSubmit.props.onPress();
    await Promise.resolve();
  });

  expect(visitApiService.rescheduleVisit).toHaveBeenCalled();
  expect(mockNavigate).toHaveBeenCalledWith(
    'TodayHome',
    expect.objectContaining({refreshToken: expect.any(Number)}),
  );
});

test('active visit shows sync chip for queued visit', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.complete',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: '11111111-1111-4111-8111-111111111111',
      syncStatus: 'pending',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
    await Promise.resolve();
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Uploading');
});

test('active visit refreshes queue on focus', async () => {
  await ReactTestRenderer.act(async () => {
    ReactTestRenderer.create(<ActiveVisitScreen />);
    await Promise.resolve();
  });

  expect(offlineQueue.readOfflineQueue).toHaveBeenCalled();
});

test('error sync chip navigates to more sync queue', async () => {
  (offlineQueue.readOfflineQueue as jest.Mock).mockResolvedValue([
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: '11111111-1111-4111-8111-111111111111',
      syncStatus: 'error',
      lastError: 'failed',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
    await Promise.resolve();
  });

  const errorChip = tree.root.findByProps({accessibilityLabel: 'Sync failed'});
  await ReactTestRenderer.act(() => {
    errorChip.props.onPress();
  });

  expect(mockParentNavigate).toHaveBeenCalledWith('More', {screen: 'SyncQueue'});
});

test('POCSO active visit expands within login OTP window', async () => {
  mockUseRoute.mockReturnValue({
    params: {
      visit: {
        ...defaultVisit,
        case: {
          ...defaultVisit.case,
          beneficiaryName: 'R. K.',
          sensitivityLevel: 'POCSO',
        },
      },
    },
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
    await Promise.resolve();
  });

  const expandButton = tree.root.findByProps({accessibilityLabel: 'Show full detail'});
  await ReactTestRenderer.act(async () => {
    expandButton.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.revealCasePii).toHaveBeenCalledWith(defaultVisit.case.id);
});

test('POCSO active visit opens step-up modal after OTP window', async () => {
  (authSessionService.getLastLoginOtpVerifiedAtUtc as jest.Mock).mockReturnValue(
    '2020-01-01T00:00:00.000Z',
  );
  (caseApiService.revealCasePii as jest.Mock).mockRejectedValue(
    new Error('Recent OTP verification is required.'),
  );
  mockUseRoute.mockReturnValue({
    params: {
      visit: {
        ...defaultVisit,
        case: {
          ...defaultVisit.case,
          beneficiaryName: 'R. K.',
          sensitivityLevel: 'POCSO',
        },
      },
    },
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(<ActiveVisitScreen />);
    await Promise.resolve();
  });

  const expandButton = tree.root.findByProps({accessibilityLabel: 'Show full detail'});
  await ReactTestRenderer.act(async () => {
    expandButton.props.onPress();
    await Promise.resolve();
  });

  expect(authSessionService.stepUp).toHaveBeenCalled();
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('Verify to show full detail');
});
