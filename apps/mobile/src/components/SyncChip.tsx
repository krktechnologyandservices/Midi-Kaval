import React from 'react';
import {Pressable, StyleSheet, Text, View, ViewStyle} from 'react-native';
import {SyncChipPresentation} from '../services/sync/resolveVisitSyncChip';

type Props = {
  chip: SyncChipPresentation;
  onPress?: () => void;
  style?: ViewStyle;
};

export function SyncChip({chip, onPress, style}: Props): React.JSX.Element {
  const chipText = (
    <Text
      style={[
        styles.chip,
        {backgroundColor: chip.backgroundColor, color: chip.color},
      ]}>
      {chip.label}
    </Text>
  );

  if (chip.state === 'error' && onPress) {
    return (
      <Pressable
        style={style}
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={chip.label}
        accessibilityHint="Opens sync queue">
        {chipText}
      </Pressable>
    );
  }

  return (
    <View style={style} accessibilityLabel={chip.label}>
      {chipText}
    </View>
  );
}

const styles = StyleSheet.create({
  chip: {
    fontSize: 11,
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: 6,
    overflow: 'hidden',
  },
});
