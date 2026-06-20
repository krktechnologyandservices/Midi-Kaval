import Geolocation from '@react-native-community/geolocation';
import React, {useEffect, useState} from 'react';
import {
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {caseApiService} from '../services/cases/CaseApiService';
import {CaseGpsDto} from '../services/cases/case.models';

type Props = {
  visible: boolean;
  caseId: string | null;
  onClose: () => void;
  onSuccess: (gps: CaseGpsDto) => void;
};

type Coordinates = {
  latitude: number;
  longitude: number;
};

function readPosition(): Promise<Coordinates> {
  return new Promise((resolve, reject) => {
    Geolocation.getCurrentPosition(
      position => {
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        });
      },
      error => reject(error),
      {enableHighAccuracy: true, timeout: 15000, maximumAge: 10000},
    );
  });
}

export function CaptureLandmarkModal({
  visible,
  caseId,
  onClose,
  onSuccess,
}: Props): React.JSX.Element {
  const [landmark, setLandmark] = useState('');
  const [coords, setCoords] = useState<Coordinates | null>(null);
  const [locating, setLocating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [locationError, setLocationError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const loadLocation = (): void => {
    setLocating(true);
    setLocationError(null);
    void readPosition()
      .then(next => {
        setCoords(next);
      })
      .catch(() => {
        setLocationError(
          'Location permission is required to verify GPS. Enable it in Settings and try again.',
        );
      })
      .finally(() => {
        setLocating(false);
      });
  };

  useEffect(() => {
    if (visible) {
      setLandmark('');
      setCoords(null);
      setLocationError(null);
      setSubmitError(null);
      loadLocation();
    }
  }, [visible]);

  const canSubmit =
    landmark.trim().length > 0 && coords != null && !locating && !submitting;

  const onSave = async (): Promise<void> => {
    if (!caseId || !coords || !canSubmit) {
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const gps = await caseApiService.verifyCaseGps(caseId, {
        latitude: coords.latitude,
        longitude: coords.longitude,
        landmark: landmark.trim(),
      });
      onSuccess(gps);
    } catch (error) {
      setSubmitError(caseApiService.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.sheet}>
          <Text style={styles.title}>Capture landmark before navigate</Text>
          {locating ? (
            <Text style={styles.status}>Getting your location…</Text>
          ) : coords ? (
            <Text style={styles.status}>
              Location ready ({coords.latitude.toFixed(5)}, {coords.longitude.toFixed(5)})
            </Text>
          ) : null}
          {locationError ? (
            <View style={styles.errorBlock}>
              <Text style={styles.error}>{locationError}</Text>
              <Pressable onPress={loadLocation} accessibilityRole="button">
                <Text style={styles.retryText}>Retry location</Text>
              </Pressable>
            </View>
          ) : null}
          <Text style={styles.label}>Landmark (required)</Text>
          <TextInput
            style={styles.input}
            value={landmark}
            onChangeText={setLandmark}
            multiline
            maxLength={500}
            placeholder="Describe the location — e.g. near temple, blue gate, 2nd lane"
            accessibilityLabel="Landmark description"
          />
          {submitError ? (
            <View style={styles.errorBlock}>
              <Text style={styles.error}>{submitError}</Text>
              <Pressable onPress={() => void onSave()} accessibilityRole="button">
                <Text style={styles.retryText}>Retry</Text>
              </Pressable>
            </View>
          ) : null}
          <View style={styles.actions}>
            <Pressable style={styles.secondaryButton} onPress={onClose} accessibilityRole="button">
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable
              style={[styles.primaryButton, !canSubmit ? styles.primaryDisabled : null]}
              disabled={!canSubmit}
              onPress={() => void onSave()}
              accessibilityRole="button"
              accessibilityLabel="Save and navigate">
              <Text style={styles.primaryButtonText}>
                {submitting ? 'Saving…' : 'Save & navigate'}
              </Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(16, 24, 40, 0.45)',
    justifyContent: 'flex-end',
  },
  sheet: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    padding: 20,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 12,
  },
  status: {
    fontSize: 13,
    color: '#475467',
    marginBottom: 12,
  },
  label: {
    fontSize: 13,
    color: '#475467',
    marginBottom: 6,
  },
  input: {
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
    minHeight: 100,
    fontSize: 14,
    color: '#101828',
    textAlignVertical: 'top',
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
  },
  secondaryButton: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#EAECF0',
    alignItems: 'center',
  },
  secondaryButtonText: {
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
    opacity: 0.5,
  },
  primaryButtonText: {
    fontWeight: '600',
    color: '#fff',
  },
});
