import React from 'react';
import {View, Text, Pressable, StyleSheet} from 'react-native';
import {NativeStackScreenProps} from '@react-navigation/native-stack';
import {useAuth} from '../../context/AuthContext';
import {AuthStackParamList} from '../../navigation/types';

type Props = NativeStackScreenProps<AuthStackParamList, 'SessionExpired'>;

export function SessionExpiredScreen({navigation}: Props): React.JSX.Element {
  const auth = useAuth();

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Session expired</Text>
      <Text style={styles.subtitle}>
        Your session has ended for security.
      </Text>
      <Text style={styles.body}>Sign in again to continue using field tools.</Text>
      <Pressable
        style={styles.button}
        onPress={() => {
          auth.clearSessionExpired();
          navigation.replace('Login');
        }}
        accessibilityRole="button">
        <Text style={styles.buttonText}>Sign in again</Text>
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
    marginBottom: 12,
  },
  body: {
    fontSize: 16,
    color: '#344054',
    marginBottom: 24,
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
});
