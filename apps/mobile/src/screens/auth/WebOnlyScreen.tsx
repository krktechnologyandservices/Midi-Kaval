import React from 'react';
import {View, Text, Pressable, StyleSheet} from 'react-native';
import {useAuth} from '../../context/AuthContext';

export function WebOnlyScreen(): React.JSX.Element {
  const auth = useAuth();

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Web app required</Text>
      <Text style={styles.body}>Use the web app for your role.</Text>
      {auth.user ? (
        <Text style={styles.meta}>
          Signed in as {auth.user.email} ({auth.user.role})
        </Text>
      ) : null}
      <Pressable
        style={styles.button}
        onPress={() => void auth.logout()}
        accessibilityRole="button">
        <Text style={styles.buttonText}>Log out</Text>
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
    alignItems: 'center',
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
    color: '#101828',
    marginBottom: 12,
  },
  body: {
    fontSize: 16,
    color: '#475467',
    textAlign: 'center',
    marginBottom: 16,
  },
  meta: {
    fontSize: 14,
    color: '#475467',
    textAlign: 'center',
    marginBottom: 24,
  },
  button: {
    borderWidth: 1,
    borderColor: '#0D6E6E',
    borderRadius: 8,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
  },
  buttonText: {
    color: '#0D6E6E',
    fontWeight: '600',
  },
});
