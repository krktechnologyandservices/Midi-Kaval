import NetInfo from '@react-native-community/netinfo';

let forceOfflineForTests = false;

export function setForceOfflineForTests(value: boolean): void {
  forceOfflineForTests = value;
}

export async function isDeviceOffline(): Promise<boolean> {
  if (forceOfflineForTests) {
    return true;
  }

  const state = await NetInfo.fetch();
  if (!state.isConnected) {
    return true;
  }

  return state.isInternetReachable === false;
}

export function subscribeToNetworkChanges(
  listener: (isOffline: boolean) => void,
): () => void {
  return NetInfo.addEventListener(state => {
    const offline =
      !state.isConnected || state.isInternetReachable === false;
    listener(offline);
  });
}
