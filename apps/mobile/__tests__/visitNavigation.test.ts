import {Alert, Linking} from 'react-native';
import {buildGoogleMapsUrl, openGoogleMaps} from '../src/services/visits/visitNavigation';

describe('visitNavigation', () => {
  let alertSpy: jest.SpyInstance;

  beforeEach(() => {
    jest.clearAllMocks();
    alertSpy = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    (Linking.canOpenURL as jest.Mock).mockResolvedValue(true);
    (Linking.openURL as jest.Mock).mockResolvedValue(undefined);
  });

  afterEach(() => {
    alertSpy.mockRestore();
  });

  test('buildGoogleMapsUrl formats destination coordinates', () => {
    expect(buildGoogleMapsUrl(12.9716, 77.5946)).toBe(
      'https://www.google.com/maps/dir/?api=1&destination=12.9716,77.5946',
    );
  });

  test('openGoogleMaps opens URL when canOpenURL succeeds', async () => {
    const opened = await openGoogleMaps(12.9716, 77.5946);

    expect(opened).toBe(true);
    expect(Linking.canOpenURL).toHaveBeenCalledWith(
      'https://www.google.com/maps/dir/?api=1&destination=12.9716,77.5946',
    );
    expect(Linking.openURL).toHaveBeenCalledWith(
      'https://www.google.com/maps/dir/?api=1&destination=12.9716,77.5946',
    );
    expect(alertSpy).not.toHaveBeenCalled();
  });

  test('openGoogleMaps shows alert when canOpenURL fails', async () => {
    (Linking.canOpenURL as jest.Mock).mockResolvedValue(false);

    const opened = await openGoogleMaps(12.9716, 77.5946);

    expect(opened).toBe(false);
    expect(alertSpy).toHaveBeenCalledWith(
      'Could not open Google Maps — install the app or try again',
    );
    expect(Linking.openURL).not.toHaveBeenCalled();
  });
});
