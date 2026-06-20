import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Text} from 'react-native';
import {
  handleNotificationPress,
  NotificationsListScreen,
} from '../src/screens/notifications/NotificationsListScreen';
import {notificationApiService} from '../src/services/notifications/NotificationApiService';
import {useAuth} from '../src/context/AuthContext';

const mockNavigate = jest.fn();

jest.mock('@react-navigation/native', () => {
  const React = require('react');
  return {
    useNavigation: () => ({
      navigate: mockNavigate,
    }),
    useFocusEffect: (callback: () => void | (() => void)) => {
      React.useEffect(() => {
        return callback();
      }, [callback]);
    },
  };
});

jest.mock('../src/context/AuthContext', () => ({
  useAuth: jest.fn(() => ({
    isFieldRole: true,
    user: {email: 'worker@test', role: 'SocialWorker'},
  })),
}));

jest.mock('../src/services/notifications/NotificationApiService', () => ({
  notificationApiService: {
    list: jest.fn(),
    markRead: jest.fn(),
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

test('shows empty state copy', async () => {
  (notificationApiService.list as jest.Mock).mockResolvedValue([]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<NotificationsListScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain("You're up to date.");
});

test('shows field worker guard for non-field roles', async () => {
  (useAuth as jest.Mock).mockReturnValue({
    isFieldRole: false,
    user: {email: 'director@test', role: 'Director'},
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<NotificationsListScreen />);
  });

  expect(screenText(tree)).toContain('Notifications are for field workers only.');
});

test('travel claim notification tap marks read and navigates to claim view', async () => {
  const markRead = jest.fn(async () => undefined);
  const navigateToClaim = jest.fn();

  await handleNotificationPress(
    {
      id: '99999999-9999-4999-8999-999999999999',
      eventType: 'travel.claim.approved',
      resourceId: '11111111-1111-4111-8111-111111111111',
      isRead: false,
    },
    markRead,
    navigateToClaim,
  );

  expect(markRead).toHaveBeenCalledWith('99999999-9999-4999-8999-999999999999');
  expect(navigateToClaim).toHaveBeenCalledWith('11111111-1111-4111-8111-111111111111');
});

test('non-travel notification tap marks read only', async () => {
  const markRead = jest.fn(async () => undefined);
  const navigateToClaim = jest.fn();

  await handleNotificationPress(
    {
      id: '88888888-8888-4888-8888-888888888888',
      eventType: 'intervention.overdue',
      resourceId: '22222222-2222-4222-8222-222222222222',
      isRead: false,
    },
    markRead,
    navigateToClaim,
  );

  expect(markRead).toHaveBeenCalledWith('88888888-8888-4888-8888-888888888888');
  expect(navigateToClaim).not.toHaveBeenCalled();
});

test('renders notification rows', async () => {
  (notificationApiService.list as jest.Mock).mockResolvedValue([
    {
      id: '77777777-7777-4777-8777-777777777777',
      title: 'Travel claim approved',
      body: 'Your claim for District Court (₹45.5) was approved.',
      createdAtUtc: new Date().toISOString(),
      isRead: false,
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<NotificationsListScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(screenText(tree)).toContain('Travel claim approved');
  expect(screenText(tree)).toContain('District Court');
});
