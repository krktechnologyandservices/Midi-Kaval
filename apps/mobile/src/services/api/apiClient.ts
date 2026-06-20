export {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';

const PUBLIC_AUTH_PATHS = [
  '/auth/login',
  '/auth/verify-otp',
  '/auth/refresh',
  '/auth/logout',
  '/auth/forgot-password',
  '/auth/reset-password',
] as const;

/** Bearer is omitted on unauthenticated auth endpoints (mobile refresh uses body, not cookies). */
export function shouldAttachBearer(path: string): boolean {
  return !PUBLIC_AUTH_PATHS.some(segment => path.includes(segment));
}