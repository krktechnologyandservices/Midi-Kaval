import React, {useMemo, useState} from 'react';
import {
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {NativeStackScreenProps} from '@react-navigation/native-stack';
import {DuplicateMatchSheet} from '../../components/DuplicateMatchSheet';
import {useAuth} from '../../context/AuthContext';
import {CasesStackParamList} from '../../navigation/types';
import {
  buildDuplicateCheckRequest,
  CaseDuplicateMatchDto,
  DOMICILE_OPTIONS,
  OFFENCE_CLASSIFICATIONS,
} from '../../services/cases/case.models';
import {caseApiService} from '../../services/cases/CaseApiService';

type Props = NativeStackScreenProps<CasesStackParamList, 'CaseCreate'>;

type DuplicateCheckOutcome = 'proceed' | 'sheet-shown' | 'blocked';

export function CaseCreateScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();

  const [crimeNumber, setCrimeNumber] = useState('');
  const [stNumber, setStNumber] = useState('');
  const [beneficiaryName, setBeneficiaryName] = useState('');
  const [typeOfOffence, setTypeOfOffence] = useState('');
  const [offenceClassification, setOffenceClassification] = useState<
    (typeof OFFENCE_CLASSIFICATIONS)[number]
  >('Petty');
  const [domicile, setDomicile] =
    useState<(typeof DOMICILE_OPTIONS)[number]>('Urban');
  const [checkingDuplicate, setCheckingDuplicate] = useState(false);
  const [merging, setMerging] = useState(false);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [matches, setMatches] = useState<CaseDuplicateMatchDto[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [identifiersLocked, setIdentifiersLocked] = useState(false);

  const announcement = useMemo(() => {
    const count = matches.length;
    return count === 1
      ? '1 possible match found'
      : `${count} possible matches found`;
  }, [matches.length]);

  const saveDisabled =
    checkingDuplicate
    || merging
    || sheetOpen
    || identifiersLocked
    || !crimeNumber.trim()
    || !stNumber.trim()
    || !beneficiaryName.trim()
    || !typeOfOffence.trim();

  const onIdentifierChange = (crime: string, st: string): void => {
    setCrimeNumber(crime);
    setStNumber(st);
    if (identifiersLocked) {
      setIdentifiersLocked(false);
    }
  };

  const submit = async (): Promise<void> => {
    setErrorMessage(null);
    const outcome = await runDuplicateCheck(true);
    if (outcome === 'proceed') {
      await createAfterCleanCheck();
    }
  };

  const runDuplicateCheck = async (
    allowCreateOnNoMatch: boolean,
  ): Promise<DuplicateCheckOutcome> => {
    const checkBody = buildDuplicateCheckRequest(crimeNumber, stNumber);
    if (!checkBody.crimeNumber && !checkBody.stNumber) {
      setErrorMessage('At least one of crimeNumber or stNumber is required.');
      return 'blocked';
    }

    setCheckingDuplicate(true);
    try {
      const result = await caseApiService.checkDuplicate(checkBody);
      if (result.hasMatch) {
        if (!result.matches?.length) {
          setErrorMessage(
            'Possible duplicate detected. Verify Crime/ST and try again.',
          );
          return 'blocked';
        }

        setMatches(result.matches);
        setSheetOpen(true);
        return 'sheet-shown';
      }

      return allowCreateOnNoMatch ? 'proceed' : 'blocked';
    } catch (error) {
      setErrorMessage(caseApiService.extractErrorMessage(error));
      return 'blocked';
    } finally {
      setCheckingDuplicate(false);
    }
  };

  const createAfterCleanCheck = async (): Promise<void> => {
    setCheckingDuplicate(true);
    try {
      const created = await caseApiService.createCase({
        crimeNumber: crimeNumber.trim(),
        stNumber: stNumber.trim(),
        beneficiaryName: beneficiaryName.trim(),
        typeOfOffence: typeOfOffence.trim(),
        offenceClassification,
        domicile,
        isFirstTimeOffender: true,
      });

      Alert.alert('Case created.');
      navigation.navigate('CaseDetailPlaceholder', {
        caseId: created.id!,
        crimeNumber: created.crimeNumber,
        stNumber: created.stNumber,
        beneficiaryName: created.beneficiaryName,
        currentStage: created.currentStage,
        fromCreate: true,
      });
    } catch (error) {
      if (caseApiService.isConflict(error)) {
        const outcome = await runDuplicateCheck(false);
        if (outcome === 'blocked') {
          setErrorMessage(
            caseApiService.extractErrorMessage(error)
            || 'This Crime or ST number is already in use.',
          );
        }
        return;
      }

      setErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setCheckingDuplicate(false);
    }
  };

  const openExisting = (match: CaseDuplicateMatchDto): void => {
    if (!match.caseId) {
      return;
    }

    setSheetOpen(false);
    navigation.navigate('CaseDetailPlaceholder', {
      caseId: match.caseId,
      crimeNumber: match.crimeNumber ?? undefined,
      stNumber: match.stNumber ?? undefined,
      beneficiaryName: match.beneficiaryName ?? undefined,
      currentStage: match.currentStage ?? undefined,
      matchedOn: match.matchedOn ?? undefined,
    });
  };

  const cancelSheet = (): void => {
    setSheetOpen(false);
    setIdentifiersLocked(true);
  };

  const buildCreateBody = () => ({
    crimeNumber: crimeNumber.trim(),
    stNumber: stNumber.trim(),
    beneficiaryName: beneficiaryName.trim(),
    typeOfOffence: typeOfOffence.trim(),
    offenceClassification,
    domicile,
    isFirstTimeOffender: true,
  });

  const mergeFromSheet = async (match: CaseDuplicateMatchDto): Promise<void> => {
    if (!match.caseId) {
      return;
    }

    setErrorMessage(null);
    setMerging(true);
    try {
      const merged = await caseApiService.mergeCase(
        match.caseId,
        buildCreateBody(),
      );
      setSheetOpen(false);
      Alert.alert('Intake merged into existing case.');
      navigation.navigate('CaseDetailPlaceholder', {
        caseId: merged.id!,
        crimeNumber: merged.crimeNumber,
        stNumber: merged.stNumber,
        beneficiaryName: merged.beneficiaryName,
        currentStage: merged.currentStage,
        fromCreate: true,
      });
    } catch (error) {
      setErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setMerging(false);
    }
  };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Text style={styles.title}>New case</Text>

      <TextInput
        style={styles.input}
        placeholder="Crime number"
        value={crimeNumber}
        onChangeText={value => onIdentifierChange(value, stNumber)}
      />
      <TextInput
        style={styles.input}
        placeholder="ST number"
        value={stNumber}
        onChangeText={value => onIdentifierChange(crimeNumber, value)}
      />
      <TextInput
        style={styles.input}
        placeholder="Beneficiary name"
        value={beneficiaryName}
        onChangeText={setBeneficiaryName}
      />
      <TextInput
        style={styles.input}
        placeholder="Type of offence"
        value={typeOfOffence}
        onChangeText={setTypeOfOffence}
      />

      <Text style={styles.label}>Offence classification: {offenceClassification}</Text>
      <View style={styles.chipRow}>
        {OFFENCE_CLASSIFICATIONS.map(option => (
          <Pressable
            key={option}
            style={[
              styles.chip,
              offenceClassification === option && styles.chipSelected,
            ]}
            onPress={() => setOffenceClassification(option)}>
            <Text>{option}</Text>
          </Pressable>
        ))}
      </View>

      <Text style={styles.label}>Domicile: {domicile}</Text>
      <View style={styles.chipRow}>
        {DOMICILE_OPTIONS.map(option => (
          <Pressable
            key={option}
            style={[styles.chip, domicile === option && styles.chipSelected]}
            onPress={() => setDomicile(option)}>
            <Text>{option}</Text>
          </Pressable>
        ))}
      </View>

      {errorMessage ? (
        <Text style={styles.error} accessibilityLiveRegion="polite">
          {errorMessage}
        </Text>
      ) : null}

      <Pressable
        style={[styles.primaryButton, saveDisabled && styles.disabled]}
        disabled={saveDisabled}
        onPress={() => void submit()}>
        <Text style={styles.primaryButtonText}>
          {checkingDuplicate ? 'Checking…' : merging ? 'Merging…' : 'Save case'}
        </Text>
      </Pressable>

      <DuplicateMatchSheet
        visible={sheetOpen}
        matches={matches}
        canMerge={auth.isSupervisorRole}
        merging={merging}
        announcement={announcement}
        onOpenExisting={openExisting}
        onMerge={match => void mergeFromSheet(match)}
        onCancel={cancelSheet}
      />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    backgroundColor: '#f8fafc',
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
    marginBottom: 12,
  },
  input: {
    borderWidth: 1,
    borderColor: '#cbd5e1',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    backgroundColor: '#fff',
  },
  label: {
    marginTop: 8,
    marginBottom: 4,
    color: '#334155',
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 8,
  },
  chip: {
    borderWidth: 1,
    borderColor: '#cbd5e1',
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: '#fff',
  },
  chipSelected: {
    borderColor: '#0D6E6E',
  },
  error: {
    color: '#b91c1c',
    marginVertical: 8,
  },
  primaryButton: {
    backgroundColor: '#0D6E6E',
    borderRadius: 8,
    padding: 14,
    alignItems: 'center',
    marginTop: 8,
  },
  primaryButtonText: {
    color: '#fff',
    fontWeight: '600',
  },
  disabled: {
    opacity: 0.5,
  },
});
