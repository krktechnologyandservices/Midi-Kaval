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

type Props = NativeStackScreenProps<AuthStackParamList, 'Login'>;

export function LoginScreen({navigation, route}: Props): React.JSX.Element {
  const auth = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (route.params?.resetSuccess) {
      setSuccessMessage(route.params.resetSuccess);
    }
  }, [route.params?.resetSuccess]);

  const submit = async (): Promise<void> => {
    setErrorMessage(null);
    if (!email.trim() || password.length < 8) {
      setErrorMessage('Enter a valid email and password (min 8 characters).');
      return;
    }

    setSubmitting(true);
    try {
      await auth.login({email: email.trim(), password});
      navigation.navigate('Otp');
    } catch (error) {
      setErrorMessage(auth.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Sign in</Text>
      <Text style={styles.subtitle}>Field access — Kaval Online</Text>

      <Text style={styles.label}>Email</Text>
      <TextInput
        style={styles.input}
        value={email}
        onChangeText={setEmail}
        autoCapitalize="none"
        keyboardType="email-address"
        accessibilityLabel="Email"
      />

      <Text style={styles.label}>Password</Text>
      <TextInput
        style={styles.input}
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        accessibilityLabel="Password"
      />

      <AccessibleErrorRegion message={errorMessage} />
      {successMessage ? (
        <Text style={styles.successText} accessibilityLiveRegion="polite">
          {successMessage}
        </Text>
      ) : null}

      <Pressable
        style={styles.button}
        onPress={submit}
        disabled={submitting}
        accessibilityRole="button">
        {submitting ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <Text style={styles.buttonText}>Continue</Text>
        )}
      </Pressable>

      <Pressable
        style={styles.linkButton}
        onPress={() => navigation.navigate('ForgotPassword')}
        accessibilityRole="button">
        <Text style={styles.linkText}>Forgot password?</Text>
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
  successText: {
    color: '#047857',
    marginBottom: 12,
    fontSize: 14,
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
});
