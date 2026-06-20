import React from 'react';
import {StyleSheet, Text, View} from 'react-native';

type Props = {
  label: string | null | undefined;
};

export function CourtCountdownBanner({label}: Props): React.JSX.Element | null {
  if (!label) {
    return null;
  }

  return (
    <View style={styles.banner} accessibilityRole="text">
      <Text style={styles.text}>⚠ {label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    marginBottom: 12,
    paddingVertical: 10,
    paddingHorizontal: 12,
    backgroundColor: '#FFFAEB',
    borderLeftWidth: 4,
    borderLeftColor: '#B54708',
    borderRadius: 8,
  },
  text: {
    fontSize: 13,
    color: '#101828',
  },
});
