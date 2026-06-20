import React from 'react';
import {View, Text, StyleSheet} from 'react-native';

export function CasesScreen(): React.JSX.Element {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Cases</Text>
      <Text style={styles.subtitle}>Case registry in Epic 2</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 24,
    backgroundColor: '#F8FAFC',
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
    color: '#101828',
  },
  subtitle: {
    fontSize: 14,
    color: '#475467',
    marginTop: 4,
  },
});
