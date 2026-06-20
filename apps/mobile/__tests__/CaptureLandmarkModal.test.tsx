import React from 'react';
import {Linking, Pressable, Text, TextInput} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {CaptureLandmarkModal} from '../src/components/CaptureLandmarkModal';
import {caseApiService} from '../src/services/cases/CaseApiService';

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    verifyCaseGps: jest.fn(),
    extractErrorMessage: jest.fn(() => 'verify failed'),
  },
}));

beforeEach(() => {
  jest.clearAllMocks();
});

test('capture landmark requires text before save', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaptureLandmarkModal
        visible
        caseId="22222222-2222-4222-8222-222222222222"
        onClose={jest.fn()}
        onSuccess={jest.fn()}
      />,
    );
  });

  const saveButton = tree.root.findByProps({accessibilityLabel: 'Save and navigate'});
  expect(saveButton.props.disabled).toBe(true);
});

test('save and navigate verifies gps then calls onSuccess', async () => {
  const onSuccess = jest.fn();
  (caseApiService.verifyCaseGps as jest.Mock).mockResolvedValue({
    caseId: '22222222-2222-4222-8222-222222222222',
    gpsVerified: true,
    latitude: 12.9716,
    longitude: 77.5946,
    landmark: 'Near community hall',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(
      <CaptureLandmarkModal
        visible
        caseId="22222222-2222-4222-8222-222222222222"
        onClose={jest.fn()}
        onSuccess={onSuccess}
      />,
    );
    await Promise.resolve();
  });

  const landmarkInput = tree.root.findByProps({accessibilityLabel: 'Landmark description'});
  ReactTestRenderer.act(() => {
    landmarkInput.props.onChangeText('Near community hall');
  });

  const saveButton = tree.root.findByProps({accessibilityLabel: 'Save and navigate'});
  await ReactTestRenderer.act(async () => {
    saveButton.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.verifyCaseGps).toHaveBeenCalledWith(
    '22222222-2222-4222-8222-222222222222',
    expect.objectContaining({
      latitude: 12.9716,
      longitude: 77.5946,
      landmark: 'Near community hall',
    }),
  );
  expect(onSuccess).toHaveBeenCalled();
  expect(Linking.openURL).not.toHaveBeenCalled();
});
