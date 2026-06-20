import { TestBed } from '@angular/core/testing';
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { environment } from '../../../environments/environment';
import {
  ACCESS_TOKEN_KEY,
  CHALLENGE_KEY,
  USER_KEY,
} from './auth.models';
import { AuthSessionService } from './auth-session.service';

describe('AuthSessionService', () => {
  let service: AuthSessionService;
  let http: HttpTestingController;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    sessionStorage.clear();
    router = jasmine.createSpyObj('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthSessionService,
        { provide: Router, useValue: router },
      ],
    });

    service = TestBed.inject(AuthSessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('login stores OTP challenge', async () => {
    const loginPromise = service.login({
      email: 'director@pilot.example',
      password: 'password',
    });

    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/login`);
    expect(req.request.withCredentials).toBeTrue();
    req.flush({
      data: { challengeId: '11111111-1111-4111-8111-111111111111', expiresInSeconds: 300 },
      meta: { requestId: 'r1' },
    });

    const result = await loginPromise;
    expect(result.challengeId).toBe('11111111-1111-4111-8111-111111111111');
    expect(service.otpChallenge()?.challengeId).toBe('11111111-1111-4111-8111-111111111111');
    expect(sessionStorage.getItem(CHALLENGE_KEY)).toContain('11111111-1111-4111-8111-111111111111');
  });

  it('verifyOtp stores access token and user', async () => {
    const loginPromise = service.login({ email: 'a@b.com', password: 'password1' });
    http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/login`).flush({
      data: { challengeId: '22222222-2222-4222-8222-222222222222', expiresInSeconds: 300 },
      meta: { requestId: 'r2' },
    });
    await loginPromise;

    const verifyPromise = service.verifyOtp('123456');
    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/verify-otp`);
    expect(req.request.body).toEqual({
      challengeId: '22222222-2222-4222-8222-222222222222',
      code: '123456',
    });
    expect(req.request.withCredentials).toBeTrue();
    req.flush({
      data: {
        accessToken: 'access-token',
        expiresIn: 900,
        user: {
          id: '33333333-3333-4333-8333-333333333333',
          email: 'director@pilot.example',
          role: AppRole.Director,
        },
      },
      meta: { requestId: 'r3' },
    });

    await verifyPromise;
    expect(service.getAccessToken()).toBe('access-token');
    expect(sessionStorage.getItem(ACCESS_TOKEN_KEY)).toBe('access-token');
    expect(service.currentUser()?.role).toBe(AppRole.Director);
    expect(service.otpChallenge()).toBeNull();
  });

  it('refreshSession updates token with cookie-only body', async () => {
    const refreshPromise = service.refreshSession();
    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/refresh`);
    expect(req.request.body).toEqual({});
    expect(req.request.withCredentials).toBeTrue();
    req.flush({
      data: { accessToken: 'new-token', expiresIn: 900 },
      meta: { requestId: 'r4' },
    });

    await Promise.resolve();
    const meReq = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`);
    meReq.flush({
      data: { id: '1', email: 'director@pilot.example', role: AppRole.Director },
      meta: { requestId: 'r5' },
    });

    expect(await refreshPromise).toBeTrue();
    expect(service.getAccessToken()).toBe('new-token');
  });

  it('logout clears session and navigates to login', async () => {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, 'token');
    sessionStorage.setItem(USER_KEY, JSON.stringify({ role: AppRole.Director }));

    const logoutPromise = service.logout();
    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/logout`);
    expect(req.request.withCredentials).toBeTrue();
    req.flush(null, { status: 204, statusText: 'No Content' });

    await logoutPromise;
    expect(service.getAccessToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('loadCurrentUser clears session on 401', async () => {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, 'stale-token');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthSessionService,
        { provide: Router, useValue: router },
      ],
    });
    service = TestBed.inject(AuthSessionService);
    http = TestBed.inject(HttpTestingController);

    const loadPromise = service.loadCurrentUser();
    http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`).flush(
      { detail: 'Invalid access token.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(await loadPromise).toBeNull();
    expect(service.getAccessToken()).toBeNull();
  });

  it('handleSessionExpired clears session and routes to session-expired', () => {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, 'token');
    service.handleSessionExpired();
    expect(service.getAccessToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/session-expired']);
  });

  it('forgotPassword posts email to forgot-password endpoint', async () => {
    const promise = service.forgotPassword('user@pilot.example');
    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/forgot-password`);
    expect(req.request.body).toEqual({ email: 'user@pilot.example' });
    expect(req.request.withCredentials).toBeTrue();
    req.flush({
      data: { message: 'If an account exists for that email, we sent reset instructions.' },
      meta: { requestId: 'r6' },
    });

    const result = await promise;
    expect(result.message).toContain('reset instructions');
  });

  it('resetPassword posts token and new password', async () => {
    const promise = service.resetPassword('opaque-token', 'newPassword123');
    const req = http.expectOne(`${environment.apiBaseUrl}/api/v1/auth/reset-password`);
    expect(req.request.body).toEqual({ token: 'opaque-token', newPassword: 'newPassword123' });
    req.flush({
      data: { message: 'Password updated. Sign in with your new password.' },
      meta: { requestId: 'r7' },
    });

    const result = await promise;
    expect(result.message).toContain('Password updated');
  });
});
