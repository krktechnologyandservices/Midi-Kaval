import type {FirebaseMessagingTypes} from '@react-native-firebase/messaging';
import type {NavigationContainerRefWithCurrent} from '@react-navigation/native';
import {MainTabParamList} from '../../navigation/types';
import {notificationApiService} from '../notifications/NotificationApiService';
import {
  handleNotificationNavigation,
  NotificationPayload,
} from '../notifications/notificationNavigation';

export function parsePushData(
  data: FirebaseMessagingTypes.RemoteMessage['data'] | undefined,
): NotificationPayload {
  if (!data) {
    return {};
  }

  return {
    notificationId: readString(data.notificationId),
    eventType: readString(data.eventType),
    resourceId: readString(data.resourceId),
  };
}

function readString(value: unknown): string | undefined {
  if (typeof value !== 'string' || value.length === 0) {
    return undefined;
  }

  return value;
}

export function registerPushNotificationHandlers(
  navigationRef: NavigationContainerRefWithCurrent<MainTabParamList>,
  options: {
    isAuthenticated: () => boolean;
  },
): () => void {
  let messagingModule: typeof import('@react-native-firebase/messaging') | null = null;

  try {
    messagingModule = require('@react-native-firebase/messaging');
  } catch {
    return () => undefined;
  }

  const messaging = messagingModule.default;

  const navigateFromPayload = async (payload: NotificationPayload): Promise<void> => {
    if (!options.isAuthenticated()) {
      return;
    }

    try {
      await handleNotificationNavigation(payload, {
        markRead: id => notificationApiService.markRead(id),
        navigateToClaim: claimId => {
          if (!navigationRef.isReady()) {
            return;
          }

          navigationRef.navigate('More', {
            screen: 'TravelClaimForm',
            params: {claimId, mode: 'view'},
          });
        },
      });
    } catch (error) {
      console.warn('[PushNotification] Failed to handle notification tap.', error);
    }
  };

  const unsubscribeOpened = messaging().onNotificationOpenedApp(remoteMessage => {
    void navigateFromPayload(parsePushData(remoteMessage.data));
  });

  void messaging()
    .getInitialNotification()
    .then(remoteMessage => {
      if (remoteMessage) {
        void navigateFromPayload(parsePushData(remoteMessage.data));
      }
    });

  const unsubscribeForeground = messaging().onMessage(remoteMessage => {
    const payload = parsePushData(remoteMessage.data);
    console.info('[PushNotification] Foreground message received.', payload);
  });

  return () => {
    unsubscribeOpened();
    unsubscribeForeground();
  };
}
