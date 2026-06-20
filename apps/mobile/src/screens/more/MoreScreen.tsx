import React from 'react';
import {View, Text, Pressable, StyleSheet} from 'react-native';
import {useNavigation} from '@react-navigation/native';
import {NativeStackNavigationProp} from '@react-navigation/native-stack';
import {useAuth} from '../../context/AuthContext';
import {MoreStackParamList} from '../../navigation/types';
import {useUnreadCount} from '../../services/notifications/UnreadCountContext';

type MoreNavigation = NativeStackNavigationProp<MoreStackParamList, 'MoreHome'>;

export function MoreScreen(): React.JSX.Element {
  const auth = useAuth();
  const navigation = useNavigation<MoreNavigation>();
  const {unreadCount} = useUnreadCount();

  return (
    <View style={styles.container}>
      <Text style={styles.title}>More</Text>
      {auth.user ? (
        <Text style={styles.meta}>
          {auth.user.email} ({auth.user.role})
        </Text>
      ) : null}
      <Pressable
        style={styles.menuRow}
        onPress={() => navigation.navigate('NotificationsList')}
        accessibilityRole="button"
        accessibilityLabel="Notifications">
        <View style={styles.menuRowInner}>
          <Text style={styles.menuRowText}>Notifications</Text>
          {unreadCount != null && unreadCount > 0 ? (
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{unreadCount > 99 ? '99+' : unreadCount}</Text>
            </View>
          ) : null}
        </View>
      </Pressable>
      <Pressable
        style={styles.menuRow}
        onPress={() => navigation.navigate('TravelClaimsList')}
        accessibilityRole="button"
        accessibilityLabel="Travel claims">
        <Text style={styles.menuRowText}>Travel</Text>
      </Pressable>
      <Pressable
        style={styles.menuRow}
        onPress={() => navigation.navigate('SyncQueue')}
        accessibilityRole="button"
        accessibilityLabel="Sync queue">
        <Text style={styles.menuRowText}>Sync queue</Text>
      </Pressable>
      <Pressable
        style={styles.button}
        onPress={() => void auth.logout()}
        accessibilityRole="button">
        <Text style={styles.buttonText}>Log out</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 24,
    backgroundColor: '#F8FAFC',
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 8,
  },
  meta: {
    fontSize: 14,
    color: '#475467',
    marginBottom: 24,
  },
  menuRow: {
    borderWidth: 1,
    borderColor: '#EAECF0',
    borderRadius: 8,
    minHeight: 44,
    paddingHorizontal: 16,
    alignSelf: 'stretch',
    backgroundColor: '#fff',
    marginBottom: 12,
  },
  menuRowInner: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  menuRowText: {
    color: '#101828',
    fontWeight: '600',
    fontSize: 14,
  },
  badge: {
    backgroundColor: '#0D6E6E',
    borderRadius: 12,
    minWidth: 22,
    height: 22,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 6,
  },
  badgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '700',
  },
  button: {
    borderWidth: 1,
    borderColor: '#0D6E6E',
    borderRadius: 8,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
    alignSelf: 'flex-start',
  },
  buttonText: {
    color: '#0D6E6E',
    fontWeight: '600',
  },
});
