import React, {useEffect, useState} from 'react';
import {
  BackHandler,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {AccessibleErrorRegion} from './AccessibleErrorRegion';

type Props = {
  visible: boolean;
  loading: boolean;
  errorMessage: string | null;
  onClose: () => void;
  onSubmit: (code: string) => void;
};

export function DiscreetExpandModal({
  visible,
  loading,
  errorMessage,
  onClose,
  onSubmit,
}: Props): React.JSX.Element {
  const [code, setCode] = useState('');

  useEffect(() => {
    if (visible) {
      setCode('');
    }
  }, [visible]);

  useEffect(() => {
    if (!visible) {
      return undefined;
    }

    const subscription = BackHandler.addEventListener('hardwareBackPress', () => true);
    return () => subscription.remove();
  }, [visible]);

  const handleSubmit = (): void => {
    if (!/^\d{6}$/.test(code.trim())) {
      return;
    }

    onSubmit(code.trim());
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      accessibilityViewIsModal
      onRequestClose={onClose}>
      <View style={styles.overlay}>
        <View style={styles.sheet} accessibilityLiveRegion="polite">
          <Text style={styles.title}>Verify to show full detail</Text>
          <Text style={styles.subtitle}>
            Enter the 6-digit code sent to your email.
          </Text>

          <TextInput
            style={styles.input}
            value={code}
            onChangeText={setCode}
            keyboardType="number-pad"
            maxLength={6}
            editable={!loading}
            accessibilityLabel="6-digit step-up code"
          />

          {errorMessage ? (
            <AccessibleErrorRegion message={errorMessage} />
          ) : null}

          <View style={styles.actions}>
            <Pressable
              style={styles.secondaryButton}
              onPress={onClose}
              disabled={loading}
              accessibilityRole="button">
              <Text style={styles.secondaryButtonText}>Cancel</Text>
            </Pressable>
            <Pressable
              style={[styles.primaryButton, loading ? styles.primaryDisabled : null]}
              onPress={handleSubmit}
              disabled={loading || !/^\d{6}$/.test(code.trim())}
              accessibilityRole="button">
              <Text style={styles.primaryButtonText}>
                {loading ? 'Verifying…' : 'Verify'}
              </Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: 'rgba(16, 24, 40, 0.4)',
  },
  sheet: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    padding: 20,
    paddingBottom: 28,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 14,
    color: '#475467',
    marginBottom: 16,
  },
  input: {
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 8,
    padding: 12,
    fontSize: 18,
    letterSpacing: 4,
    textAlign: 'center',
    marginBottom: 12,
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
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
