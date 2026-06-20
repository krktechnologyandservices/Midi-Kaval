import AsyncStorage from '@react-native-async-storage/async-storage';
import {environment} from '../../config/environment';
import {
  ApiEnvelope,
  CHALLENGE_KEY,
  LoginRequest,
  LoginResponse,
  OtpChallengeState,
  ProblemDetails,
  RefreshResponse,
  SessionUserDto,
  VerifyOtpRequest,
  VerifyOtpResponse,
  StepUpResponse,
  VerifyStepUpRequest,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  ResetPasswordRequest,
  ResetPasswordResponse,
} from './auth.models';
import {shouldAttachBearer} from '../api/apiClient';
import {clearCache as clearCommandStripCache} from '../visits/commandStripCache';
import * as secureStorage from './secureStorage';

const DEACTIVATED_MESSAGE = 'Contact your coordinator';

export class AuthSessionService {
  private challenge: OtpChallengeState | null = null;
  private stepUpChallenge: OtpChallengeState | null = null;
  private lastLoginOtpVerifiedAtUtc: string | null = null;
  private user: SessionUserDto | null = null;
  private refreshInFlight: Promise<boolean> | null = null;
  onSessionExpired: (() => void) | null = null;
  onDeactivated: (() => void) | null = null;

  getOtpChallenge(): OtpChallengeState | null {
    return this.challenge;
  }

  getUser(): SessionUserDto | null {
    return this.user;
  }

  getLastLoginOtpVerifiedAtUtc(): string | null {
    return this.lastLoginOtpVerifiedAtUtc;
  }

  async isAuthenticated(): Promise<boolean> {
    const token = await secureStorage.getAccessToken();
    return !!token;
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    const envelope = await this.post<LoginResponse>('/api/v1/auth/login', request);
    this.challenge = {
      challengeId: envelope.data.challengeId!,
      expiresInSeconds: envelope.data.expiresInSeconds ?? 300,
    };
    await AsyncStorage.setItem(CHALLENGE_KEY, JSON.stringify(this.challenge));
    return envelope.data;
  }

  async verifyOtp(code: string): Promise<VerifyOtpResponse> {
    if (!this.challenge) {
      const stored = await AsyncStorage.getItem(CHALLENGE_KEY);
      if (stored) {
        try {
          this.challenge = JSON.parse(stored) as OtpChallengeState;
        } catch {
          await AsyncStorage.removeItem(CHALLENGE_KEY);
        }
      }
    }

    if (!this.challenge) {
      throw new Error('No OTP challenge in progress.');
    }

    const body: VerifyOtpRequest = {
      challengeId: this.challenge.challengeId,
      code,
    };

    const envelope = await this.post<VerifyOtpResponse>(
      '/api/v1/auth/verify-otp',
      body,
    );

    await this.applySession(
      envelope.data.accessToken!,
      envelope.data.refreshToken!,
      {
        id: envelope.data.user!.id,
        email: envelope.data.user!.email,
        role: envelope.data.user!.role,
      },
    );

    this.challenge = null;
    await AsyncStorage.removeItem(CHALLENGE_KEY);
    this.lastLoginOtpVerifiedAtUtc = new Date().toISOString();
    return envelope.data;
  }

  async stepUp(): Promise<StepUpResponse> {
    this.challenge = null;
    await AsyncStorage.removeItem(CHALLENGE_KEY);
    const envelope = await this.postApi<StepUpResponse>('/api/v1/auth/step-up');
    this.stepUpChallenge = {
      challengeId: envelope.data.challengeId,
      expiresInSeconds: envelope.data.expiresInSeconds ?? 300,
    };
    return envelope.data;
  }

  async verifyStepUp(code: string): Promise<void> {
    if (!this.stepUpChallenge) {
      throw new Error('No step-up challenge in progress.');
    }

    const body: VerifyStepUpRequest = {
      challengeId: this.stepUpChallenge.challengeId,
      code,
    };

    await this.postApi<{verified: boolean}>(
      '/api/v1/auth/verify-step-up',
      body,
    );
    this.stepUpChallenge = null;
    this.lastLoginOtpVerifiedAtUtc = new Date().toISOString();
  }

  async forgotPassword(email: string): Promise<ForgotPasswordResponse> {
    const body: ForgotPasswordRequest = {email};
    const envelope = await this.post<ForgotPasswordResponse>(
      '/api/v1/auth/forgot-password',
      body,
      false,
    );
    return envelope.data;
  }

  async resetPassword(
    token: string,
    newPassword: string,
  ): Promise<ResetPasswordResponse> {
    const body: ResetPasswordRequest = {token, newPassword};
    const envelope = await this.post<ResetPasswordResponse>(
      '/api/v1/auth/reset-password',
      body,
      false,
    );
    return envelope.data;
  }

  async refreshSession(): Promise<boolean> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    this.refreshInFlight = this.performRefresh();
    try {
      return await this.refreshInFlight;
    } finally {
      this.refreshInFlight = null;
    }
  }

  async logout(): Promise<void> {
    const refreshToken = await secureStorage.getRefreshToken();
    const deviceInstallId = await secureStorage.getDeviceInstallId();
    if (refreshToken) {
      try {
        await fetch(`${environment.apiBaseUrl}/api/v1/auth/logout`, {
          method: 'POST',
          headers: {'Content-Type': 'application/json'},
          body: JSON.stringify({
            refreshToken,
            deviceInstallId: deviceInstallId ?? undefined,
          }),
        });
      } catch {
        // Clear local session even if network fails.
      }
    }

    await this.clearSession();
  }

  async bootstrapSession(): Promise<SessionUserDto | null> {
    const storedChallenge = await AsyncStorage.getItem(CHALLENGE_KEY);
    if (storedChallenge) {
      try {
        this.challenge = JSON.parse(storedChallenge) as OtpChallengeState;
      } catch {
        await AsyncStorage.removeItem(CHALLENGE_KEY);
      }
    }

    const token = await secureStorage.getAccessToken();
    if (!token) {
      return null;
    }

    return this.loadCurrentUser();
  }

  async loadCurrentUser(): Promise<SessionUserDto | null> {
    const token = await secureStorage.getAccessToken();
    if (!token) {
      return null;
    }

    try {
      const envelope = await this.get<SessionUserDto>('/api/v1/auth/me');
      this.user = envelope.data;
      return envelope.data;
    } catch {
      await this.clearSession();
      return null;
    }
  }

  async clearSession(): Promise<void> {
    this.user = null;
    this.challenge = null;
    this.stepUpChallenge = null;
    this.lastLoginOtpVerifiedAtUtc = null;
    await secureStorage.clearTokens();
    await AsyncStorage.removeItem(CHALLENGE_KEY);
    await clearCommandStripCache();
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof ApiClientError) {
      if (error.problem?.detail) {
        return error.problem.detail;
      }
      if (error.status === 401) {
        return 'Invalid email or password.';
      }
      if (error.status === 429) {
        return 'Too many attempts. Please try again later.';
      }
    }

    return 'Something went wrong. Please try again.';
  }

  private async applySession(
    accessToken: string,
    refreshToken: string,
    user: SessionUserDto,
  ): Promise<void> {
    await secureStorage.saveAccessToken(accessToken);
    await secureStorage.saveRefreshToken(refreshToken);
    this.user = user;
  }

  private async performRefresh(): Promise<boolean> {
    const refreshToken = await secureStorage.getRefreshToken();
    if (!refreshToken) {
      return false;
    }

    try {
      const envelope = await this.post<RefreshResponse>(
        '/api/v1/auth/refresh',
        {refreshToken},
        false,
      );

      const accessToken = envelope.data.accessToken;
      const rotatedRefresh = envelope.data.refreshToken;
      if (!accessToken || !rotatedRefresh) {
        return false;
      }

      await secureStorage.saveAccessToken(accessToken);
      await secureStorage.saveRefreshToken(rotatedRefresh);
      await this.loadCurrentUser();
      return true;
    } catch {
      return false;
    }
  }

  private async get<T>(path: string, retried = false): Promise<ApiEnvelope<T>> {
    return this.request<T>('GET', path, undefined, retried);
  }

  private async post<T>(
    path: string,
    body: unknown,
    allowRefresh = true,
  ): Promise<ApiEnvelope<T>> {
    return this.request<T>('POST', path, body, false, allowRefresh);
  }

  /** Authenticated JSON POST for domain API calls (cases, visits, etc.). */
  postApi<T>(path: string, body?: unknown): Promise<ApiEnvelope<T>> {
    return this.post<T>(path, body ?? {});
  }

  /** Authenticated JSON GET for domain API calls (cases, visits, etc.). */
  getApi<T>(path: string): Promise<ApiEnvelope<T>> {
    return this.get<T>(path);
  }

  /** Authenticated JSON PATCH for domain API calls (cases, visits, etc.). */
  patchApi<T>(path: string, body?: unknown): Promise<ApiEnvelope<T>> {
    return this.request<T>('PATCH', path, body ?? {});
  }

  /** Authenticated JSON PUT for domain API calls (device registration, etc.). */
  putApi<T>(path: string, body?: unknown): Promise<ApiEnvelope<T>> {
    return this.request<T>('PUT', path, body ?? {});
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    retried = false,
    allowRefresh = true,
  ): Promise<ApiEnvelope<T>> {
    const token = await secureStorage.getAccessToken();
    const headers: Record<string, string> = {
      Accept: 'application/json',
    };

    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (token && shouldAttachBearer(path)) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(`${environment.apiBaseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    if (response.status === 204) {
      return {data: {} as T, meta: {requestId: ''}};
    }

    const payload = (await response.json()) as ApiEnvelope<T> | ProblemDetails;

    if (!response.ok) {
      const problem = payload as ProblemDetails;
      if (response.status === 403 && problem.detail === DEACTIVATED_MESSAGE) {
        await this.clearSession();
        this.onDeactivated?.();
      }

      if (
        allowRefresh
        && response.status === 401
        && !retried
        && shouldAttachBearer(path)
      ) {
        const refreshed = await this.refreshSession();
        if (refreshed) {
          return this.request<T>(method, path, body, true, allowRefresh);
        }

        await this.clearSession();
        this.onSessionExpired?.();
      }

      if (response.status === 401 && retried) {
        await this.clearSession();
        this.onSessionExpired?.();
      }

      throw new ApiClientError(response.status, problem);
    }

    return payload as ApiEnvelope<T>;
  }
}

export class ApiClientError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? `Request failed with status ${status}`);
    this.name = 'ApiClientError';
  }
}

export const authSessionService = new AuthSessionService();
