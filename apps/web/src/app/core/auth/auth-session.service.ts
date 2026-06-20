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

@Injectable({ providedIn: 'root' })
export class AuthSessionService {
  private readonly accessToken = signal<string | null>(this.readStoredAccessToken());
  private readonly user = signal<SessionUserDto | null>(this.readStoredUser());
  private readonly challenge = signal<OtpChallengeState | null>(this.readStoredChallenge());
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
  ) {}

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

    this.challenge.set({
      challengeId: envelope.data.challengeId!,
      expiresInSeconds: envelope.data.expiresInSeconds ?? 300,
    });
    this.persistChallenge(this.challenge());

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
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    this.persistChallenge(null);
  }

  navigateAfterLogin(): void {
    if (this.isMobileOnlyRole()) {
      void this.router.navigate(['/mobile-only']);
      return;
    }

    if (this.isSupervisorRole()) {
      void this.router.navigate(['/crisis-queue']);
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
}
