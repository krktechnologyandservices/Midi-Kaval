import {Platform} from 'react-native';
import {
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {getOrCreateDeviceInstallId} from '../auth/secureStorage';
import {resolvePushToken} from './pushMessaging';

export class DeviceRegistrationService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async registerIfAuthenticated(): Promise<void> {
    try {
      if (!(await this.auth.isAuthenticated())) {
        return;
      }

      const pushToken = await resolvePushToken();
      if (!pushToken) {
        return;
      }

      const deviceInstallId = await getOrCreateDeviceInstallId();
      const platform = Platform.OS === 'ios' ? 'ios' : 'android';

      await this.auth.putApi('/api/v1/devices/me', {
        deviceInstallId,
        platform,
        pushToken,
      });
    } catch (error) {
      console.warn('[DeviceRegistration] Push token registration failed.', error);
    }
  }
}

export const deviceRegistrationService = new DeviceRegistrationService();
