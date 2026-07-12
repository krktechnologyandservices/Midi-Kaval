// Note: no "/api" suffix here — every call site appends "/api/v1/..." itself
// (e.g. `${environment.apiBaseUrl}/api/v1/auth/login`), so including it here
// would double up to ".../api/api/v1/...".
export const environment = {
  apiBaseUrl: 'https://midi-kaval.duckdns.org',
};
