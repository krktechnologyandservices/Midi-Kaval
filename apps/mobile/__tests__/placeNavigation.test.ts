import {Alert, Linking} from 'react-native';
import {buildGeoUri, openPlaceInMaps} from '../src/services/visits/placeNavigation';

describe('placeNavigation', () => {
  let alertSpy: jest.SpyInstance;

  beforeEach(() => {
    jest.clearAllMocks();
    alertSpy = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    (Linking.openURL as jest.Mock).mockResolvedValue(undefined);
  });

  afterEach(() => {
    alertSpy.mockRestore();
  });

  test('buildGeoUri uses precise coordinates when available', () => {
    expect(buildGeoUri({latitude: 12.9716, longitude: 77.5946, address: "Ma's house"})).toBe(
      "geo:12.9716,77.5946?q=12.9716,77.5946(Ma's%20house)",
    );
  });

  test('buildGeoUri falls back to a text query when no coordinates exist', () => {
    expect(buildGeoUri({address: '221B Baker Street'})).toBe(
      'geo:0,0?q=221B%20Baker%20Street',
    );
  });

  test('openPlaceInMaps opens the geo URI directly', async () => {
    const opened = await openPlaceInMaps({
      latitude: 12.9716,
      longitude: 77.5946,
      address: 'School',
    });

    expect(opened).toBe(true);
    expect(Linking.openURL).toHaveBeenCalledWith(
      'geo:12.9716,77.5946?q=12.9716,77.5946(School)',
    );
    expect(alertSpy).not.toHaveBeenCalled();
  });

  test('openPlaceInMaps alerts with the underlying error when it fails', async () => {
    (Linking.openURL as jest.Mock).mockRejectedValue(new Error('No activity found'));

    const opened = await openPlaceInMaps({address: 'School'});

    expect(opened).toBe(false);
    expect(alertSpy).toHaveBeenCalledWith('Could not open maps', 'No activity found');
  });
});
