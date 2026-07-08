import {Alert, Linking} from 'react-native';

// Uses Android's built-in geo: URI instead of a Google Maps-specific link, so the OS
// offers whatever maps app is actually installed (Google Maps, OsmAnd, etc.) — no API
// key, no dependency on one specific provider being installed.
export function buildGeoUri(options: {
  latitude?: number | null;
  longitude?: number | null;
  address: string;
}): string {
  if (options.latitude != null && options.longitude != null) {
    return `geo:${options.latitude},${options.longitude}?q=${options.latitude},${options.longitude}(${encodeURIComponent(
      options.address,
    )})`;
  }

  // No precise coordinates were captured when this place was added — fall back to a
  // text query so the maps app geocodes the address itself.
  return `geo:0,0?q=${encodeURIComponent(options.address)}`;
}

export async function openPlaceInMaps(options: {
  latitude?: number | null;
  longitude?: number | null;
  address: string;
}): Promise<boolean> {
  const uri = buildGeoUri(options);

  try {
    await Linking.openURL(uri);
    return true;
  } catch (error) {
    Alert.alert(
      'Could not open maps',
      error instanceof Error ? error.message : 'Install a maps app and try again.',
    );
    return false;
  }
}
