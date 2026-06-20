import {distanceKm, formatDistanceKm, roundKmOneDecimal} from '../src/services/visits/visitGeo';

test('distanceKm returns approximately one kilometer for pilot coordinates', () => {
  const distance = distanceKm(12.9716, 77.5946, 12.98, 77.5946);
  expect(distance).toBeGreaterThan(0.8);
  expect(distance).toBeLessThan(1.2);
});

test('formatDistanceKm renders one decimal kilometer label', () => {
  expect(formatDistanceKm(1.24)).toBe('1.2 km');
  expect(formatDistanceKm(null)).toBeNull();
});

test('roundKmOneDecimal rounds away from zero', () => {
  expect(roundKmOneDecimal(1.24)).toBe(1.2);
  expect(roundKmOneDecimal(1.25)).toBe(1.3);
});
