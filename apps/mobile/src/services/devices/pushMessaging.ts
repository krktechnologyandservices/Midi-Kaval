import {Platform} from 'react-native';

export async function resolvePushToken(): Promise<string | null> {
  try {
    const messagingModule = require('@react-native-firebase/messaging');
    const messaging = messagingModule.default;

    if (Platform.OS === 'ios') {
      await messaging().requestPermission();
    }

    const token = await messaging().getToken();
    return token || null;
  } catch {
    if (__DEV__) {
      return `dev-stub-token-${Platform.OS}`;
    }

    return null;
  }
}
