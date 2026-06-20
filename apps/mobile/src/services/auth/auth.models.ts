import type {components} from '@midi-kaval/api-client';

export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];
export type VerifyOtpRequest = components['schemas']['VerifyOtpRequest'];
export type VerifyOtpResponse = components['schemas']['VerifyOtpResponse'];
export type RefreshRequest = components['schemas']['RefreshRequest'];
export type RefreshResponse = components['schemas']['RefreshResponse'];
export type SessionUserDto = components['schemas']['SessionUserDto'];
export type ForgotPasswordRequest = components['schemas']['ForgotPasswordRequest'];
export type ForgotPasswordResponse = components['schemas']['ForgotPasswordResponse'];
export type ResetPasswordRequest = components['schemas']['ResetPasswordRequest'];
export type ResetPasswordResponse = components['schemas']['ResetPasswordResponse'];

export interface ApiEnvelope<T> {
  data: T;
  meta: {requestId: string};
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
}

export const CHALLENGE_KEY = 'midi_kaval_otp_challenge';

export interface OtpChallengeState {
  challengeId: string;
  expiresInSeconds: number;
}

export interface StepUpResponse {
  challengeId: string;
  expiresInSeconds?: number;
}

export interface VerifyStepUpRequest {
  challengeId: string;
  code: string;
}
