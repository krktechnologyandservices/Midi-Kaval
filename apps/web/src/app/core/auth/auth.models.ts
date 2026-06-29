import type { components } from '@midi-kaval/api-client';

export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];
export type VerifyOtpRequest = components['schemas']['VerifyOtpRequest'];
export type VerifyOtpResponse = components['schemas']['VerifyOtpResponse'];
export type RefreshResponse = components['schemas']['RefreshResponse'];
export type SessionUserDto = components['schemas']['SessionUserDto'];
export type AuthUserDto = components['schemas']['AuthUserDto'];
export type ForgotPasswordRequest = components['schemas']['ForgotPasswordRequest'];
export type ForgotPasswordResponse = components['schemas']['ForgotPasswordResponse'];
export type ResetPasswordRequest = components['schemas']['ResetPasswordRequest'];
export type ResetPasswordResponse = components['schemas']['ResetPasswordResponse'];

export interface ApiEnvelope<T> {
  data: T;
  meta: { requestId: string };
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
}

export const AUTH_HTTP_OPTIONS = { withCredentials: true } as const;

export const ACCESS_TOKEN_KEY = 'midi_kaval_access_token';
export const USER_KEY = 'midi_kaval_user';
export const CHALLENGE_KEY = 'midi_kaval_otp_challenge';
export const TOTP_KEY = 'midi_kaval_totp_state';
