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
  TOTP_CHALLENGE_KEY,
  TotpChallengeState,
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
  private totpChallenge: TotpChallengeState | null = null;
  private stepUpChallenge: OtpChallengeState | null = null;
  private lastLoginOtpVerifiedAtUtc: string | null = null;
  private user: SessionUserDto | null = null;
  private refreshInFlight: Promise<boolean> | null = null;
  onSessionExpired: (() => void) | null = null;
  onDeactivated: (() => void) | null = null;

  getOtpChallenge(): OtpChallengeState | null {
    return this.challenge;
  }

  async clearOtpChallenge(): Promise<void> {
    this.challenge = null;
    await AsyncStorage.removeItem(CHALLENGE_KEY);
  }

  getTotpChallenge(): TotpChallengeState | null {
    return this.totpChallenge;
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

    // requiresTotp/totpChallengeId/userId/tokenVersion aren't on the generated LoginResponse
    // type yet (see auth.models.ts) but are always present on the real API response for
    // users enrolled in TOTP — read them via an unknown-record cast, matching the same
    // workaround already used on web (apps/web/.../auth-session.service.ts).
    const loginData = envelope.data as unknown as Record<string, unknown>;

    if (loginData['requiresTotp']) {
      this.totpChallenge = {
        userId: loginData['userId'] as string,
        tokenVersion: (loginData['tokenVersion'] as number) ?? 0,
        totpChallengeId: loginData['totpChallengeId'] as string,
      };
      await AsyncStorage.setItem(
        TOTP_CHALLENGE_KEY,
        JSON.stringify(this.totpChallenge),
      );
      this.challenge = null;
      await AsyncStorage.removeItem(CHALLENGE_KEY);
      return envelope.data;
    }

    const expiresInSeconds = envelope.data.expiresInSeconds ?? 300;
    this.challenge = {
      challengeId: envelope.data.challengeId!,
      expiresInSeconds,
      expiresAtUtc: Date.now() + expiresInSeconds * 1000,
    };
    await AsyncStorage.setItem(CHALLENGE_KEY, JSON.stringify(this.challenge));
    this.totpChallenge = null;
    await AsyncStorage.removeItem(TOTP_CHALLENGE_KEY);
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

  async verifyTotpLogin(code: string): Promise<VerifyOtpResponse> {
    if (!this.totpChallenge) {
      const stored = await AsyncStorage.getItem(TOTP_CHALLENGE_KEY);
      if (stored) {
        try {
          this.totpChallenge = JSON.parse(stored) as TotpChallengeState;
        } catch {
          await AsyncStorage.removeItem(TOTP_CHALLENGE_KEY);
        }
      }
    }

    if (!this.totpChallenge) {
      throw new Error('No TOTP login in progress.');
    }

    const body = {
      userId: this.totpChallenge.userId,
      totpChallengeId: this.totpChallenge.totpChallengeId,
      tokenVersion: this.totpChallenge.tokenVersion,
      code,
    };

    const envelope = await this.post<VerifyOtpResponse>(
      '/api/v1/auth/verify-totp-login',
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

    this.totpChallenge = null;
    await AsyncStorage.removeItem(TOTP_CHALLENGE_KEY);
    this.lastLoginOtpVerifiedAtUtc = new Date().toISOString();
    return envelope.data;
  }

  async stepUp(): Promise<StepUpResponse> {
    this.challenge = null;
    await AsyncStorage.removeItem(CHALLENGE_KEY);
    const envelope = await this.postApi<StepUpResponse>('/api/v1/auth/step-up');
    const expiresInSeconds = envelope.data.expiresInSeconds ?? 300;
    this.stepUpChallenge = {
      challengeId: envelope.data.challengeId,
      expiresInSeconds,
      expiresAtUtc: Date.now() + expiresInSeconds * 1000,
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
        const parsed = JSON.parse(storedChallenge) as OtpChallengeState;
        // A challenge left over from a login the user never finished (missed OTP, app
        // killed) must not force the OTP screen forever — without this check it was
        // reloaded as-is every launch, blocking the user from ever reaching Login again.
        if (parsed.expiresAtUtc && parsed.expiresAtUtc > Date.now()) {
          this.challenge = parsed;
        } else {
          await AsyncStorage.removeItem(CHALLENGE_KEY);
        }
      } catch {
        await AsyncStorage.removeItem(CHALLENGE_KEY);
      }
    }

    const storedTotpChallenge = await AsyncStorage.getItem(TOTP_CHALLENGE_KEY);
    if (storedTotpChallenge) {
      try {
        this.totpChallenge = JSON.parse(storedTotpChallenge) as TotpChallengeState;
      } catch {
        await AsyncStorage.removeItem(TOTP_CHALLENGE_KEY);
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
    this.totpChallenge = null;
    this.stepUpChallenge = null;
    this.lastLoginOtpVerifiedAtUtc = null;
    await secureStorage.clearTokens();
    await AsyncStorage.removeItem(CHALLENGE_KEY);
    await AsyncStorage.removeItem(TOTP_CHALLENGE_KEY);
    await clearCommandStripCache();
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof ApiClientError) {
      if (error.problem?.detail) {
        return error.problem.detail;
      }
      // ASP.NET Core's automatic [ApiController] model validation returns a
      // ValidationProblemDetails with per-field messages under `errors` and no
      // top-level `detail` — without this, those responses fell through to the
      // fully generic message below and the user never saw what was actually wrong.
      const fieldErrors = error.problem?.errors;
      if (fieldErrors) {
        const firstMessages = Object.values(fieldErrors).flat();
        if (firstMessages.length > 0) {
          return firstMessages[0];
        }
      }
      if (error.status === 401) {
        return 'Invalid email or password.';
      }
      if (error.status === 429) {
        return 'Too many attempts. Please try again later.';
      }
      if (error.problem?.title) {
        return error.problem.title;
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

  /**
   * Authenticated multipart POST (file uploads) — deliberately not routed through
   * request<T>(), since that always sets Content-Type: application/json and
   * JSON.stringifies the body, which would break a FormData upload. fetch() sets its
   * own multipart Content-Type (with boundary) automatically when given a FormData body.
   */
  async postMultipartApi<T>(path: string, formData: FormData): Promise<ApiEnvelope<T>> {
    const token = await secureStorage.getAccessToken();
    const headers: Record<string, string> = {Accept: 'application/json'};
    if (token && shouldAttachBearer(path)) {
      headers.Authorization = `Bearer ${token}`;
    }

    let response: Response;
    try {
      response = await fetch(`${environment.apiBaseUrl}${path}`, {
        method: 'POST',
        headers,
        body: formData,
      });
    } catch {
      throw new ApiClientError(0, null);
    }

    let payload: ApiEnvelope<T> | ProblemDetails;
    try {
      payload = (await response.json()) as ApiEnvelope<T> | ProblemDetails;
    } catch {
      payload = {} as ProblemDetails;
    }

    if (!response.ok) {
      throw new ApiClientError(response.status, payload as ProblemDetails);
    }

    return payload as ApiEnvelope<T>;
  }

  /** Authenticated GET expecting a raw binary body (file download), not a JSON envelope. */
  async getBinaryApi(path: string): Promise<Blob> {
    const token = await secureStorage.getAccessToken();
    const headers: Record<string, string> = {};
    if (token && shouldAttachBearer(path)) {
      headers.Authorization = `Bearer ${token}`;
    }

    let response: Response;
    try {
      response = await fetch(`${environment.apiBaseUrl}${path}`, {method: 'GET', headers});
    } catch {
      throw new ApiClientError(0, null);
    }

    if (!response.ok) {
      let problem: ProblemDetails | null = null;
      try {
        problem = (await response.json()) as ProblemDetails;
      } catch {
        // Non-JSON error body — leave problem null, status code alone still identifies the failure.
      }
      throw new ApiClientError(response.status, problem);
    }

    return response.blob();
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

    let response: Response;
    try {
      response = await fetch(`${environment.apiBaseUrl}${path}`, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
    } catch {
      // fetch() itself rejected (no connectivity, DNS/TLS failure, timeout) — a raw
      // TypeError here previously escaped uncategorized, so callers never recognized
      // it as a network failure (that classification only applied to HTTP responses)
      // and the offline-draft recovery path never triggered.
      throw new ApiClientError(0, null);
    }

    if (response.status === 204) {
      return {data: {} as T, meta: {requestId: ''}};
    }

    let payload: ApiEnvelope<T> | ProblemDetails;
    try {
      payload = (await response.json()) as ApiEnvelope<T> | ProblemDetails;
    } catch {
      // Non-JSON or empty body (e.g. a raw 5xx error page). Fall back to an empty
      // problem so the existing status-code handling below (401 refresh-retry, 403
      // deactivated check) still runs instead of the parse error escaping uncategorized.
      payload = {} as ProblemDetails;
    }

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
