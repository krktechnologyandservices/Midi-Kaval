import React, {useMemo, useState} from 'react';
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

type Props = NativeStackScreenProps<AuthStackParamList, 'ResetPassword'>;

export function ResetPasswordScreen({navigation, route}: Props): React.JSX.Element {
  const auth = useAuth();
  const initialToken = useMemo(
    () => route.params?.token ?? '',
    [route.params?.token],
  );
  const [token, setToken] = useState(initialToken);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(
    initialToken ? null : 'Invalid or expired reset token.',
  );
  const [submitting, setSubmitting] = useState(false);

  const submit = async (): Promise<void> => {
    setErrorMessage(null);

    if (!token.trim()) {
      setErrorMessage('Invalid or expired reset token.');
      return;
    }

    if (newPassword.length < 8) {
      setErrorMessage('Password must be at least 8 characters.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setErrorMessage('Passwords do not match.');
      return;
    }

    setSubmitting(true);
    try {
      const result = await auth.resetPassword(token.trim(), newPassword);
      navigation.navigate('Login', {resetSuccess: result.message});
    } catch (error) {
      setErrorMessage(auth.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Choose a new password</Text>
      <Text style={styles.subtitle}>
        Choose a new password (at least 8 characters).
      </Text>

      {!initialToken ? (
        <>
          <Text style={styles.label}>Reset token (dev)</Text>
          <TextInput
            style={styles.input}
            value={token}
            onChangeText={setToken}
            autoCapitalize="none"
            accessibilityLabel="Reset token"
          />
        </>
      ) : null}

      <Text style={styles.label}>New password</Text>
      <TextInput
        style={styles.input}
        value={newPassword}
        onChangeText={setNewPassword}
        secureTextEntry
        accessibilityLabel="New password"
      />

      <Text style={styles.label}>Confirm new password</Text>
      <TextInput
        style={styles.input}
        value={confirmPassword}
        onChangeText={setConfirmPassword}
        secureTextEntry
        accessibilityLabel="Confirm new password"
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
          <Text style={styles.buttonText}>Update password</Text>
        )}
      </Pressable>

      <Pressable
        style={styles.linkButton}
        onPress={() => navigation.navigate('Login')}
        accessibilityRole="button">
        <Text style={styles.linkText}>Back to sign in</Text>
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
