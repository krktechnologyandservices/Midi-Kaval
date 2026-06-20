import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_RETRY_ATTEMPT } from './auth-http.context';
import { AuthSessionService } from './auth-session.service';
import type { ProblemDetails } from './auth.models';

const API_PREFIX = environment.apiBaseUrl;
const DEACTIVATED_MESSAGE = 'Contact your coordinator';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthSessionService);
  const isApiRequest = req.url.startsWith(API_PREFIX);
  const isAuthRefresh = req.url.includes('/api/v1/auth/refresh');
  const isAuthLogout = req.url.includes('/api/v1/auth/logout');
  const isAuthLogin = req.url.includes('/api/v1/auth/login')
    || req.url.includes('/api/v1/auth/verify-otp');
  const isAuthCookieFlow = isAuthRefresh || isAuthLogout;

  if (!isApiRequest) {
    return next(req);
  }

  let cloned = req.clone({ withCredentials: true });
  const token = auth.getAccessToken();
  if (token && !isAuthLogin && !isAuthCookieFlow) {
    cloned = cloned.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(cloned).pipe(
    catchError((error) => {
      const problem = error.error as ProblemDetails | null;
      if (error.status === 403 && problem?.detail === DEACTIVATED_MESSAGE) {
        auth.handleDeactivatedUser();
        return throwError(() => error);
      }

      if (
        error.status !== 401
        || isAuthRefresh
        || isAuthLogin
        || isAuthLogout
        || req.context.get(AUTH_RETRY_ATTEMPT)
      ) {
        if (error.status === 401 && req.context.get(AUTH_RETRY_ATTEMPT)) {
          auth.handleSessionExpired();
        }

        return throwError(() => error);
      }

      return from(auth.refreshSession()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            auth.handleSessionExpired();
            return throwError(() => error);
          }

          const retryToken = auth.getAccessToken();
          const retry = req.clone({
            withCredentials: true,
            context: req.context.set(AUTH_RETRY_ATTEMPT, true),
            setHeaders: retryToken
              ? { Authorization: `Bearer ${retryToken}` }
              : {},
          });

          return next(retry).pipe(
            catchError((retryError) => {
              if (retryError.status === 401) {
                auth.handleSessionExpired();
              }

              return throwError(() => retryError);
            }),
          );
        }),
      );
    }),
  );
};
