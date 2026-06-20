import type {components} from '@midi-kaval/api-client';

export type TravelClaimDto = components['schemas']['TravelClaimDto'];
export type TravelClaimListResultDto = components['schemas']['TravelClaimListResultDto'];
export type CreateTravelClaimRequest = components['schemas']['CreateTravelClaimRequest'];
export type UpdateTravelClaimRequest = components['schemas']['UpdateTravelClaimRequest'];

export const TRANSPORT_MODES = ['Bus', 'Auto', 'Petrol', 'Other'] as const;
export type TransportMode = (typeof TRANSPORT_MODES)[number];

export const RECEIPT_REQUIRED_MODES: TransportMode[] = ['Bus', 'Auto', 'Petrol'];

export function requiresReceipt(mode: string | null | undefined): boolean {
  return RECEIPT_REQUIRED_MODES.includes((mode ?? '') as TransportMode);
}

export const RECEIPT_REQUIRED_MESSAGE =
  'Receipt image is required for Bus, Auto, and Petrol claims before submit.';
