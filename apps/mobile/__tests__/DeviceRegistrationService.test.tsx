import {Platform} from 'react-native';
import {AuthSessionService} from '../src/services/auth/AuthSessionService';
import {DeviceRegistrationService} from '../src/services/devices/DeviceRegistrationService';

jest.mock('../src/services/devices/pushMessaging', () => ({
  resolvePushToken: jest.fn(),
}));

jest.mock('../src/services/auth/secureStorage', () => ({
  getOrCreateDeviceInstallId: jest.fn(),
}));

import {resolvePushToken} from '../src/services/devices/pushMessaging';
import {getOrCreateDeviceInstallId} from '../src/services/auth/secureStorage';

describe('DeviceRegistrationService', () => {
  const putApi = jest.fn();
  const isAuthenticated = jest.fn();
  const auth = {
    isAuthenticated,
    putApi,
  } as unknown as AuthSessionService;

  beforeEach(() => {
    jest.clearAllMocks();
    Platform.OS = 'android';
  });

  test('registers device when authenticated', async () => {
    isAuthenticated.mockResolvedValue(true);
    (resolvePushToken as jest.Mock).mockResolvedValue('push-token-1');
    (getOrCreateDeviceInstallId as jest.Mock).mockResolvedValue('install-1');
    putApi.mockResolvedValue({data: {}, meta: {requestId: 'req'}});

    const service = new DeviceRegistrationService(auth);
    await service.registerIfAuthenticated();

    expect(putApi).toHaveBeenCalledWith('/api/v1/devices/me', {
      deviceInstallId: 'install-1',
      platform: 'android',
      pushToken: 'push-token-1',
    });
  });

  test('skips registration when unauthenticated', async () => {
    isAuthenticated.mockResolvedValue(false);

    const service = new DeviceRegistrationService(auth);
    await service.registerIfAuthenticated();

    expect(putApi).not.toHaveBeenCalled();
  });

  test('swallows API errors during bootstrap registration', async () => {
    const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
    isAuthenticated.mockResolvedValue(true);
    (resolvePushToken as jest.Mock).mockResolvedValue('push-token-1');
    (getOrCreateDeviceInstallId as jest.Mock).mockResolvedValue('install-1');
    const error = new Error('network');
    putApi.mockRejectedValue(error);

    const service = new DeviceRegistrationService(auth);
    await expect(service.registerIfAuthenticated()).resolves.toBeUndefined();
    expect(warnSpy).toHaveBeenCalledWith(
      '[DeviceRegistration] Push token registration failed.',
      error,
    );
    warnSpy.mockRestore();
  });
});
