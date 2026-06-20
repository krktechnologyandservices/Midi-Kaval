import React, {useCallback, useState} from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {RouteProp, useFocusEffect, useNavigation, useRoute} from '@react-navigation/native';
import {NativeStackNavigationProp} from '@react-navigation/native-stack';
import {CaptureLandmarkModal} from '../../components/CaptureLandmarkModal';
import {DiscreetExpandModal} from '../../components/DiscreetExpandModal';
import {DiscreetHeader} from '../../components/DiscreetHeader';
import {RescheduleVisitModal} from '../../components/RescheduleVisitModal';
import {SyncChip} from '../../components/SyncChip';
import {useDiscreetCaseReveal} from '../../hooks/useDiscreetCaseReveal';
import {TodayStackParamList} from '../../navigation/types';
import {readCache, writeCache} from '../../services/visits/commandStripCache';
import {readOfflineQueue} from '../../services/sync/offlineQueue';
import {flushOfflineQueue} from '../../services/sync/mobileSyncPushService';
import {mergeQueueWithVisits} from '../../services/sync/mergeQueueWithVisits';
import {resolveVisitSyncChip} from '../../services/sync/resolveVisitSyncChip';
import {useSyncOnForeground} from '../../services/sync/useSyncOnForeground';
import {QueuedMutation} from '../../services/sync/syncMutationTypes';
import {useVisitNavigation} from '../../services/visits/useVisitNavigation';
import {visitApiService} from '../../services/visits/VisitApiService';
import {VisitListItemDto} from '../../services/visits/visit.models';

type ActiveVisitRoute = RouteProp<TodayStackParamList, 'ActiveVisit'>;
type ActiveVisitNavigation = NativeStackNavigationProp<
  TodayStackParamList,
  'ActiveVisit'
>;

export function ActiveVisitScreen(): React.JSX.Element {
  const route = useRoute<ActiveVisitRoute>();
  const navigation = useNavigation<ActiveVisitNavigation>();
  const [visit, setVisit] = useState<VisitListItemDto>(route.params.visit);
  const [offlineQueue, setOfflineQueue] = useState<QueuedMutation[]>([]);
  const caseSummary = visit.case;

  const refreshOfflineQueue = useCallback(async (): Promise<void> => {
    const queue = await readOfflineQueue();
    setOfflineQueue(queue);
  }, []);

  const syncChipQueueRefresh = useCallback(async (): Promise<void> => {
    const queueBeforeFlush = await readOfflineQueue();
    if (queueBeforeFlush.length) {
      setOfflineQueue(
        queueBeforeFlush.map(item => ({...item, syncStatus: 'pending' as const})),
      );
    }
    await flushOfflineQueue();
    await refreshOfflineQueue();
  }, [refreshOfflineQueue]);

  useFocusEffect(
    useCallback(() => {
      void refreshOfflineQueue();
    }, [refreshOfflineQueue]),
  );

  useSyncOnForeground({
    enabled: true,
    onSynced: syncChipQueueRefresh,
  });

  const syncChip = resolveVisitSyncChip(visit.id, offlineQueue);

  const navigateToSyncQueue = (): void => {
    navigation.getParent()?.navigate('More', {screen: 'SyncQueue'});
  };

  const {navigateToVisit, captureModalProps} = useVisitNavigation({
    onGpsVerified: (caseId, gps) => {
      if (visit.case?.id === caseId) {
        setVisit(prev => ({
          ...prev,
          case: {
            ...prev.case!,
            gpsVerified: gps.gpsVerified,
            latitude: gps.latitude ?? prev.case?.latitude,
            longitude: gps.longitude ?? prev.case?.longitude,
            landmark: gps.landmark ?? prev.case?.landmark,
          },
        }));
      }
    },
  });

  const [note, setNote] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [completing, setCompleting] = useState(false);
  const [rescheduleVisible, setRescheduleVisible] = useState(false);
  const [rescheduling, setRescheduling] = useState(false);
  const [rescheduleError, setRescheduleError] = useState<string | null>(null);

  const discreet = useDiscreetCaseReveal(caseSummary?.id ?? '', {
    crimeNumber: caseSummary?.crimeNumber,
    stNumber: caseSummary?.stNumber,
    domicile: caseSummary?.domicile,
    beneficiaryName: caseSummary?.beneficiaryName,
    sensitivityLevel: caseSummary?.sensitivityLevel,
  });

  const canComplete = note.trim().length > 0 && !completing;

  const returnToToday = (refreshToken: number): void => {
    navigation.navigate('TodayHome', {refreshToken});
  };

  const onComplete = async (): Promise<void> => {
    if (!visit.id || !canComplete) {
      return;
    }

    setCompleting(true);
    setErrorMessage(null);
    try {
      const completed = await visitApiService.completeVisit(visit, note.trim());
      const cached = await readCache();
      const queue = await readOfflineQueue();
      const cachedItems = cached?.items ?? [visit];
      const updatedItems = cachedItems.map(item =>
        item.id === visit.id ? completed : item,
      );
      await writeCache(mergeQueueWithVisits(updatedItems, queue), {
        customVisitOrder: cached?.customVisitOrder,
        routeGroupingActive: cached?.routeGroupingActive,
      });
      returnToToday(Date.now());
    } catch (error) {
      setErrorMessage(visitApiService.extractErrorMessage(error));
    } finally {
      setCompleting(false);
    }
  };

  const onReschedule = async (
    scheduledAtUtc: string,
    reason: string,
  ): Promise<void> => {
    if (!visit.id) {
      return;
    }

    setRescheduling(true);
    setRescheduleError(null);
    try {
      await visitApiService.rescheduleVisit(visit.id, scheduledAtUtc, reason);
      setRescheduleVisible(false);
      returnToToday(Date.now());
    } catch (error) {
      setRescheduleError(visitApiService.extractErrorMessage(error));
    } finally {
      setRescheduling(false);
    }
  };

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.card}>
        <SyncChip
          chip={syncChip}
          style={styles.syncChipPosition}
          onPress={syncChip.state === 'error' ? navigateToSyncQueue : undefined}
        />
        <Text style={styles.statusChip}>In progress</Text>
        <DiscreetHeader
          caseInfo={discreet.headerCase}
          expanded={discreet.expanded}
          expandLoading={discreet.expandLoading}
          onExpandPress={() => void discreet.onExpandPress()}
        />
      </View>

      <Text style={styles.label}>Visit note</Text>
      <TextInput
        style={styles.noteInput}
        value={note}
        onChangeText={setNote}
        multiline
        maxLength={4000}
        placeholder="What happened on this visit?"
        accessibilityLabel="Visit note"
      />

      {errorMessage ? (
        <View style={styles.errorBlock}>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable onPress={() => void onComplete()} accessibilityRole="button">
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </View>
      ) : null}

      <View style={styles.actions}>
        <Pressable
          style={styles.secondaryButton}
          onPress={() => void navigateToVisit(visit)}
          accessibilityRole="button">
          <Text style={styles.secondaryButtonText}>Navigate</Text>
        </Pressable>
        <Pressable
          style={styles.secondaryButton}
          onPress={() => {
            setRescheduleError(null);
            setRescheduleVisible(true);
          }}
          accessibilityRole="button">
          <Text style={styles.secondaryButtonText}>Reschedule</Text>
        </Pressable>
      </View>

      <Pressable
        style={[styles.primaryButton, !canComplete ? styles.primaryDisabled : null]}
        disabled={!canComplete}
        onPress={() => void onComplete()}
        accessibilityRole="button"
        accessibilityLabel="Complete visit">
        <Text style={styles.primaryButtonText}>
          {completing ? 'Completing…' : 'Complete visit'}
        </Text>
      </Pressable>

      <RescheduleVisitModal
        visible={rescheduleVisible}
        loading={rescheduling}
        errorMessage={rescheduleError}
        onClose={() => setRescheduleVisible(false)}
        onSubmit={(scheduledAtUtc, reason) => void onReschedule(scheduledAtUtc, reason)}
      />
      <DiscreetExpandModal
        visible={discreet.stepUpVisible}
        loading={discreet.stepUpLoading}
        errorMessage={discreet.stepUpError}
        onClose={discreet.closeStepUp}
        onSubmit={code => void discreet.onStepUpSubmit(code)}
      />
      <CaptureLandmarkModal {...captureModalProps} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F8FAFC',
  },
  content: {
    padding: 16,
    paddingBottom: 32,
  },
  card: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    position: 'relative',
  },
  syncChipPosition: {
    position: 'absolute',
    top: 12,
    right: 12,
    zIndex: 1,
  },
  statusChip: {
    alignSelf: 'flex-start',
    fontSize: 11,
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: 6,
    backgroundColor: '#EFF8FF',
    color: '#175CD3',
    marginBottom: 8,
    overflow: 'hidden',
  },
  crime: {
    fontWeight: '600',
    fontSize: 15,
    color: '#101828',
    paddingRight: 88,
  },
  meta: {
    fontSize: 13,
    color: '#475467',
    marginTop: 4,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 8,
  },
  noteInput: {
    minHeight: 120,
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 8,
    padding: 12,
    backgroundColor: '#fff',
    fontSize: 14,
    color: '#101828',
    textAlignVertical: 'top',
    marginBottom: 16,
  },
  errorBlock: {
    marginBottom: 12,
  },
  error: {
    color: '#B42318',
    marginBottom: 8,
  },
  retryText: {
    color: '#175CD3',
    fontWeight: '600',
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  secondaryButton: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#EAECF0',
    backgroundColor: '#F8FAFC',
    alignItems: 'center',
  },
  secondaryButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#101828',
  },
  primaryButton: {
    paddingVertical: 14,
    borderRadius: 8,
    backgroundColor: '#0D6E6E',
    alignItems: 'center',
  },
  primaryDisabled: {
    opacity: 0.5,
  },
  primaryButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#fff',
  },
});
