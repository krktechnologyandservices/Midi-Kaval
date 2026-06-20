import React, {useCallback, useState} from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useFocusEffect, useNavigation} from '@react-navigation/native';
import {NativeStackNavigationProp} from '@react-navigation/native-stack';
import {useAuth} from '../../context/AuthContext';
import {MoreStackParamList} from '../../navigation/types';
import {notificationApiService} from '../../services/notifications/NotificationApiService';
import {
  handleNotificationNavigation,
  toNotificationPayload,
} from '../../services/notifications/notificationNavigation';
import {NotificationDto} from '../../services/notifications/notification.models';

type Navigation = NativeStackNavigationProp<MoreStackParamList, 'NotificationsList'>;

function formatRelativeTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  const diffMs = Date.now() - date.getTime();
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) {
    return 'Just now';
  }

  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }

  const days = Math.floor(hours / 24);
  if (days < 7) {
    return `${days}d ago`;
  }

  return date.toLocaleDateString();
}

export async function handleNotificationPress(
  item: NotificationDto,
  markRead: (id: string) => Promise<void>,
  navigateToClaim: (claimId: string) => void,
): Promise<void> {
  if (!item.id) {
    return;
  }

  await handleNotificationNavigation(toNotificationPayload(item), {
    markRead,
    navigateToClaim,
    markReadBeforeNavigate: !item.isRead,
  });
}

export function NotificationsListScreen(): React.JSX.Element {
  const auth = useAuth();
  const navigation = useNavigation<Navigation>();
  const [items, setItems] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const load = useCallback(async (isRefresh = false): Promise<void> => {
    if (!auth.isFieldRole) {
      setItems([]);
      setLoading(false);
      setRefreshing(false);
      return;
    }

    if (!isRefresh) {
      setLoading(true);
    }

    try {
      const notifications = await notificationApiService.list();
      setItems(notifications);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(notificationApiService.extractErrorMessage(error));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [auth.isFieldRole]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  const onRefresh = (): void => {
    setRefreshing(true);
    void load(true);
  };

  const onRowPress = (item: NotificationDto): void => {
    void handleNotificationPress(
      item,
      id => notificationApiService.markRead(id),
      claimId =>
        navigation.navigate('TravelClaimForm', {claimId, mode: 'view'}),
    )
      .then(() => {
        if (item.id && !item.isRead) {
          setItems(prev =>
            prev.map(notification =>
              notification.id === item.id
                ? {...notification, isRead: true}
                : notification,
            ),
          );
        }
      })
      .catch(error => {
        setErrorMessage(notificationApiService.extractErrorMessage(error));
      });
  };

  if (!auth.isFieldRole) {
    return (
      <View style={styles.container}>
        <Text style={styles.subtitle}>Notifications are for field workers only.</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      {loading ? <ActivityIndicator accessibilityLabel="Loading notifications" /> : null}

      {errorMessage ? (
        <>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable onPress={() => void load()} accessibilityRole="button">
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </>
      ) : null}

      {!loading && !errorMessage && items.length === 0 ? (
        <Text style={styles.emptyState}>You're up to date.</Text>
      ) : null}

      {items.map(item => (
        <Pressable
          key={item.id ?? `${item.eventType}-${item.createdAtUtc}`}
          style={[styles.row, item.isRead ? null : styles.rowUnread]}
          accessibilityRole="button"
          onPress={() => onRowPress(item)}>
          <Text style={styles.title}>{item.title ?? 'Notification'}</Text>
          <Text style={styles.body}>{item.body ?? ''}</Text>
          <Text style={styles.meta}>{formatRelativeTime(item.createdAtUtc)}</Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
  },
  content: {
    padding: 16,
    gap: 12,
  },
  subtitle: {
    padding: 16,
    color: '#475467',
  },
  error: {
    color: '#b42318',
  },
  retryText: {
    color: '#175cd3',
    marginTop: 8,
  },
  emptyState: {
    color: '#475467',
    fontSize: 15,
  },
  row: {
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    backgroundColor: '#fff',
  },
  rowUnread: {
    borderColor: '#0d6e6e',
    backgroundColor: '#f0fdf9',
  },
  title: {
    fontWeight: '600',
    fontSize: 15,
    color: '#101828',
    marginBottom: 4,
  },
  body: {
    fontSize: 14,
    color: '#475467',
    marginBottom: 6,
  },
  meta: {
    fontSize: 12,
    color: '#667085',
  },
});
