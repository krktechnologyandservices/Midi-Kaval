import {Alert, Linking} from 'react-native';

export function buildGoogleMapsUrl(latitude: number, longitude: number): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}`;
}

export async function openGoogleMaps(
  latitude: number,
  longitude: number,
): Promise<boolean> {
  const url = buildGoogleMapsUrl(latitude, longitude);

  // Deliberately not gating on Linking.canOpenURL() first: on Android it's known to
  // return a false negative for https:// links even when a capable app is installed
  // and the manifest <queries> declaration is correct (an OS/OEM package-visibility
  // quirk, not something this app controls) — a standard https URL is always
  // handleable by *something* on a real device, so treat an actual thrown error from
  // openURL as the only real failure signal instead of pre-emptively blocking on a
  // check that's proven unreliable.
  try {
    await Linking.openURL(url);
    return true;
  } catch (error) {
    Alert.alert(
      'Could not open Google Maps',
      error instanceof Error ? error.message : 'Install the app or try again.',
    );
    return false;
  }
}
