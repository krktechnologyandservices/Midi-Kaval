import React from 'react';
import MaterialCommunityIcons from 'react-native-vector-icons/MaterialCommunityIcons';

// Single choke point for icon rendering so the whole app draws from one font family
// (MaterialCommunityIcons — closest visual match to the web app's Material Symbols icons).
// Route all icon usage through this component rather than importing icon-family modules
// directly in screens, so a future font/family change only happens in one place.
export function Icon({
  name,
  size = 22,
  color = '#101828',
}: {
  name: string;
  size?: number;
  color?: string;
}): React.JSX.Element {
  return <MaterialCommunityIcons name={name} size={size} color={color} />;
}
