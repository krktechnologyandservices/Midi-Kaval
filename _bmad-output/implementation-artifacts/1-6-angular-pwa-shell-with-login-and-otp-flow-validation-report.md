# Story Code Review Report — 1.6 Angular PWA Shell with Login and OTP Flow

**Story:** `1-6-angular-pwa-shell-with-login-and-otp-flow`  
**Reviewed:** 2026-06-14  
**Verdict:** **PASS — all patches applied**

---

## Summary

Adversarial review of Story 1.6 found 9 issues (2 critical, 3 high, 3 medium, 1 low). All were patched. Tests: **15** Angular unit tests, **30** .NET tests passing.

---

## Findings Applied

### Critical (2) — fixed

**1. Auth interceptor retry 401 never routed to session-expired**  
`next(retry)` inside `switchMap` did not handle retry failures; `handleSessionExpired()` was unreachable.  
**Fix:** Wrap `next(retry)` in `catchError`; call `handleSessionExpired()` on retry 401.

**2. Potential infinite refresh loop on repeated 401**  
Original request context was reused in nested `catchError`; retry failures could re-enter refresh.  
**Fix:** `AUTH_RETRY_ATTEMPT` `HttpContextToken` + single retry; session-expired on second 401.

### High (3) — fixed

**3. Stale session restored without `/auth/me` validation**  
`sessionStorage` token restored on load with no server check.  
**Fix:** `bootstrapSession()` via `APP_INITIALIZER`.

**4. `/auth/me` 401/403 left stale token in storage**  
**Fix:** `loadCurrentUser()` clears session on 401/403.

**5. Deactivated user 403 not handled globally**  
**Fix:** Interceptor maps `Contact your coordinator` → `handleDeactivatedUser()`.

### Medium (3) — fixed

**6. OTP challenge lost on page refresh**  
**Fix:** Persist challenge in `sessionStorage` (`CHALLENGE_KEY`).

**7. CORS policy with credentials but no origins in non-Development**  
**Fix:** Throw `InvalidOperationException` if `AllowedOrigins` empty outside Development/Testing.

**8. Bearer token sent on refresh/logout**  
**Fix:** Exclude cookie-auth endpoints from `Authorization` header.

### Low (1) — fixed

**9. Toolbar visibility lagged behind auth state**  
**Fix:** `effect()` tracks `isAuthenticated()` for toolbar updates.

---

## Test additions

- `auth.interceptor.spec.ts` — retry-once, session-expired on double 401, deactivated 403
- `auth-session.service.spec.ts` — challenge persistence, `/me` 401 clears session

---

## Recommendation

Story 1.6 → **`done`**. Proceed to Story 1.7 (RN mobile login) or `bmad-create-story` for next epic item.
