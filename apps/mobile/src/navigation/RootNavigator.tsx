import React from 'react';
import {ActivityIndicator, View, StyleSheet} from 'react-native';
import {useAuth} from '../context/AuthContext';
import {useDeviceRegistrationOnForeground} from '../services/devices/useDeviceRegistrationOnForeground';
import {AuthNavigator} from './AuthNavigator';
import {MainTabNavigator} from './MainTabNavigator';
import {WebOnlyScreen} from '../screens/auth/WebOnlyScreen';

export function RootNavigator(): React.JSX.Element {
  const auth = useAuth();
  const navigationKey = auth.isAuthenticated
    ? `${auth.user?.id ?? 'unknown'}-${auth.user?.role ?? 'unknown'}`
    : `signed-out-${auth.otpChallenge?.challengeId ?? 'login'}`;

  useDeviceRegistrationOnForeground({
    enabled: auth.isAuthenticated && auth.destination === 'tabs',
  });
  if (auth.phase === 'loading') {
    return (
      <View style={styles.loading}>
        <ActivityIndicator size="large" color="#0D6E6E" />
      </View>
    );
  }

  if (auth.sessionExpired) {
    return <AuthNavigator key={`${navigationKey}-expired`} initialRouteName="SessionExpired" />;
  }

  switch (auth.destination) {
    case 'tabs':
      return <MainTabNavigator key={navigationKey} />;
    case 'web-only':
      return <WebOnlyScreen key={navigationKey} />;
    case 'auth':
    default:
      return (
        <AuthNavigator
          key={navigationKey}
          initialRouteName={auth.otpChallenge ? 'Otp' : 'Login'}
        />
      );
  }
}

const styles = StyleSheet.create({
  loading: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#F8FAFC',
  },
});
