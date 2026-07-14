import * as Keychain from 'react-native-keychain';
import AsyncStorage from '@react-native-async-storage/async-storage';
import {AppRole} from '@midi-kaval/shared-types';
import {AuthSessionService} from '../src/services/auth/AuthSessionService';
import * as secureStorage from '../src/services/auth/secureStorage';

describe('AuthSessionService', () => {
  let service: AuthSessionService;

  beforeEach(() => {
    jest.resetAllMocks();
    service = new AuthSessionService();
    global.fetch = jest.fn();
  });

  it('login stores OTP challenge', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({
        data: {
          challengeId: '11111111-1111-4111-8111-111111111111',
          expiresInSeconds: 300,
        },
        meta: {requestId: 'r1'},
      }),
    });

    const result = await service.login({
      email: 'worker@pilot.example',
      password: 'password1',
    });

    expect(result.challengeId).toBe('11111111-1111-4111-8111-111111111111');
    expect(service.getOtpChallenge()?.challengeId).toBe(
      '11111111-1111-4111-8111-111111111111',
    );
    expect(AsyncStorage.setItem).toHaveBeenCalled();
  });

  it('verifyOtp stores tokens in secure storage', async () => {
    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          data: {
            challengeId: '22222222-2222-4222-8222-222222222222',
            expiresInSeconds: 300,
          },
          meta: {requestId: 'r1'},
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          data: {
            accessToken: 'access-token',
            refreshToken: 'refresh-token',
            user: {
              id: '1',
              email: 'worker@pilot.example',
              role: AppRole.SocialWorker,
            },
          },
          meta: {requestId: 'r2'},
        }),
      });

    await service.login({email: 'worker@pilot.example', password: 'password1'});
    await service.verifyOtp('123456');

    expect(Keychain.setGenericPassword).toHaveBeenCalled();
    expect(secureStorage.getAccessToken).toBeDefined();
    expect(service.getUser()?.role).toBe(AppRole.SocialWorker);
  });

  it('refresh sends refreshToken in JSON body', async () => {
    (Keychain.getGenericPassword as jest.Mock).mockImplementation(
      ({service: svc}: {service: string}) => {
        if (svc.includes('refresh')) {
          return Promise.resolve({username: 'refresh', password: 'refresh-token'});
        }
        return Promise.resolve({username: 'access', password: 'access-token'});
      },
    );

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({
        data: {
          accessToken: 'new-access',
          refreshToken: 'new-refresh',
        },
        meta: {requestId: 'r3'},
      }),
    });

    const refreshed = await service.refreshSession();
    expect(refreshed).toBe(true);

    const [, options] = (global.fetch as jest.Mock).mock.calls[0];
    expect(JSON.parse(options.body)).toEqual({refreshToken: 'refresh-token'});
    expect(options.headers.Authorization).toBeUndefined();
  });

  it('retried 401 invokes session expired callback', async () => {
    const onSessionExpired = jest.fn();
    service.onSessionExpired = onSessionExpired;

    (Keychain.getGenericPassword as jest.Mock).mockImplementation(
      ({service: svc}: {service: string}) => {
        if (svc.includes('refresh')) {
          return Promise.resolve({username: 'refresh', password: 'refresh-token'});
        }
        return Promise.resolve({username: 'access', password: 'access-token'});
      },
    );

    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({detail: 'Unauthorized'}),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          data: {
            accessToken: 'new-access',
            refreshToken: 'new-refresh',
          },
          meta: {requestId: 'r4'},
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          data: {id: '1', email: 'worker@pilot.example', role: 'SocialWorker'},
          meta: {requestId: 'r5'},
        }),
      })
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({detail: 'Unauthorized'}),
      });

    await expect(service.loadCurrentUser()).resolves.toBeNull();
    expect(onSessionExpired).toHaveBeenCalled();
  });

  it('deactivated 403 invokes deactivated callback', async () => {
    const onDeactivated = jest.fn();
    service.onDeactivated = onDeactivated;

    (Keychain.getGenericPassword as jest.Mock).mockResolvedValue({
      username: 'access',
      password: 'access-token',
    });

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: false,
      status: 403,
      json: async () => ({detail: 'Contact your coordinator'}),
    });

    await expect(service.loadCurrentUser()).resolves.toBeNull();
    expect(onDeactivated).toHaveBeenCalled();
  });

  it('bootstrap restores a still-valid OTP challenge from storage', async () => {
    (AsyncStorage.getItem as jest.Mock).mockResolvedValueOnce(
      JSON.stringify({
        challengeId: '33333333-3333-4333-8333-333333333333',
        expiresInSeconds: 120,
        expiresAtUtc: Date.now() + 120_000,
      }),
    );
    (Keychain.getGenericPassword as jest.Mock).mockResolvedValue(false);

    const user = await service.bootstrapSession();
    expect(user).toBeNull();
    expect(service.getOtpChallenge()?.challengeId).toBe(
      '33333333-3333-4333-8333-333333333333',
    );
  });

  it('bootstrap discards an expired OTP challenge instead of restoring it', async () => {
    // Regression test: a challenge left over from a login the user never finished
    // (missed OTP, app killed) must not force the OTP screen forever on relaunch.
    (AsyncStorage.getItem as jest.Mock).mockResolvedValueOnce(
      JSON.stringify({
        challengeId: '44444444-4444-4444-8444-444444444444',
        expiresInSeconds: 120,
        expiresAtUtc: Date.now() - 1_000,
      }),
    );
    (Keychain.getGenericPassword as jest.Mock).mockResolvedValue(false);

    const user = await service.bootstrapSession();
    expect(user).toBeNull();
    expect(service.getOtpChallenge()).toBeNull();
    expect(AsyncStorage.removeItem).toHaveBeenCalledWith('midi_kaval_otp_challenge');
  });

  it('logout sends refreshToken and deviceInstallId in JSON body', async () => {
    (Keychain.getGenericPassword as jest.Mock).mockImplementation(
      ({service}: {service: string}) => {
        if (service.includes('refresh')) {
          return Promise.resolve({username: 'refresh', password: 'refresh-token'});
        }
        if (service.includes('device_install')) {
          return Promise.resolve({username: 'device', password: 'install-id-123'});
        }
        return Promise.resolve(false);
      },
    );

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      status: 204,
      json: async () => ({}),
    });

    await service.logout();

    const [, options] = (global.fetch as jest.Mock).mock.calls[0];
    expect(JSON.parse(options.body)).toEqual({
      refreshToken: 'refresh-token',
      deviceInstallId: 'install-id-123',
    });
    expect(Keychain.resetGenericPassword).toHaveBeenCalled();
  });

  it('logout clears secure storage', async () => {
    (Keychain.getGenericPassword as jest.Mock).mockResolvedValue({
      username: 'refresh',
      password: 'refresh-token',
    });

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      status: 204,
      json: async () => ({}),
    });

    await service.logout();
    expect(Keychain.resetGenericPassword).toHaveBeenCalled();
  });
});
