import {Alert, Linking} from 'react-native';
import {buildGoogleMapsUrl, openGoogleMaps} from '../src/services/visits/visitNavigation';

describe('visitNavigation', () => {
  let alertSpy: jest.SpyInstance;

  beforeEach(() => {
    jest.clearAllMocks();
    alertSpy = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
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

  test('openGoogleMaps opens the URL directly without a canOpenURL pre-check', async () => {
    const opened = await openGoogleMaps(12.9716, 77.5946);

    expect(opened).toBe(true);
    expect(Linking.canOpenURL).not.toHaveBeenCalled();
    expect(Linking.openURL).toHaveBeenCalledWith(
      'https://www.google.com/maps/dir/?api=1&destination=12.9716,77.5946',
    );
    expect(alertSpy).not.toHaveBeenCalled();
  });

  test('openGoogleMaps shows alert with the underlying error when openURL throws', async () => {
    (Linking.openURL as jest.Mock).mockRejectedValue(new Error('No activity found'));

    const opened = await openGoogleMaps(12.9716, 77.5946);

    expect(opened).toBe(false);
    expect(alertSpy).toHaveBeenCalledWith('Could not open Google Maps', 'No activity found');
  });
});
