jest.mock('react-native-keychain', () => ({
  setGenericPassword: jest.fn(() => Promise.resolve(true)),
  getGenericPassword: jest.fn(() => Promise.resolve(false)),
  resetGenericPassword: jest.fn(() => Promise.resolve(true)),
}));

jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: jest.fn(() => Promise.resolve()),
  getItem: jest.fn(() => Promise.resolve(null)),
  removeItem: jest.fn(() => Promise.resolve()),
}));

jest.mock('react-native-safe-area-context', () => {
  const React = require('react');
  return {
    SafeAreaProvider: ({children}: {children: React.ReactNode}) => children,
    useSafeAreaInsets: () => ({top: 0, bottom: 0, left: 0, right: 0}),
  };
});

jest.mock('react-native-gesture-handler', () => ({}));

jest.mock('@react-native-community/datetimepicker', () => {
  const React = require('react');
  const {View} = require('react-native');
  return {
    __esModule: true,
    default: ({accessibilityLabel}: {accessibilityLabel?: string}) =>
      React.createElement(View, {accessibilityLabel}),
  };
});

jest.mock('@react-native-community/geolocation', () => ({
  getCurrentPosition: jest.fn(success =>
    success({
      coords: {latitude: 12.9716, longitude: 77.5946},
    }),
  ),
}));

jest.mock('@react-native-community/netinfo', () => ({
  __esModule: true,
  default: {
    fetch: jest.fn(() =>
      Promise.resolve({
        isConnected: true,
        isInternetReachable: true,
      }),
    ),
    addEventListener: jest.fn(() => jest.fn()),
  },
}));

jest.mock('react-native/Libraries/Linking/Linking', () => ({
  canOpenURL: jest.fn(() => Promise.resolve(true)),
  openURL: jest.fn(() => Promise.resolve()),
}));

jest.mock('react-native/Libraries/Utilities/BackHandler', () => ({
  addEventListener: jest.fn(() => ({remove: jest.fn()})),
  exitApp: jest.fn(),
}));

jest.mock('react-native-screens', () => {
  const React = require('react');
  const {View} = require('react-native');
  return {
    enableScreens: jest.fn(),
    Screen: ({children}: {children: React.ReactNode}) =>
      React.createElement(View, null, children),
    ScreenContainer: ({children}: {children: React.ReactNode}) =>
      React.createElement(View, null, children),
  };
});
