import DateTimePicker from '@react-native-community/datetimepicker';
import React, {useEffect, useState} from 'react';
import {
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';

type Props = {
  visible: boolean;
  loading: boolean;
  errorMessage: string | null;
  onClose: () => void;
  onSubmit: (scheduledAtUtc: string, reason: string) => void;
};

function defaultScheduledDate(): Date {
  const next = new Date();
  next.setDate(next.getDate() + 1);
  next.setHours(10, 0, 0, 0);
  return next;
}

function formatDateTime(date: Date): string {
  return date.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function RescheduleVisitModal({
  visible,
  loading,
  errorMessage,
  onClose,
  onSubmit,
}: Props): React.JSX.Element {
  const [scheduledDate, setScheduledDate] = useState(defaultScheduledDate);
  const [reason, setReason] = useState('');
  const [showPicker, setShowPicker] = useState(Platform.OS === 'ios');

  useEffect(() => {
    if (visible) {
      setScheduledDate(defaultScheduledDate());
      setReason('');
      setShowPicker(Platform.OS === 'ios');
    }
  }, [visible]);

  const isFuture = scheduledDate.getTime() > Date.now();
  const canSubmit = reason.trim().length > 0 && isFuture && !loading;

  const onDateChange = (
    _event: unknown,
    date?: Date,
  ): void => {
    if (Platform.OS === 'android') {
      setShowPicker(false);
    }

    if (date) {
      setScheduledDate(date);
    }
  };

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.sheet}>
          <Text style={styles.title}>Reschedule visit</Text>
          <Text style={styles.label}>New date and time</Text>
          {Platform.OS === 'android' ? (
            <Pressable
              style={styles.dateButton}
              onPress={() => setShowPicker(true)}
              accessibilityRole="button"
              accessibilityLabel="Pick reschedule date and time">
              <Text style={styles.dateButtonText}>{formatDateTime(scheduledDate)}</Text>
            </Pressable>
          ) : (
            <Text style={styles.datePreview}>{formatDateTime(scheduledDate)}</Text>
          )}
          {showPicker ? (
            <DateTimePicker
              value={scheduledDate}
              mode="datetime"
              minimumDate={new Date()}
              onChange={onDateChange}
              accessibilityLabel="Reschedule date time"
            />
          ) : null}
          {!isFuture ? (
            <Text style={styles.validation}>Choose a future date and time.</Text>
          ) : null}
          <Text style={styles.label}>Reason (required)</Text>
          <TextInput
            style={[styles.input, styles.reasonInput]}
            value={reason}
            onChangeText={setReason}
            multiline
            maxLength={500}
            placeholder="Why is this visit being moved?"
            accessibilityLabel="Reschedule reason"
          />
          {errorMessage ? <Text style={styles.error}>{errorMessage}</Text> : null}
          <View style={styles.actions}>
            <Pressable
              style={styles.secondaryButton}
              onPress={onClose}
              accessibilityRole="button">
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable
              style={[styles.primaryButton, !canSubmit ? styles.primaryDisabled : null]}
              disabled={!canSubmit}
              onPress={() => onSubmit(scheduledDate.toISOString(), reason.trim())}
              accessibilityRole="button"
              accessibilityLabel="Confirm reschedule">
              <Text style={styles.primaryButtonText}>
                {loading ? 'Saving…' : 'Reschedule'}
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
  label: {
    fontSize: 13,
    color: '#475467',
    marginBottom: 6,
  },
  dateButton: {
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
    backgroundColor: '#F8FAFC',
  },
  dateButtonText: {
    fontSize: 14,
    color: '#101828',
  },
  datePreview: {
    fontSize: 14,
    color: '#101828',
    marginBottom: 8,
  },
  validation: {
    fontSize: 12,
    color: '#B42318',
    marginBottom: 8,
  },
  input: {
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
    fontSize: 14,
    color: '#101828',
  },
  reasonInput: {
    minHeight: 80,
    textAlignVertical: 'top',
  },
  error: {
    color: '#B42318',
    marginBottom: 12,
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
