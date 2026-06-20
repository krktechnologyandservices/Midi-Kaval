import React from 'react';
import {View, Text, StyleSheet} from 'react-native';

interface AccessibleErrorRegionProps {
  message: string | null;
}

export function AccessibleErrorRegion({
  message,
}: AccessibleErrorRegionProps): React.JSX.Element | null {
  if (!message) {
    return <View style={styles.spacer} accessibilityElementsHidden />;
  }

  return (
    <View
      style={styles.container}
      accessibilityLiveRegion="polite"
      accessibilityRole="alert">
      <Text style={styles.text}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    minHeight: 20,
    marginBottom: 12,
  },
  spacer: {
    minHeight: 20,
    marginBottom: 12,
  },
  text: {
    color: '#B42318',
    fontSize: 14,
  },
});
