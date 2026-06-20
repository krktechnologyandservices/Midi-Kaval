import React from 'react';
import {StyleSheet, View} from 'react-native';

export function CommandStripSkeleton(): React.JSX.Element {
  return (
    <View>
      {[0, 1, 2].map(index => (
        <View key={index} style={styles.card} accessibilityLabel="Loading visit" />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    height: 140,
    marginBottom: 12,
    backgroundColor: '#EAECF0',
    borderRadius: 12,
    opacity: 0.6,
  },
});
