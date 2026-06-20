import React, {useEffect, useRef, useState} from 'react';
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

type Props = NativeStackScreenProps<AuthStackParamList, 'Otp'>;

export function OtpScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();
  const [code, setCode] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [expiredMessage, setExpiredMessage] = useState<string | null>(null);
  const [secondsRemaining, setSecondsRemaining] = useState(0);
  const [submitting, setSubmitting] = useState(false);

  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    const challenge = auth.otpChallenge;
    if (!challenge) {
      navigation.replace('Login');
      return;
    }

    setExpiredMessage(null);
    setSecondsRemaining(challenge.expiresInSeconds);

    timerRef.current = setInterval(() => {
      setSecondsRemaining(prev => {
        if (prev <= 1) {
          if (timerRef.current) {
            clearInterval(timerRef.current);
            timerRef.current = null;
          }
          setExpiredMessage('Code expired — request new code');
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [auth.otpChallenge, navigation]);

  const submit = async (): Promise<void> => {
    setErrorMessage(null);
    if (expiredMessage) {
      return;
    }

    if (!/^\d{6}$/.test(code)) {
      setErrorMessage('Enter a 6-digit code.');
      return;
    }

    setSubmitting(true);
    try {
      await auth.verifyOtp(code);
    } catch (error) {
      setErrorMessage(auth.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Verification code</Text>
      <Text style={styles.subtitle}>
        Enter the 6-digit code from your email.
      </Text>

      {secondsRemaining > 0 ? (
        <Text style={styles.hint}>
          Code expires in {secondsRemaining} seconds.
        </Text>
      ) : null}

      <Text style={styles.label}>6-digit code</Text>
      <TextInput
        style={styles.input}
        value={code}
        onChangeText={setCode}
        keyboardType="number-pad"
        maxLength={6}
        accessibilityLabel="6-digit code"
      />

      <AccessibleErrorRegion message={errorMessage ?? expiredMessage} />

      <Pressable
        style={styles.button}
        onPress={submit}
        disabled={submitting || !!expiredMessage}
        accessibilityRole="button">
        {submitting ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <Text style={styles.buttonText}>Verify</Text>
        )}
      </Pressable>

      {expiredMessage ? (
        <Pressable
          style={styles.linkButton}
          onPress={() => navigation.replace('Login')}
          accessibilityRole="button">
          <Text style={styles.linkText}>Request new code</Text>
        </Pressable>
      ) : null}
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
  hint: {
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
