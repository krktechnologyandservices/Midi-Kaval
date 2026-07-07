import React, {useEffect, useState} from 'react';
import {
  View,
  Text,
  TextInput,
  Pressable,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import {NativeStackScreenProps} from '@react-navigation/native-stack';
import {AccessibleErrorRegion} from '../../components/AccessibleErrorRegion';
import {useAuth} from '../../context/AuthContext';
import {AuthStackParamList} from '../../navigation/types';

type Props = NativeStackScreenProps<AuthStackParamList, 'Totp'>;

export function TotpLoginScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();
  const [code, setCode] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!auth.totpChallenge) {
      navigation.replace('Login');
    }
  }, [auth.totpChallenge, navigation]);

  const submit = async (): Promise<void> => {
    setErrorMessage(null);

    if (!/^\d{6}$/.test(code)) {
      setErrorMessage('Enter a 6-digit code.');
      return;
    }

    setSubmitting(true);
    try {
      await auth.verifyTotpLogin(code);
    } catch (error) {
      setErrorMessage(auth.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Two-factor authentication</Text>
      <Text style={styles.subtitle}>
        Enter the 6-digit code from your authenticator app.
      </Text>

      <Text style={styles.label}>Authentication code</Text>
      <TextInput
        style={styles.input}
        value={code}
        onChangeText={setCode}
        keyboardType="number-pad"
        maxLength={6}
        accessibilityLabel="Authentication code"
      />

      <AccessibleErrorRegion message={errorMessage} />

      <Pressable
        style={styles.button}
        onPress={submit}
        disabled={submitting}
        accessibilityRole="button">
        {submitting ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <Text style={styles.buttonText}>Verify</Text>
        )}
      </Pressable>

      <Pressable
        style={styles.linkButton}
        onPress={async () => {
          await auth.logout();
          navigation.replace('Login');
        }}
        accessibilityRole="button">
        <Text style={styles.linkText}>Lost access to your authenticator app?</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 24,
    backgroundColor: '#F8FAFC',
    justifyContent: 'center',
  },
  title: {
    fontSize: 24,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 14,
    color: '#475467',
    marginBottom: 16,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 6,
    color: '#344054',
  },
  input: {
    borderWidth: 1,
    borderColor: '#D0D5DD',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 12,
    marginBottom: 16,
    backgroundColor: '#FFFFFF',
    minHeight: 44,
  },
  button: {
    backgroundColor: '#0D6E6E',
    borderRadius: 8,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  buttonText: {
    color: '#FFFFFF',
    fontWeight: '600',
    fontSize: 16,
  },
  linkButton: {
    marginTop: 16,
    alignItems: 'center',
    minHeight: 44,
    justifyContent: 'center',
  },
  linkText: {
    color: '#0D6E6E',
    fontWeight: '600',
  },
});
