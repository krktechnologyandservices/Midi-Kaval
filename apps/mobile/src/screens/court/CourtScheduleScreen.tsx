import React, {useCallback, useEffect, useMemo, useState} from 'react';
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
import {useAuth} from '../../context/AuthContext';
import {TodayStackParamList} from '../../navigation/types';
import {courtApiService} from '../../services/court/CourtApiService';
import {CourtSittingScheduleItemDto} from '../../services/cases/case.models';
import {beneficiaryInitials} from '../../utils/beneficiaryInitials';
import {getUtcWeekBounds} from '../../utils/utcWeekBounds';

type Navigation = NativeStackNavigationProp<TodayStackParamList, 'CourtSchedule'>;

function isInCurrentUtcWeek(scheduledAtUtc: string | null | undefined): boolean {
  if (!scheduledAtUtc) {
    return false;
  }

  const scheduled = new Date(scheduledAtUtc);
  if (Number.isNaN(scheduled.getTime())) {
    return false;
  }

  const {start, end} = getUtcWeekBounds();
  return scheduled.getTime() >= start.getTime() && scheduled.getTime() <= end.getTime();
}

export function CourtScheduleScreen(): React.JSX.Element {
  const auth = useAuth();
  const navigation = useNavigation<Navigation>();
  const [items, setItems] = useState<CourtSittingScheduleItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const weekItems = useMemo(
    () => items.filter(item => isInCurrentUtcWeek(item.scheduledAtUtc)),
    [items],
  );

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
      const upcoming = await courtApiService.listUpcomingCourtSittings();
      setItems(upcoming);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(courtApiService.extractErrorMessage(error));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [auth.isFieldRole]);

  useEffect(() => {
    void load();
  }, [load]);

  const onRefresh = (): void => {
    setRefreshing(true);
    void load(true);
  };

  const openCase = (caseId: string | undefined): void => {
    if (!caseId) {
      return;
    }

    navigation.getParent()?.navigate('Cases', {
      screen: 'CaseDetailPlaceholder',
      params: {caseId},
    });
  };

  if (!auth.isFieldRole) {
    return (
      <View style={styles.container}>
        <Text style={styles.subtitle}>Court schedule is for field workers only.</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      {loading ? <ActivityIndicator accessibilityLabel="Loading court schedule" /> : null}

      {errorMessage ? (
        <>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable onPress={() => void load()} accessibilityRole="button">
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </>
      ) : null}

      {!loading && !errorMessage && weekItems.length === 0 ? (
        <Text style={styles.emptyState}>No sittings this week.</Text>
      ) : null}

      {weekItems.map(item => (
        <Pressable
          key={item.id}
          style={[styles.row, item.isPastDue ? styles.rowPastDue : null]}
          accessibilityRole="button"
          onPress={() => openCase(item.caseId)}>
          <View style={styles.rowHeader}>
            <Text style={styles.statusChip}>{item.status}</Text>
            {item.isPastDue ? <Text style={styles.overdueChip}>Overdue</Text> : null}
          </View>
          <Text style={styles.courtName}>{item.courtName}</Text>
          <Text style={styles.purpose}>{item.purpose}</Text>
          <Text style={styles.meta}>
            {item.scheduledAtUtc
              ? new Date(item.scheduledAtUtc).toLocaleString()
              : 'No date'}
            {item.case
              ? ` · ${beneficiaryInitials(item.case.beneficiaryName)} · ${item.case.crimeNumber ?? '—'}`
              : ''}
          </Text>
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
  },
  rowPastDue: {
    borderLeftWidth: 4,
    borderLeftColor: '#b54708',
    backgroundColor: '#fffaeb',
  },
  rowHeader: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 6,
  },
  statusChip: {
    fontSize: 12,
    backgroundColor: '#e8f0fe',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 999,
    overflow: 'hidden',
  },
  overdueChip: {
    fontSize: 12,
    backgroundColor: '#b54708',
    color: '#fff',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 999,
    overflow: 'hidden',
  },
  courtName: {
    fontWeight: '600',
    fontSize: 15,
    marginBottom: 4,
  },
  purpose: {
    color: '#344054',
    marginBottom: 4,
  },
  meta: {
    color: '#667085',
    fontSize: 13,
  },
});
