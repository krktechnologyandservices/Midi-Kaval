import React from 'react';
import ReactTestRenderer from 'react-test-renderer';

jest.mock('../src/context/AuthContext', () => ({
  AuthProvider: ({children}: {children: React.ReactNode}) => <>{children}</>,
  useAuth: () => ({
    isAuthenticated: false,
    destination: 'auth',
  }),
}));

jest.mock('../src/navigation/RootNavigator', () => {
  const React = require('react');
  const {Text} = require('react-native');
  return {
    RootNavigator: () => React.createElement(Text, null, 'Navigation shell'),
  };
});

jest.mock('../src/services/devices/pushNotificationHandlers', () => ({
  registerPushNotificationHandlers: jest.fn(() => jest.fn()),
}));

jest.mock('@react-navigation/native', () => {
  const React = require('react');
  return {
    NavigationContainer: React.forwardRef(
      ({children}: {children: React.ReactNode}, _ref: unknown) => <>{children}</>,
    ),
    createNavigationContainerRef: () => ({
      isReady: () => false,
      navigate: jest.fn(),
    }),
  };
});

import App from '../src/App';

test('renders mobile navigation shell', () => {
  const tree = ReactTestRenderer.create(<App />);
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('Navigation shell');
});
