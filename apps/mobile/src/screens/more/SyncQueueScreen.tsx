import React, {useCallback, useState} from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useFocusEffect} from '@react-navigation/native';
import {readCache} from '../../services/visits/commandStripCache';
import {readOfflineQueue} from '../../services/sync/offlineQueue';
import {flushOfflineQueue} from '../../services/sync/mobileSyncPushService';
import {resolveVisitSyncChip} from '../../services/sync/resolveVisitSyncChip';
import {resolveTravelSyncChip} from '../../services/sync/resolveTravelSyncChip';
import {
  mutationTypeLabel,
  resolveSyncQueueRowTitle,
} from '../../services/sync/resolveSyncQueueLabel';
import {
  isTravelClaimMutation,
  isVisitMutation,
  QueuedMutation,
} from '../../services/sync/syncMutationTypes';

function resolveRowSyncChip(item: QueuedMutation) {
  if (isVisitMutation(item)) {
    return resolveVisitSyncChip(item.visitId, [item]);
  }

  if (isTravelClaimMutation(item)) {
    return resolveTravelSyncChip(item.localDraftKey, [item]);
  }

  return resolveVisitSyncChip(undefined, []);
}

export function SyncQueueScreen(): React.JSX.Element {
  const [queue, setQueue] = useState<QueuedMutation[]>([]);
  const [cacheTitles, setCacheTitles] = useState<
    {id?: string; case?: {crimeNumber?: string; stNumber?: string}}[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [retrying, setRetrying] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const refreshQueue = useCallback(async (): Promise<void> => {
    const [nextQueue, cache] = await Promise.all([
      readOfflineQueue(),
      readCache(),
    ]);
    setQueue(nextQueue);
    setCacheTitles(cache?.items ?? []);
    setLoading(false);
  }, []);

  useFocusEffect(
    useCallback(() => {
      void refreshQueue();
    }, [refreshQueue]),
  );

  const onRetry = async (): Promise<void> => {
    if (!queue.length || retrying) {
      return;
    }

    setRetrying(true);
    setErrorMessage(null);
    setQueue(prev => prev.map(item => ({...item, syncStatus: 'pending' as const})));
    try {
      await flushOfflineQueue();
      await refreshQueue();
    } catch {
      setErrorMessage('Could not sync — check connection and try again.');
    } finally {
      setRetrying(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color="#0D6E6E" />
      </View>
    );
  }

  if (!queue.length) {
    return (
      <View style={styles.centered}>
        <Text style={styles.emptyText}>All queued changes are synced.</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={queue}
        keyExtractor={item => item.clientMutationId}
        contentContainerStyle={styles.listContent}
        renderItem={({item}) => {
          const chip = resolveRowSyncChip(item);
          return (
            <View style={styles.row}>
              <Text style={styles.rowTitle}>
                {resolveSyncQueueRowTitle(item, cacheTitles)}
              </Text>
              <Text style={styles.rowSubtitle}>
                {mutationTypeLabel(item.type)} · {chip.label}
              </Text>
              {item.syncStatus === 'error' ? (
                <Text style={styles.rowError}>
                  {item.lastError ?? 'Sync failed — try again.'}
                </Text>
              ) : null}
            </View>
          );
        }}
      />
      {errorMessage ? <Text style={styles.bannerError}>{errorMessage}</Text> : null}
      <Pressable
        style={[styles.retryButton, retrying ? styles.retryDisabled : null]}
        disabled={retrying}
        onPress={() => void onRetry()}
        accessibilityRole="button"
        accessibilityLabel="Retry sync">
        <Text style={styles.retryButtonText}>
          {retrying ? 'Syncing…' : 'Retry sync'}
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F8FAFC',
    padding: 16,
  },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
    backgroundColor: '#F8FAFC',
  },
  emptyText: {
    fontSize: 16,
    color: '#475467',
    textAlign: 'center',
  },
  listContent: {
    paddingBottom: 16,
  },
  row: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#EAECF0',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
  },
  rowTitle: {
    fontSize: 15,
    fontWeight: '600',
    color: '#101828',
  },
  rowSubtitle: {
    fontSize: 13,
    color: '#475467',
    marginTop: 4,
  },
  rowError: {
    fontSize: 13,
    color: '#B42318',
    marginTop: 8,
  },
  bannerError: {
    color: '#B42318',
    marginBottom: 8,
    textAlign: 'center',
  },
  retryButton: {
    paddingVertical: 14,
    borderRadius: 8,
    backgroundColor: '#0D6E6E',
    alignItems: 'center',
  },
  retryDisabled: {
    opacity: 0.6,
  },
  retryButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#fff',
  },
});
