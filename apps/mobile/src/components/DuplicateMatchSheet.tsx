import React, {useEffect, useState} from 'react';
import {
  BackHandler,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {CaseDuplicateMatchDto} from '../services/cases/case.models';
import {formatMatchedOn} from '../utils/matchedOnLabel';

interface DuplicateMatchSheetProps {
  visible: boolean;
  matches: CaseDuplicateMatchDto[];
  canMerge: boolean;
  announcement: string;
  merging: boolean;
  onOpenExisting: (match: CaseDuplicateMatchDto) => void;
  onMerge: (match: CaseDuplicateMatchDto) => void;
  onCancel: () => void;
}

export function DuplicateMatchSheet({
  visible,
  matches,
  canMerge,
  announcement,
  merging,
  onOpenExisting,
  onMerge,
  onCancel,
}: DuplicateMatchSheetProps): React.JSX.Element {
  const [confirmingCaseId, setConfirmingCaseId] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setConfirmingCaseId(null);
      return;
    }

    if (typeof BackHandler.addEventListener !== 'function') {
      return;
    }

    const subscription = BackHandler.addEventListener(
      'hardwareBackPress',
      () => true,
    );

    return () => subscription.remove();
  }, [visible]);

  return (
    <Modal
      visible={visible}
      animationType="fade"
      transparent
      accessibilityViewIsModal
      onRequestClose={() => {
        /* blocking — dismiss only via Cancel */
      }}>
      <View style={styles.backdrop}>
        <View
          style={styles.sheet}
          accessibilityLabel="Possible match review sheet">
          <Text style={styles.title} accessibilityRole="header">
            Possible match — review before saving.
          </Text>
          <Text style={styles.srOnly} accessibilityLiveRegion="polite">
            {announcement}
          </Text>

          {matches.map(match => {
            const isConfirming = confirmingCaseId === match.caseId;
            return (
              <View
                key={match.caseId}
                style={styles.matchRow}
                accessibilityLabel={`Match for ${match.beneficiaryName ?? 'case'}`}>
                <Text>Crime: {match.crimeNumber}</Text>
                <Text>ST: {match.stNumber}</Text>
                <Text>Beneficiary: {match.beneficiaryName}</Text>
                <Text>Stage: {match.currentStage}</Text>
                <Text style={styles.matchedOn}>
                  {formatMatchedOn(match.matchedOn)}
                </Text>

                {isConfirming ? (
                  <>
                    <Text style={styles.confirmCopy}>
                      Merge this intake into the existing case?
                    </Text>
                    <View style={styles.rowActions}>
                      <Pressable
                        style={styles.secondaryButton}
                        disabled={merging}
                        onPress={() => setConfirmingCaseId(null)}>
                        <Text style={styles.secondaryButtonText}>Back</Text>
                      </Pressable>
                      <Pressable
                        style={[
                          styles.primaryButton,
                          merging && styles.disabled,
                        ]}
                        disabled={merging}
                        onPress={() => onMerge(match)}>
                        <Text style={styles.primaryButtonText}>
                          {merging ? 'Merging…' : 'Confirm merge'}
                        </Text>
                      </Pressable>
                    </View>
                  </>
                ) : (
                  <View style={styles.rowActions}>
                    <Pressable
                      style={styles.secondaryButton}
                      onPress={() => onOpenExisting(match)}>
                      <Text style={styles.secondaryButtonText}>
                        Open existing
                      </Text>
                    </Pressable>
                    {canMerge ? (
                      <Pressable
                        style={styles.primaryButton}
                        onPress={() =>
                          match.caseId && setConfirmingCaseId(match.caseId)
                        }>
                        <Text style={styles.primaryButtonText}>Merge</Text>
                      </Pressable>
                    ) : null}
                  </View>
                )}
              </View>
            );
          })}

          <Pressable style={styles.cancelButton} onPress={onCancel}>
            <Text>Cancel</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: 16,
  },
  sheet: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    maxHeight: '90%',
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 12,
  },
  srOnly: {
    position: 'absolute',
    width: 1,
    height: 1,
    opacity: 0,
  },
  matchRow: {
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
  },
  matchedOn: {
    color: '#475569',
    marginTop: 4,
  },
  confirmCopy: {
    marginTop: 8,
    color: '#334155',
  },
  rowActions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
    flexWrap: 'wrap',
  },
  secondaryButton: {
    borderWidth: 1,
    borderColor: '#0D6E6E',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  secondaryButtonText: {
    color: '#0D6E6E',
  },
  primaryButton: {
    backgroundColor: '#0D6E6E',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  primaryButtonText: {
    color: '#fff',
    fontWeight: '600',
  },
  disabled: {
    opacity: 0.5,
  },
  cancelButton: {
    alignSelf: 'flex-end',
    padding: 8,
  },
});
