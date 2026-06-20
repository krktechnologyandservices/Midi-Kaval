import React, {useEffect} from 'react';
import {NavigationContainer} from '@react-navigation/native';
import {SafeAreaProvider} from 'react-native-safe-area-context';
import {AuthProvider, useAuth} from './context/AuthContext';
import {navigationRef} from './navigation/navigationRef';
import {RootNavigator} from './navigation/RootNavigator';
import {registerPushNotificationHandlers} from './services/devices/pushNotificationHandlers';

function PushNotificationBootstrap(): null {
  const auth = useAuth();

  useEffect(() => {
    if (!auth.isAuthenticated || auth.destination !== 'tabs') {
      return undefined;
    }

    return registerPushNotificationHandlers(navigationRef, {
      isAuthenticated: () => auth.isAuthenticated,
    });
  }, [auth.destination, auth.isAuthenticated]);

  return null;
}

export default function App(): React.JSX.Element {
  return (
    <SafeAreaProvider>
      <AuthProvider>
        <NavigationContainer ref={navigationRef}>
          <PushNotificationBootstrap />
          <RootNavigator />
        </NavigationContainer>
      </AuthProvider>
    </SafeAreaProvider>
  );
}
