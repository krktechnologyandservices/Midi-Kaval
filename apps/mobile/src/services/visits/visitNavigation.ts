import {Alert, Linking} from 'react-native';

export function buildGoogleMapsUrl(latitude: number, longitude: number): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}`;
}

export async function openGoogleMaps(
  latitude: number,
  longitude: number,
): Promise<boolean> {
  const url = buildGoogleMapsUrl(latitude, longitude);

  try {
    const canOpen = await Linking.canOpenURL(url);
    if (!canOpen) {
      Alert.alert('Could not open Google Maps — install the app or try again');
      return false;
    }

    await Linking.openURL(url);
    return true;
  } catch {
    Alert.alert('Could not open Google Maps — install the app or try again');
    return false;
  }
}
