import React, {useCallback, useEffect, useState} from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useNavigation} from '@react-navigation/native';
import {NativeStackNavigationProp} from '@react-navigation/native-stack';
import {SyncChip} from '../../components/SyncChip';
import {useAuth} from '../../context/AuthContext';
import {MoreStackParamList} from '../../navigation/types';
import {
  mergeQueueWithTravelClaims,
  TravelClaimListItem,
} from '../../services/sync/mergeQueueWithTravelClaims';
import {readOfflineQueue} from '../../services/sync/offlineQueue';
import {resolveTravelSyncChip} from '../../services/sync/resolveTravelSyncChip';
import {useSyncOnForeground} from '../../services/sync/useSyncOnForeground';
import {QueuedMutation} from '../../services/sync/syncMutationTypes';
import {travelClaimApiService} from '../../services/travel/TravelClaimApiService';

type Navigation = NativeStackNavigationProp<MoreStackParamList, 'TravelClaimsList'>;

function statusStyle(status: string | null | undefined) {
  switch (status) {
    case 'Submitted':
      return styles.statusSubmitted;
    case 'Approved':
      return styles.statusApproved;
    case 'Returned':
      return styles.statusReturned;
    default:
      return styles.statusDraft;
  }
}

function formatClaimDate(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value.slice(0, 10) : date.toLocaleDateString();
}

export function TravelClaimsListScreen(): React.JSX.Element {
  const auth = useAuth();
  const navigation = useNavigation<Navigation>();
  const [items, setItems] = useState<TravelClaimListItem[]>([]);
  const [offlineQueue, setOfflineQueue] = useState<QueuedMutation[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const load = useCallback(async (isRefresh = false): Promise<void> => {
    if (!auth.isFieldRole) {
      setItems([]);
      setOfflineQueue([]);
      setLoading(false);
      setRefreshing(false);
      return;
    }

    if (!isRefresh) {
      setLoading(true);
    }

    try {
      const [claims, queue] = await Promise.all([
        travelClaimApiService.listMine(),
        readOfflineQueue(),
      ]);
      setOfflineQueue(queue);
      setItems(mergeQueueWithTravelClaims(claims, queue));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(travelClaimApiService.extractErrorMessage(error));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [auth.isFieldRole]);

  useEffect(() => {
    void load();
  }, [load]);

  useSyncOnForeground({
    enabled: auth.isFieldRole,
    onSynced: async () => {
      await load(true);
    },
  });

  const onRefresh = (): void => {
    setRefreshing(true);
    void load(true);
  };

  const openClaim = (item: TravelClaimListItem): void => {
    if (item.isLocalOnly && item.localDraftKey) {
      navigation.navigate('TravelClaimForm', {
        localDraftKey: item.localDraftKey,
        mode: 'edit',
      });
      return;
    }

    if (!item.id) {
      return;
    }

    const mode = item.status === 'Draft' ? 'edit' : 'view';
    navigation.navigate('TravelClaimForm', {claimId: item.id, mode});
  };

  if (!auth.isFieldRole) {
    return (
      <View style={styles.container}>
        <Text style={styles.subtitle}>Travel claims are for field workers only.</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      {loading ? <ActivityIndicator accessibilityLabel="Loading travel claims" /> : null}

      {errorMessage ? (
        <>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable onPress={() => void load()} accessibilityRole="button">
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </>
      ) : null}

      {!loading && !errorMessage && items.length === 0 ? (
        <View style={styles.emptyBlock}>
          <Text style={styles.emptyState}>No claims yet</Text>
          <Pressable
            style={styles.createButton}
            onPress={() => navigation.navigate('TravelClaimForm', {mode: 'create'})}
            accessibilityRole="button"
            accessibilityLabel="Create claim">
            <Text style={styles.createButtonText}>Create claim</Text>
          </Pressable>
        </View>
      ) : null}

      {items.map(item => {
        const rowKey = item.localDraftKey ?? item.id ?? 'claim-row';
        const syncChip = item.isLocalOnly
          ? resolveTravelSyncChip(item.localDraftKey, offlineQueue)
          : null;

        return (
          <Pressable
            key={rowKey}
            style={styles.row}
            accessibilityRole="button"
            onPress={() => openClaim(item)}>
            <View style={styles.rowHeader}>
              <Text style={[styles.statusChip, statusStyle(item.status)]}>
                {item.status ?? 'Draft'}
              </Text>
              {syncChip ? <SyncChip chip={syncChip} /> : null}
            </View>
            <Text style={styles.destination}>{item.destination ?? '—'}</Text>
            <Text style={styles.meta}>
              {formatClaimDate(item.claimDate)} · {item.transportMode ?? '—'} · ₹
              {item.amount?.toFixed(2) ?? '0.00'}
            </Text>
          </Pressable>
        );
      })}

      {!loading && !errorMessage && items.length > 0 ? (
        <Pressable
          style={styles.createButton}
          onPress={() => navigation.navigate('TravelClaimForm', {mode: 'create'})}
          accessibilityRole="button"
          accessibilityLabel="Create claim">
          <Text style={styles.createButtonText}>Create claim</Text>
        </Pressable>
      ) : null}
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
  emptyBlock: {
    gap: 12,
    paddingVertical: 8,
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
  },
  rowHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 6,
    gap: 8,
  },
  statusChip: {
    fontSize: 12,
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 999,
    overflow: 'hidden',
  },
  statusDraft: {
    backgroundColor: '#f2f4f7',
    color: '#344054',
  },
  statusSubmitted: {
    backgroundColor: '#e0f2fe',
    color: '#026aa2',
  },
  statusApproved: {
    backgroundColor: '#ecfdf3',
    color: '#027a48',
  },
  statusReturned: {
    backgroundColor: '#fef3f2',
    color: '#b42318',
  },
  destination: {
    fontWeight: '600',
    fontSize: 15,
    marginBottom: 4,
  },
  meta: {
    color: '#475467',
    fontSize: 13,
  },
  createButton: {
    marginTop: 8,
    backgroundColor: '#0d6e6e',
    borderRadius: 8,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  createButtonText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 14,
  },
});
