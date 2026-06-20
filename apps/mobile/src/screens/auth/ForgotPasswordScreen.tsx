import React, {useState} from 'react';
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

type Props = NativeStackScreenProps<AuthStackParamList, 'ForgotPassword'>;

export function ForgotPasswordScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();
  const [email, setEmail] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const submit = async (): Promise<void> => {
    setErrorMessage(null);
    setSuccessMessage(null);

    if (!email.trim()) {
      setErrorMessage('Enter a valid email address.');
      return;
    }

    setSubmitting(true);
    try {
      const result = await auth.forgotPassword(email.trim());
      setSuccessMessage(result.message ?? null);
    } catch (error) {
      setErrorMessage(auth.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Reset password</Text>
      <Text style={styles.subtitle}>
        Enter the email for your account. We'll send reset instructions if it
        exists.
      </Text>

      {!successMessage ? (
        <>
          <Text style={styles.label}>Email</Text>
          <TextInput
            style={styles.input}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            keyboardType="email-address"
            accessibilityLabel="Email"
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
              <Text style={styles.buttonText}>Send reset instructions</Text>
            )}
          </Pressable>
        </>
      ) : (
        <>
          <Text style={styles.successText} accessibilityLiveRegion="polite">
            {successMessage}
          </Text>
          <Pressable
            style={styles.linkButton}
            onPress={() => navigation.navigate('Login')}
            accessibilityRole="button">
            <Text style={styles.linkText}>Back to sign in</Text>
          </Pressable>
        </>
      )}
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
    marginBottom: 24,
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
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  linkText: {
    color: '#0D6E6E',
    fontWeight: '600',
    fontSize: 16,
  },
  successText: {
    color: '#047857',
    fontSize: 14,
    marginBottom: 12,
  },
});
