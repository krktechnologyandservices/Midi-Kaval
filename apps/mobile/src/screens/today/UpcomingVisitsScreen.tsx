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
import {visitApiService} from '../../services/visits/VisitApiService';
import {VisitListItemDto} from '../../services/visits/visit.models';
import {beneficiaryInitials} from '../../utils/beneficiaryInitials';
import {formatDaysUntilLabel} from '../../utils/courtSittingUtils';

type Navigation = NativeStackNavigationProp<TodayStackParamList, 'UpcomingVisits'>;

function isStartOfTodayOrLater(scheduledAtUtc: string | null | undefined): boolean {
  if (!scheduledAtUtc) {
    return false;
  }

  const scheduled = new Date(scheduledAtUtc);
  if (Number.isNaN(scheduled.getTime())) {
    return false;
  }

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  return scheduled.getTime() >= startOfToday.getTime();
}

/** Rest-of-week visits, excluding today's (already shown on the Today tab) and
 * anything already completed. */
function isUpcoming(item: VisitListItemDto): boolean {
  if (item.status === 'Completed') {
    return false;
  }

  if (!item.scheduledAtUtc) {
    return false;
  }

  const scheduled = new Date(item.scheduledAtUtc);
  const startOfTomorrow = new Date();
  startOfTomorrow.setHours(0, 0, 0, 0);
  startOfTomorrow.setDate(startOfTomorrow.getDate() + 1);
  return scheduled.getTime() >= startOfTomorrow.getTime();
}

export function UpcomingVisitsScreen(): React.JSX.Element {
  const auth = useAuth();
  const navigation = useNavigation<Navigation>();
  const [items, setItems] = useState<VisitListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const upcomingItems = useMemo(() => items.filter(isUpcoming), [items]);

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
      const result = await visitApiService.listWeekly();
      // The generated VisitListResultDto's nested `case` type predates the locally
      // patched VisitListItemDto (see visit.models.ts) — same known mismatch already
      // present elsewhere (e.g. TodayScreen.tsx), not something introduced here.
      const weeklyItems = (result.items ?? []) as unknown as VisitListItemDto[];
      setItems(weeklyItems.filter(item => isStartOfTodayOrLater(item.scheduledAtUtc)));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(visitApiService.extractErrorMessage(error));
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
        <Text style={styles.subtitle}>Upcoming visits is for field workers only.</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      {loading ? <ActivityIndicator accessibilityLabel="Loading upcoming visits" /> : null}

      {errorMessage ? (
        <>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable onPress={() => void load()} accessibilityRole="button">
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </>
      ) : null}

      {!loading && !errorMessage && upcomingItems.length === 0 ? (
        <Text style={styles.emptyState}>No upcoming visits scheduled this week.</Text>
      ) : null}

      {upcomingItems.map(item => (
        <Pressable
          key={item.id}
          style={[styles.row, item.isOverdue ? styles.rowOverdue : null]}
          accessibilityRole="button"
          onPress={() => openCase(item.case?.id)}>
          <View style={styles.rowHeader}>
            <Text style={styles.statusChip}>{item.status}</Text>
            {item.isOverdue ? <Text style={styles.overdueChip}>Overdue</Text> : null}
          </View>
          <Text style={styles.caseName}>
            {item.case
              ? `${beneficiaryInitials(item.case.beneficiaryName)} · ${item.case.crimeNumber ?? '—'}`
              : 'Unknown case'}
          </Text>
          <Text style={styles.meta}>
            {item.scheduledAtUtc
              ? `${new Date(item.scheduledAtUtc).toLocaleString()} · ${formatDaysUntilLabel(item.scheduledAtUtc, item.isOverdue)}`
              : 'No date'}
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
  rowOverdue: {
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
  caseName: {
    fontWeight: '600',
    fontSize: 15,
    marginBottom: 4,
  },
  meta: {
    color: '#667085',
    fontSize: 13,
  },
});
