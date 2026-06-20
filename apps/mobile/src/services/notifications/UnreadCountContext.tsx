import React, {createContext, useCallback, useContext, useEffect, useRef, useState} from 'react';
import {AppState, AppStateStatus} from 'react-native';
import {useIsFocused} from '@react-navigation/native';
import {notificationApiService} from './NotificationApiService';

type UnreadCountContextValue = {
  unreadCount: number;
  refresh: () => void;
};

const UnreadCountContext = createContext<UnreadCountContextValue>({
  unreadCount: 0,
  refresh: () => {},
});

export function UnreadCountProvider({children}: {children: React.ReactNode}): React.JSX.Element {
  const [unreadCount, setUnreadCount] = useState(0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const appStateRef = useRef(AppState.currentState);
  const isFocused = useIsFocused();

  const refresh = useCallback(() => {
    notificationApiService.getUnreadCount().then(setUnreadCount).catch(() => {});
  }, []);

  useEffect(() => {
    if (!isFocused) {
      return;
    }
    refresh();
    intervalRef.current = setInterval(refresh, 60_000);

    const subscription = AppState.addEventListener('change', (nextState: AppStateStatus) => {
      if (appStateRef.current.match(/inactive|background/) && nextState === 'active') {
        refresh();
      }
      appStateRef.current = nextState;
    });

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
      subscription.remove();
    };
  }, [refresh, isFocused]);

  return (
    <UnreadCountContext.Provider value={{unreadCount, refresh}}>
      {children}
    </UnreadCountContext.Provider>
  );
}

export function useUnreadCount(): UnreadCountContextValue {
  return useContext(UnreadCountContext);
}
