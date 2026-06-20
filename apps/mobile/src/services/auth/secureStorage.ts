import * as Keychain from 'react-native-keychain';

const ACCESS_SERVICE = 'midi_kaval_access_token';
const REFRESH_SERVICE = 'midi_kaval_refresh_token';
const DEVICE_INSTALL_SERVICE = 'midi_kaval_device_install_id';

function createDeviceInstallId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  return `device-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

export async function saveAccessToken(token: string): Promise<void> {
  await Keychain.setGenericPassword('access', token, {service: ACCESS_SERVICE});
}

export async function saveRefreshToken(token: string): Promise<void> {
  await Keychain.setGenericPassword('refresh', token, {service: REFRESH_SERVICE});
}

export async function getAccessToken(): Promise<string | null> {
  const credentials = await Keychain.getGenericPassword({service: ACCESS_SERVICE});
  return credentials ? credentials.password : null;
}

export async function getRefreshToken(): Promise<string | null> {
  const credentials = await Keychain.getGenericPassword({service: REFRESH_SERVICE});
  return credentials ? credentials.password : null;
}

export async function getDeviceInstallId(): Promise<string | null> {
  const credentials = await Keychain.getGenericPassword({service: DEVICE_INSTALL_SERVICE});
  return credentials ? credentials.password : null;
}

export async function getOrCreateDeviceInstallId(): Promise<string> {
  const existing = await getDeviceInstallId();
  if (existing) {
    return existing;
  }

  const id = createDeviceInstallId();
  await Keychain.setGenericPassword('device', id, {service: DEVICE_INSTALL_SERVICE});
  return id;
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    Keychain.resetGenericPassword({service: ACCESS_SERVICE}),
    Keychain.resetGenericPassword({service: REFRESH_SERVICE}),
  ]);
}
