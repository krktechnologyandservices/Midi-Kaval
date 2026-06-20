import {useEffect, useState} from 'react';
import NetInfo from '@react-native-community/netinfo';

export function useNetworkStatus(): boolean {
  const [isOffline, setIsOffline] = useState(false);

  useEffect(() => {
    const update = (connected: boolean | null, reachable: boolean | null | undefined) => {
      setIsOffline(!connected || reachable === false);
    };

    void NetInfo.fetch().then(state => {
      update(state.isConnected, state.isInternetReachable);
    });

    const unsubscribe = NetInfo.addEventListener(state => {
      update(state.isConnected, state.isInternetReachable);
    });

    return unsubscribe;
  }, []);

  return isOffline;
}
