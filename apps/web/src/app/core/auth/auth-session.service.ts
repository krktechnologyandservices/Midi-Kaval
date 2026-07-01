import { computed, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AppRole } from '@midi-kaval/shared-types';
import { environment } from '../../../environments/environment';
import {
  ACCESS_TOKEN_KEY,
  ApiEnvelope,
  AUTH_HTTP_OPTIONS,
  CHALLENGE_KEY,
  TOTP_KEY,
  TWOFA_SETUP_KEY,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  RefreshResponse,
  SessionUserDto,
  USER_KEY,
  VerifyOtpRequest,
  VerifyOtpResponse,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  ResetPasswordRequest,
  ResetPasswordResponse,
} from './auth.models';

export interface OtpChallengeState {
  challengeId: string;
  expiresInSeconds: number;
}

export interface TotpState {
  userId: string;
  tokenVersion: number;
  totpChallengeId: string;
}

@Injectable({ providedIn: 'root' })
export class AuthSessionService {
  private readonly accessToken = signal<string | null>(this.readStoredAccessToken());
  private readonly user = signal<SessionUserDto | null>(this.readStoredUser());
  private readonly challenge = signal<OtpChallengeState | null>(this.readStoredChallenge());
  readonly requiresTotp = signal(false);
  readonly totpUserId = signal<string | null>(null);
  readonly totpTokenVersion = signal<number>(0);
  readonly totpChallengeId = signal<string | null>(null);
  readonly requires2faSetup = signal(false);
  readonly setupUrl = signal<string | null>(null);
  readonly orgRequires2fa = signal(false);
  private refreshInFlight: Promise<boolean> | null = null;

  readonly isAuthenticated = computed(() => !!this.accessToken());
  readonly currentUser = computed(() => this.user());
  readonly otpChallenge = computed(() => this.challenge());

  readonly isSupervisorRole = computed(() => {
    const role = this.user()?.role;
    return role === AppRole.Director || role === AppRole.Coordinator;
  });

  readonly isMobileOnlyRole = computed(() => {
    const role = this.user()?.role;
    return role === AppRole.SocialWorker || role === AppRole.CaseWorker;
  });

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {
    this.restoreTotpState();
    this.restore2faSetupState();
  }

  getAccessToken(): string | null {
    return this.accessToken();
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    const envelope = await firstValueFrom(
      this.http.post<ApiEnvelope<LoginResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/login`,
        request,
        AUTH_HTTP_OPTIONS,
      ),
    );

    const loginData: Record<string, unknown> = envelope.data as unknown as Record<string, unknown>;
    this.requires2faSetup.set((loginData['requires2faSetup'] as boolean) ?? false);
    this.setupUrl.set((loginData['setupUrl'] as string) ?? null);
    this.orgRequires2fa.set((loginData['orgRequires2fa'] as boolean) ?? false);
    this.persist2faSetupState();
    if (loginData['requiresTotp']) {
      this.requiresTotp.set(true);
      this.totpUserId.set(loginData['userId'] as string);
      this.totpTokenVersion.set((loginData['tokenVersion'] as number) ?? 0);
      this.totpChallengeId.set(loginData['totpChallengeId'] as string);
      this.persistTotpState({
        userId: loginData['userId'] as string,
        tokenVersion: (loginData['tokenVersion'] as number) ?? 0,
        totpChallengeId: loginData['totpChallengeId'] as string,
      });
      await this.router.navigate(['/login/totp']);
      return envelope.data;
    }

    this.challenge.set({
      challengeId: envelope.data.challengeId!,
      expiresInSeconds: envelope.data.expiresInSeconds ?? 300,
    });
    this.persistChallenge(this.challenge());

    await this.router.navigate(['/login/otp']);

    return envelope.data;
  }

  async verifyOtp(code: string): Promise<VerifyOtpResponse> {
    const current = this.challenge();
    if (!current) {
      throw new Error('No OTP challenge in progress.');
    }

    const body: VerifyOtpRequest = {
      challengeId: current.challengeId,
      code,
    };

    const envelope = await firstValueFrom(
      this.http.post<ApiEnvelope<VerifyOtpResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/verify-otp`,
        body,
        AUTH_HTTP_OPTIONS,
      ),
    );

    this.applySession(envelope.data.accessToken!, {
      id: envelope.data.user!.id,
      email: envelope.data.user!.email,
      role: envelope.data.user!.role,
    });
    this.challenge.set(null);
    this.persistChallenge(null);
    return envelope.data;
  }

  async verifyTotpLogin(code: string): Promise<VerifyOtpResponse> {
    const userId = this.totpUserId();
    const tokenVersion = this.totpTokenVersion();
    const totpChallengeId = this.totpChallengeId();
    if (!userId) throw new Error('No TOTP login in progress.');

    const envelope = await firstValueFrom(
      this.http.post<ApiEnvelope<VerifyOtpResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/verify-totp-login`,
        { userId, code, tokenVersion, totpChallengeId },
        AUTH_HTTP_OPTIONS,
      ),
    );

    this.applySession(envelope.data.accessToken!, {
      id: envelope.data.user!.id,
      email: envelope.data.user!.email,
      role: envelope.data.user!.role,
    });
    this.requiresTotp.set(false);
    this.totpUserId.set(null);
    this.totpTokenVersion.set(0);
    this.totpChallengeId.set(null);
    this.persistTotpState(null);
    return envelope.data;
  }

  async forgotPassword(email: string): Promise<ForgotPasswordResponse> {
    const body: ForgotPasswordRequest = { email };
    const envelope = await firstValueFrom(
      this.http.post<ApiEnvelope<ForgotPasswordResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/forgot-password`,
        body,
        AUTH_HTTP_OPTIONS,
      ),
    );

    return envelope.data;
  }

  async resetPassword(token: string, newPassword: string): Promise<ResetPasswordResponse> {
    const body: ResetPasswordRequest = { token, newPassword };
    const envelope = await firstValueFrom(
      this.http.post<ApiEnvelope<ResetPasswordResponse>>(
        `${environment.apiBaseUrl}/api/v1/auth/reset-password`,
        body,
        AUTH_HTTP_OPTIONS,
      ),
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

  async bootstrapSession(): Promise<void> {
    if (!this.accessToken()) {
      return;
    }

    await this.loadCurrentUser();
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post(
          `${environment.apiBaseUrl}/api/v1/auth/logout`,
          {},
          AUTH_HTTP_OPTIONS,
        ),
      );
    } finally {
      this.clearSession();
      await this.router.navigate(['/login']);
    }
  }

  async loadCurrentUser(): Promise<SessionUserDto | null> {
    const token = this.accessToken();
    if (!token) {
      return null;
    }

    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<SessionUserDto>>(
          `${environment.apiBaseUrl}/api/v1/auth/me`,
        ),
      );
      this.user.set(envelope.data);
      sessionStorage.setItem(USER_KEY, JSON.stringify(envelope.data));
      return envelope.data;
    } catch (error) {
      if (
        error instanceof HttpErrorResponse
        && (error.status === 401 || error.status === 403)
      ) {
        this.clearSession();
      }

      return null;
    }
  }

  clearSession(): void {
    this.accessToken.set(null);
    this.user.set(null);
    this.challenge.set(null);
    this.requiresTotp.set(false);
    this.totpUserId.set(null);
    this.totpTokenVersion.set(0);
    this.totpChallengeId.set(null);
    this.requires2faSetup.set(false);
    this.setupUrl.set(null);
    this.orgRequires2fa.set(false);
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    this.persistChallenge(null);
    this.persistTotpState(null);
    this.persist2faSetupState();
  }

  navigateAfterLogin(): void {
    if (this.requires2faSetup()) {
      const url = this.setupUrl() ?? '/settings/2fa';
      void this.router.navigate([url]);
      return;
    }

    if (this.isMobileOnlyRole()) {
      void this.router.navigate(['/mobile-only']);
      return;
    }

    if (this.isSupervisorRole()) {
      void this.router.navigate(['/crisis-queue']);
      return;
    }

    if (this.currentUser()?.role === AppRole.Vendor) {
      void this.router.navigate(['/vendor']);
      return;
    }

    void this.router.navigate(['/login']);
  }

  handleSessionExpired(): void {
    this.clearSession();
    void this.router.navigate(['/session-expired']);
  }

  handleDeactivatedUser(): void {
    this.clearSession();
    void this.router.navigate(['/login']);
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as ProblemDetails | null;
      if (problem?.detail) {
        return problem.detail;
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

  private async performRefresh(): Promise<boolean> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<RefreshResponse>>(
          `${environment.apiBaseUrl}/api/v1/auth/refresh`,
          {},
          AUTH_HTTP_OPTIONS,
        ),
      );

      const token = envelope.data.accessToken;
      if (!token) {
        return false;
      }

      this.accessToken.set(token);
      sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
      await this.loadCurrentUser();
      return true;
    } catch {
      return false;
    }
  }

  private applySession(token: string, user: SessionUserDto): void {
    this.accessToken.set(token);
    this.user.set(user);
    sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
    sessionStorage.setItem(USER_KEY, JSON.stringify(user));
  }

  private readStoredAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  private readStoredUser(): SessionUserDto | null {
    const raw = sessionStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as SessionUserDto;
    } catch {
      return null;
    }
  }

  private readStoredChallenge(): OtpChallengeState | null {
    const raw = sessionStorage.getItem(CHALLENGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as OtpChallengeState;
    } catch {
      return null;
    }
  }

  private persistChallenge(state: OtpChallengeState | null): void {
    if (!state) {
      sessionStorage.removeItem(CHALLENGE_KEY);
      return;
    }

    sessionStorage.setItem(CHALLENGE_KEY, JSON.stringify(state));
  }

  private persistTotpState(state: TotpState | null): void {
    if (!state) {
      sessionStorage.removeItem(TOTP_KEY);
      return;
    }

    sessionStorage.setItem(TOTP_KEY, JSON.stringify(state));
  }

  private readStoredTotpState(): TotpState | null {
    const raw = sessionStorage.getItem(TOTP_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as TotpState;
    } catch {
      return null;
    }
  }

  private restoreTotpState(): void {
    const state = this.readStoredTotpState();
    if (state) {
      this.requiresTotp.set(true);
      this.totpUserId.set(state.userId);
      this.totpTokenVersion.set(state.tokenVersion);
      this.totpChallengeId.set(state.totpChallengeId);
    }
  }

  private persist2faSetupState(): void {
    if (!this.requires2faSetup() && !this.setupUrl() && !this.orgRequires2fa()) {
      sessionStorage.removeItem(TWOFA_SETUP_KEY);
      return;
    }

    sessionStorage.setItem(
      TWOFA_SETUP_KEY,
      JSON.stringify({
        requires2faSetup: this.requires2faSetup(),
        setupUrl: this.setupUrl(),
        orgRequires2fa: this.orgRequires2fa(),
      }),
    );
  }

  private restore2faSetupState(): void {
    const state = this.readStored2faSetupState();
    if (state) {
      this.requires2faSetup.set(state.requires2faSetup);
      this.setupUrl.set(state.setupUrl);
      this.orgRequires2fa.set(state.orgRequires2fa);
    }
  }

  private readStored2faSetupState(): { requires2faSetup: boolean; setupUrl: string | null; orgRequires2fa: boolean } | null {
    const raw = sessionStorage.getItem(TWOFA_SETUP_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }
}
