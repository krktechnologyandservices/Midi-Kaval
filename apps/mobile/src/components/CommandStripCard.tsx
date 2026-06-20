import React from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import {SyncChip} from './SyncChip';
import {SyncChipPresentation} from '../services/sync/resolveVisitSyncChip';
import {VisitListItemDto} from '../services/visits/visit.models';
import {beneficiaryInitials, isPocsoCase} from '../utils/beneficiaryInitials';

type Props = {
  visit: VisitListItemDto;
  visitIndex: number;
  syncChip: SyncChipPresentation;
  distanceKm?: number | null;
  startVisitLoading?: boolean;
  onNavigate: () => void;
  onStartVisit: () => void;
  onOpenCase: () => void;
  onSyncChipPress?: () => void;
};

function buildWhisperLine(
  whisper: NonNullable<VisitListItemDto['handoffWhisper']>,
): string {
  const parts = [
    whisper.priorActions ? `Prior: ${whisper.priorActions}` : null,
    whisper.openItems ? `Open: ${whisper.openItems}` : null,
    whisper.nextVisitPurpose ? `Next: ${whisper.nextVisitPurpose}` : null,
  ].filter(Boolean);

  return parts.length > 0 ? parts.join(' · ') : 'Handoff summary available';
}

export function CommandStripCard({
  visit,
  visitIndex,
  syncChip,
  distanceKm = null,
  startVisitLoading = false,
  onNavigate,
  onStartVisit,
  onOpenCase,
  onSyncChipPress,
}: Props): React.JSX.Element {
  const caseSummary = visit.case;
  const pocso = isPocsoCase(caseSummary?.sensitivityLevel);
  const crimeLine = pocso
    ? `${beneficiaryInitials(caseSummary?.beneficiaryName)} · ${caseSummary?.crimeNumber ?? '—'}`
    : `${caseSummary?.crimeNumber ?? '—'} · ${caseSummary?.stNumber ?? '—'}`;
  const distanceSuffix =
    caseSummary?.gpsVerified && distanceKm !== null && distanceKm !== undefined
      ? ` · ${distanceKm.toFixed(1)} km`
      : '';
  const metaLine = pocso
    ? `Visit ${visitIndex + 1}${distanceSuffix}${
        !caseSummary?.gpsVerified ? ' · GPS unverified' : ''
      }`
    : `Visit ${visitIndex + 1} · ${caseSummary?.domicile ?? '—'}${distanceSuffix}${
        !caseSummary?.gpsVerified ? ' · GPS unverified' : ''
      }`;
  const startLabel =
    visit.status === 'InProgress' ? 'Continue visit' : 'Start visit';
  const startAccessibilityLabel = startLabel;

  return (
    <View
      style={[styles.card, visit.isOverdue ? styles.cardOverdue : null]}
      accessibilityLabel={`Visit card ${visitIndex + 1}`}>
      <SyncChip
        chip={syncChip}
        style={styles.syncChipPosition}
        onPress={syncChip.state === 'error' ? onSyncChipPress : undefined}
      />

      <Pressable onPress={onOpenCase} accessibilityRole="button">
        <Text style={styles.crime}>{crimeLine}</Text>
        <Text style={styles.meta}>{metaLine}</Text>
      </Pressable>

      {visit.handoffWhisper ? (
        <View style={styles.whisper} accessibilityLabel="Handoff summary">
          <Text style={styles.whisperText} numberOfLines={3}>
            Handoff: {buildWhisperLine(visit.handoffWhisper)}
          </Text>
        </View>
      ) : null}

      <View style={styles.actions}>
        <Pressable
          style={styles.secondaryButton}
          onPress={onNavigate}
          accessibilityRole="button"
          accessibilityLabel="Navigate to visit">
          <Text style={styles.secondaryButtonText}>Navigate</Text>
        </Pressable>
        <Pressable
          style={[styles.primaryButton, startVisitLoading ? styles.primaryDisabled : null]}
          onPress={onStartVisit}
          disabled={startVisitLoading}
          accessibilityRole="button"
          accessibilityLabel={startAccessibilityLabel}>
          <Text style={styles.primaryButtonText}>
            {startVisitLoading ? 'Starting…' : startLabel}
          </Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    marginBottom: 12,
    padding: 16,
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#EAECF0',
    borderRadius: 12,
    position: 'relative',
  },
  cardOverdue: {
    borderLeftWidth: 4,
    borderLeftColor: '#B42318',
  },
  syncChipPosition: {
    position: 'absolute',
    top: 12,
    right: 12,
    zIndex: 1,
  },
  crime: {
    fontWeight: '600',
    fontSize: 15,
    color: '#101828',
    fontVariant: ['tabular-nums'],
    paddingRight: 88,
  },
  meta: {
    fontSize: 13,
    color: '#475467',
    marginTop: 4,
  },
  whisper: {
    marginTop: 12,
    padding: 10,
    backgroundColor: '#EFF8FF',
    borderLeftWidth: 3,
    borderLeftColor: '#175CD3',
  },
  whisperText: {
    fontSize: 12,
    color: '#101828',
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 14,
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
    flex: 1,
    paddingVertical: 12,
    borderRadius: 8,
    backgroundColor: '#0D6E6E',
    alignItems: 'center',
  },
  primaryDisabled: {
    opacity: 0.6,
  },
  primaryButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#fff',
  },
});
