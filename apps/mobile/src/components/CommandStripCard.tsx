import Geolocation from '@react-native-community/geolocation';
import React, {useState} from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {SyncChip} from './SyncChip';
import {SyncChipPresentation} from '../services/sync/resolveVisitSyncChip';
import {VisitListItemDto, VisitPlaceDto} from '../services/visits/visit.models';
import {openPlaceInMaps} from '../services/visits/placeNavigation';
import {visitApiService} from '../services/visits/VisitApiService';
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
  onPlaceLogged?: (visitId: string, place: VisitPlaceDto) => void;
};

function readDevicePosition(): Promise<{latitude: number; longitude: number}> {
  return new Promise((resolve, reject) => {
    Geolocation.getCurrentPosition(
      position =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        }),
      error => reject(error),
      {enableHighAccuracy: true, timeout: 15000, maximumAge: 10000},
    );
  });
}

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

function PlaceRow({
  visitId,
  place,
  onLogged,
}: {
  visitId: string;
  place: VisitPlaceDto;
  onLogged?: (visitId: string, place: VisitPlaceDto) => void;
}): React.JSX.Element {
  const [logging, setLogging] = useState(false);
  const [editingComment, setEditingComment] = useState(false);
  const [commentDraft, setCommentDraft] = useState(place.comment ?? '');
  const [savingComment, setSavingComment] = useState(false);
  const isLogged = !!place.loggedAtUtc;

  const startEditComment = (): void => {
    setCommentDraft(place.comment ?? '');
    setEditingComment(true);
  };

  const cancelEditComment = (): void => {
    setEditingComment(false);
  };

  const saveComment = async (): Promise<void> => {
    if (savingComment) {
      return;
    }

    setSavingComment(true);
    try {
      const updated = await visitApiService.updatePlaceComment(
        visitId,
        place.id,
        commentDraft.trim(),
      );
      onLogged?.(visitId, updated);
      setEditingComment(false);
    } catch (error) {
      Alert.alert(
        'Could not save note',
        error instanceof Error ? error.message : 'Check connection and try again.',
      );
    } finally {
      setSavingComment(false);
    }
  };

  const navigate = (): void => {
    void openPlaceInMaps({
      latitude: place.plannedLatitude,
      longitude: place.plannedLongitude,
      address: place.address,
    });
  };

  const logLocation = async (): Promise<void> => {
    if (logging || isLogged) {
      return;
    }

    setLogging(true);
    try {
      const position = await readDevicePosition();
      const updated = await visitApiService.logPlace(
        visitId,
        place.id,
        position.latitude,
        position.longitude,
      );
      onLogged?.(visitId, updated);
    } catch (error) {
      Alert.alert(
        'Could not log location',
        error instanceof Error ? error.message : 'Check location permission and try again.',
      );
    } finally {
      setLogging(false);
    }
  };

  return (
    <View style={styles.placeRow}>
      <Text style={styles.placeAddress}>{place.address}</Text>
      <Text style={styles.placeStatus}>
        {isLogged ? `Logged ${new Date(place.loggedAtUtc!).toLocaleString()}` : 'Not yet visited'}
      </Text>

      {editingComment ? (
        <View style={styles.commentEditRow}>
          <TextInput
            style={styles.commentInput}
            value={commentDraft}
            onChangeText={setCommentDraft}
            multiline
            maxLength={1000}
            placeholder="Add a note about this location…"
            placeholderTextColor="#98A2B3"
            accessibilityLabel={`Note for ${place.address}`}
          />
          <View style={styles.placeActions}>
            <Pressable
              style={[styles.placeButton, savingComment ? styles.primaryDisabled : null]}
              onPress={() => void saveComment()}
              disabled={savingComment}
              accessibilityRole="button"
              accessibilityLabel="Save note">
              {savingComment ? (
                <ActivityIndicator size="small" color="#0D6E6E" />
              ) : (
                <Text style={styles.placeButtonText}>Save note</Text>
              )}
            </Pressable>
            <Pressable
              style={styles.placeButton}
              onPress={cancelEditComment}
              accessibilityRole="button"
              accessibilityLabel="Cancel note edit">
              <Text style={styles.placeButtonText}>Cancel</Text>
            </Pressable>
          </View>
        </View>
      ) : (
        <>
          {place.comment ? <Text style={styles.placeComment}>{place.comment}</Text> : null}
          <View style={styles.placeActions}>
            <Pressable
              style={styles.placeButton}
              onPress={navigate}
              accessibilityRole="button"
              accessibilityLabel={`Navigate to ${place.address}`}>
              <Text style={styles.placeButtonText}>Navigate</Text>
            </Pressable>
            {!isLogged ? (
              <Pressable
                style={[styles.placeButton, logging ? styles.primaryDisabled : null]}
                onPress={() => void logLocation()}
                disabled={logging}
                accessibilityRole="button"
                accessibilityLabel={`Log location for ${place.address}`}>
                {logging ? (
                  <ActivityIndicator size="small" color="#0D6E6E" />
                ) : (
                  <Text style={styles.placeButtonText}>Log this location</Text>
                )}
              </Pressable>
            ) : null}
            <Pressable
              style={styles.placeButton}
              onPress={startEditComment}
              accessibilityRole="button"
              accessibilityLabel={`${place.comment ? 'Edit' : 'Add'} note for ${place.address}`}>
              <Text style={styles.placeButtonText}>{place.comment ? 'Edit note' : 'Add note'}</Text>
            </Pressable>
          </View>
        </>
      )}
    </View>
  );
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
  onPlaceLogged,
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

      {visit.id && visit.places && visit.places.length > 0 ? (
        <View style={styles.placesSection} accessibilityLabel="Places to visit">
          {visit.places.map(place => (
            <PlaceRow
              key={place.id}
              visitId={visit.id!}
              place={place}
              onLogged={onPlaceLogged}
            />
          ))}
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
  placesSection: {
    marginTop: 12,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#EAECF0',
    gap: 10,
  },
  placeRow: {
    gap: 4,
  },
  placeAddress: {
    fontSize: 13,
    fontWeight: '600',
    color: '#101828',
  },
  placeStatus: {
    fontSize: 12,
    color: '#667085',
  },
  placeComment: {
    fontSize: 12,
    color: '#475467',
    fontStyle: 'italic',
  },
  commentEditRow: {
    gap: 6,
  },
  commentInput: {
    borderWidth: 1,
    borderColor: '#D0D5DD',
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 13,
    color: '#101828',
    minHeight: 44,
    textAlignVertical: 'top',
  },
  placeActions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 4,
  },
  placeButton: {
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#0D6E6E',
    alignItems: 'center',
  },
  placeButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#0D6E6E',
  },
});
