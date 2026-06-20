import {useEffect, useRef} from 'react';
import {AppState, AppStateStatus} from 'react-native';
import {
  flushOfflineQueue,
  FlushOfflineQueueResult,
} from './mobileSyncPushService';
import {isDeviceOffline, subscribeToNetworkChanges} from './networkStatus';
import {readOfflineQueue} from './offlineQueue';

type Options = {
  enabled: boolean;
  onSynced?: (result: FlushOfflineQueueResult) => void | Promise<void>;
};

export function useSyncOnForeground({enabled, onSynced}: Options): void {
  const appState = useRef<AppStateStatus>(AppState.currentState);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    const trySync = async (): Promise<void> => {
      const queue = await readOfflineQueue();
      if (!queue.length || (await isDeviceOffline())) {
        return;
      }

      const result = await flushOfflineQueue();
      await onSynced?.(result);
    };

    void trySync();

    const appStateSubscription = AppState.addEventListener('change', nextState => {
      if (
        appState.current.match(/inactive|background/) &&
        nextState === 'active'
      ) {
        void trySync();
      }

      appState.current = nextState;
    });

    const networkUnsubscribe = subscribeToNetworkChanges(offline => {
      if (!offline) {
        void trySync();
      }
    });

    return () => {
      appStateSubscription.remove();
      networkUnsubscribe();
    };
  }, [enabled, onSynced]);
}
