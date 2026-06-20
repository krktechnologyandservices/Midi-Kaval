import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_RETRY_ATTEMPT } from './auth-http.context';
import { authInterceptor } from './auth.interceptor';
import { AuthSessionService } from './auth-session.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: jasmine.SpyObj<AuthSessionService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthSessionService>('AuthSessionService', [
      'getAccessToken',
      'refreshSession',
      'handleSessionExpired',
      'handleDeactivatedUser',
    ]);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthSessionService, useValue: auth },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth.getAccessToken.and.returnValue('access-token');
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retries a protected request once after refresh succeeds', async () => {
    auth.refreshSession.and.resolveTo(true);
    auth.getAccessToken.and.returnValues('access-token', 'new-token');

    const responsePromise = firstValueFrom(
      http.get(`${environment.apiBaseUrl}/api/v1/auth/me`),
    );

    httpMock.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`).flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await Promise.resolve();
    expect(auth.refreshSession).toHaveBeenCalledTimes(1);

    const retry = httpMock.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`);
    expect(retry.request.context.get(AUTH_RETRY_ATTEMPT)).toBeTrue();
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    retry.flush({ data: { id: '1', email: 'a@b.com', role: 'Director' }, meta: { requestId: 'r2' } });

    await responsePromise;
  });

  it('routes to session expired when retry still returns 401', async () => {
    auth.refreshSession.and.resolveTo(true);
    auth.getAccessToken.and.returnValue('new-token');

    http.get(`${environment.apiBaseUrl}/api/v1/auth/me`).subscribe({ error: () => undefined });

    httpMock.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`).flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await Promise.resolve();

    httpMock.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`).flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await Promise.resolve();

    expect(auth.handleSessionExpired).toHaveBeenCalled();
    expect(auth.refreshSession).toHaveBeenCalledTimes(1);
  });

  it('clears session on deactivated 403 responses', () => {
    http.get(`${environment.apiBaseUrl}/api/v1/auth/me`).subscribe({ error: () => undefined });

    httpMock.expectOne(`${environment.apiBaseUrl}/api/v1/auth/me`).flush(
      { detail: 'Contact your coordinator' },
      { status: 403, statusText: 'Forbidden' },
    );

    expect(auth.handleDeactivatedUser).toHaveBeenCalled();
  });
});
