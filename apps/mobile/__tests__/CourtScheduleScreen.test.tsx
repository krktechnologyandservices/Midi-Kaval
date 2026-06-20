import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Pressable, Text} from 'react-native';
import {CourtScheduleScreen} from '../src/screens/court/CourtScheduleScreen';
import {courtApiService} from '../src/services/court/CourtApiService';
import {useAuth} from '../src/context/AuthContext';

const mockParentNavigate = jest.fn();

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({
    getParent: () => ({
      navigate: mockParentNavigate,
    }),
  }),
}));

jest.mock('../src/context/AuthContext', () => ({
  useAuth: jest.fn(() => ({
    isFieldRole: true,
    user: {email: 'worker@test', role: 'SocialWorker'},
  })),
}));

jest.mock('../src/services/court/CourtApiService', () => ({
  courtApiService: {
    listUpcomingCourtSittings: jest.fn(),
    extractErrorMessage: jest.fn(() => 'load failed'),
  },
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
});

test('shows empty state copy for current week', async () => {
  (courtApiService.listUpcomingCourtSittings as jest.Mock).mockResolvedValue([]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<CourtScheduleScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('No sittings this week.');
});

test('renders past-due row styling and navigates to case detail', async () => {
  const now = new Date();
  const day = now.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const weekStart = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + mondayOffset + 1, 10),
  );

  (courtApiService.listUpcomingCourtSittings as jest.Mock).mockResolvedValue([
    {
      id: 'sitting-1',
      caseId: '11111111-1111-4111-8111-111111111111',
      courtName: 'District Court',
      purpose: 'Hearing',
      status: 'Upcoming',
      scheduledAtUtc: weekStart.toISOString(),
      isPastDue: true,
      case: {crimeNumber: 'CR-1', beneficiaryName: 'R. K.'},
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<CourtScheduleScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('District Court');
  expect(text).toContain('Overdue');
  expect(text).toContain('R. K.');
  expect(text).toContain('CR-1');

  const courtNameNode = tree.root
    .findAllByType(Text)
    .find(node => node.props.children === 'District Court');
  expect(courtNameNode).toBeTruthy();

  let pressable = courtNameNode?.parent;
  while (pressable && pressable.type !== Pressable) {
    pressable = pressable.parent;
  }

  expect(pressable).toBeTruthy();
  await ReactTestRenderer.act(() => {
    pressable!.props.onPress();
  });

  expect(mockParentNavigate).toHaveBeenCalledWith('Cases', {
    screen: 'CaseDetailPlaceholder',
    params: {caseId: '11111111-1111-4111-8111-111111111111'},
  });
});

test('shows retry on load error', async () => {
  (courtApiService.listUpcomingCourtSittings as jest.Mock).mockRejectedValue(new Error('fail'));

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<CourtScheduleScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('load failed');
  expect(screenText(tree)).toContain('Retry');
});

test('shows field-worker-only message for non-field roles', async () => {
  (useAuth as jest.Mock).mockReturnValue({
    isFieldRole: false,
    user: {email: 'coord@test', role: 'Coordinator'},
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<CourtScheduleScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('Court schedule is for field workers only.');
  expect(courtApiService.listUpcomingCourtSittings).not.toHaveBeenCalled();
});
