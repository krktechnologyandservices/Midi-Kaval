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
import {NativeStackScreenProps} from '@react-navigation/native-stack';
import {useAuth} from '../../context/AuthContext';
import {CasesStackParamList} from '../../navigation/types';
import {caseApiService} from '../../services/cases/CaseApiService';
import {CaseSummaryDto} from '../../services/cases/case.models';

type Props = NativeStackScreenProps<CasesStackParamList, 'CasesList'>;

export function CasesListScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();
  const [items, setItems] = useState<CaseSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadAssigned = useCallback(async () => {
    if (!auth.isFieldRole) {
      setItems([]);
      setLoading(false);
      return;
    }

    try {
      const result = await caseApiService.listAssignedCases();
      setItems(result.items ?? []);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [auth.isFieldRole]);

  useEffect(() => {
    void loadAssigned();
  }, [loadAssigned]);

  const onRefresh = (): void => {
    setRefreshing(true);
    void loadAssigned();
  };

  const isOverdue = (value?: string | null): boolean => {
    if (!value) {
      return false;
    }
    return new Date(value).getTime() < Date.now();
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={
        auth.isFieldRole ? (
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        ) : undefined
      }>
      <Text style={styles.title}>Cases</Text>
      <Text style={styles.subtitle}>
        {auth.isFieldRole ? 'Your assigned cases' : 'Case registry in Epic 2'}
      </Text>

      {!auth.isFieldRole ? (
        <Pressable
          style={styles.primaryButton}
          onPress={() => navigation.navigate('CaseCreate')}>
          <Text style={styles.primaryButtonText}>New case</Text>
        </Pressable>
      ) : null}

      {loading ? <ActivityIndicator style={styles.loader} /> : null}
      {errorMessage ? <Text style={styles.error}>{errorMessage}</Text> : null}

      {auth.isFieldRole
        ? items.map(item => (
            <Pressable
              key={item.id}
              style={styles.row}
              onPress={() =>
                navigation.navigate('CaseDetailPlaceholder', {
                  caseId: item.id!,
                })
              }>
              <Text style={styles.rowTitle}>
                {item.crimeNumber} / {item.stNumber}
              </Text>
              <Text style={styles.rowMeta}>Stage: {item.currentStage}</Text>
              {isOverdue(item.nextVisitDueAtUtc) ? (
                <Text style={styles.overdue}>Overdue visit</Text>
              ) : null}
            </Pressable>
          ))
        : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8fafc',
  },
  content: {
    padding: 16,
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
    marginBottom: 4,
  },
  subtitle: {
    color: '#475569',
    marginBottom: 16,
  },
  primaryButton: {
    alignSelf: 'flex-start',
    backgroundColor: '#0D6E6E',
    borderRadius: 8,
    paddingHorizontal: 16,
    paddingVertical: 10,
    marginBottom: 12,
  },
  primaryButtonText: {
    color: '#fff',
    fontWeight: '600',
  },
  loader: {
    marginTop: 12,
  },
  error: {
    color: '#b42318',
    marginBottom: 8,
  },
  row: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: '#e2e8f0',
  },
  rowTitle: {
    fontWeight: '600',
    marginBottom: 4,
  },
  rowMeta: {
    color: '#475569',
  },
  overdue: {
    marginTop: 4,
    color: '#b42318',
    fontWeight: '600',
  },
});
