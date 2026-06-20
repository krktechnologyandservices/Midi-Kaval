import {useEffect, useRef} from 'react';
import {AppState, AppStateStatus} from 'react-native';
import {deviceRegistrationService} from './DeviceRegistrationService';

type Options = {
  enabled: boolean;
};

export function useDeviceRegistrationOnForeground({enabled}: Options): void {
  const appState = useRef<AppStateStatus>(AppState.currentState);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    const register = (): void => {
      void deviceRegistrationService.registerIfAuthenticated();
    };

    register();

    const subscription = AppState.addEventListener('change', nextState => {
      if (
        appState.current.match(/inactive|background/) &&
        nextState === 'active'
      ) {
        register();
      }

      appState.current = nextState;
    });

    return () => {
      subscription.remove();
    };
  }, [enabled]);
}
