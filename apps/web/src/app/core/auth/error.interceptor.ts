import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

const API_PREFIX = environment.apiBaseUrl;

function normalizeApiError(error: HttpErrorResponse): HttpErrorResponse {
  if (!error.url?.startsWith(API_PREFIX)) {
    return error;
  }

  const problem = error.error as { detail?: string } | null;
  if (!problem?.detail) {
    return error;
  }

  return new HttpErrorResponse({
    error: { ...problem, detail: problem.detail },
    headers: error.headers,
    status: error.status,
    statusText: error.statusText,
    url: error.url ?? undefined,
  });
}

export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: HttpErrorResponse) =>
      throwError(() => normalizeApiError(error)),
    ),
  );
