---
baseline_commit: NO_VCS
---

# Story 1.9: Self-Service Password Reset

Status: done

<!-- Validated: 2026-06-15 — see 1-9-self-service-password-reset-validation-report.md -->

## Story

As a **user who forgot my password**,
I want to reset it via a secure email link,
so that I can regain access without waiting for an administrator (FR-1).

## Acceptance Criteria

1. **Given** an active user account with a registered email  
   **When** I `POST /api/v1/auth/forgot-password` with `{ email }`  
   **Then** a time-limited single-use reset token is created in Redis and a reset link is emailed via `IEmailSender`  
   **And** the API returns **200** with envelope `{ data: { message } }` using a **generic success message** whether or not the email exists (no account enumeration)  
   **And** the endpoint is `[AllowAnonymous]`, rate-limited (`auth-forgot-password`), and deactivated accounts receive **no usable token/email** but still get the same generic 200 response

2. **Given** a valid, unexpired, unused reset token from the email link  
   **When** I `POST /api/v1/auth/reset-password` with `{ token, newPassword }` where `newPassword` meets policy (min **8** characters — match login)  
   **Then** the user's password hash updates, `IUserSessionService.InvalidateUserSessionsAsync` runs (`token_version` incremented, all refresh tokens revoked), and the reset token is consumed (single-use)  
   **And** response is **200** `{ data: { message } }` with success copy; user must log in again with OTP (no auto-login)  
   **And** expired or already-used tokens return **400** Problem Details; unknown/malformed token returns **401** (Redis miss on never-issued token → 401; consumed/revoked marker → 400)  
   **And** `audit_events` records `auth.password_reset.requested` (active user only) and `auth.password_reset.completed` on success

3. **Given** the Angular PWA login screen  
   **When** I tap **"Forgot password?"** → submit email → open `/reset-password?token=…` from email → set new password  
   **Then** accessible labelled inputs and `aria-live="polite"` error regions guide the flow (UX-DR8)  
   **And** success navigates to `/login` with confirmation copy; voice/tone matches EXPERIENCE.md

4. **Given** the React Native login screen  
   **When** I tap **"Forgot password?"** → submit email → complete reset on **Reset Password** screen (token from deep link query or manual entry for dev)  
   **Then** accessible labelled inputs and `accessibilityLiveRegion="polite"` on errors (UX-DR8)  
   **And** success returns to **Login**; same generic/success strings as web

5. **Given** Stories 1.4–1.8 baseline  
   **When** I run `dotnet test Midi-Kaval.slnx`, `npm run test:web`, and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** new integration tests cover forgot/reset happy path, enumeration-safe response, deactivated skip, expired/used/invalid tokens, session invalidation, and audit rows  
   **And** OpenAPI + `packages/api-client` regenerated with new auth routes

6. **Given** Story 9.2 (staff directory) is not yet implemented  
   **When** this story ships  
   **Then** **admin force-reset** remains out of scope — only self-service flow is delivered

## Tasks / Subtasks

- [x] **API — password reset store + options** (AC: 1, 2)
  - [x] `PasswordResetOptions` in `AuthOptions.cs` — section `PasswordReset`; `ExpiryMinutes` (default 60), `WebResetUrl` (default `http://localhost:4200/reset-password`)
  - [x] `PasswordResetTokenStore.cs` — Redis via `IConnectionMultiplexer`; opaque token (mirror `RefreshTokenStore.GenerateToken`); store **SHA-256 hash** + `{ userId }`; `TryPeekAsync` / `TryConsumeAsync` (Lua atomic delete); `revoked` marker for reuse → 400; revoke prior active token per user on new forgot request
  - [x] Register store + options in `AuthServiceCollectionExtensions`; bind `PasswordReset` in test factory env if needed

- [x] **API — AuthService + endpoints** (AC: 1, 2, 5)
  - [x] DTOs: `ForgotPasswordRequest`, `ForgotPasswordResponse`, `ResetPasswordRequest`, `ResetPasswordResponse`
  - [x] `AuthService.ForgotPasswordAsync` — normalize email; if **active** user exists → create token, email link, audit `auth.password_reset.requested`; **always** return generic 200 message (unknown email, deactivated, empty email, SMTP failure — no enumeration, no 503 leak)
  - [x] `AuthService.ResetPasswordAsync` — validate `newPassword` length ≥ 8 **before** token consume; `TryPeekAsync` → update password hash + `SaveChanges` → `InvalidateUserSessionsAsync` → `TryConsumeAsync` → audit `auth.password_reset.completed`
  - [x] `AuthController` — `POST forgot-password`, `POST reset-password`; `[AllowAnonymous]`; rate limits `auth-forgot-password`, `auth-reset-password`
  - [x] Extend `AuditEventTypes` with password reset event constants
  - [x] Generic messages as constants (no email enumeration)

- [x] **API — integration tests** (AC: 1, 2, 5)
  - [x] `PasswordResetTests.cs` — `[Collection("AuthIntegration")]`, reuse `AuthWebApplicationFactory` + `FakeEmailSender`
  - [x] Tests: happy path forgot→email→reset→**login+OTP with new password**; unknown email 200 generic; deactivated no email; empty email 200 generic; used token 400; unknown token 401; short password 400; `token_version` bump blocks old refresh; audit rows (`password_reset.requested`, `password_reset.completed`, `session.invalidated`)

- [x] **OpenAPI + api-client** (AC: 5)
  - [x] `[ProducesResponseType]` on new endpoints
  - [x] Regenerate `packages/api-client` + extend `SwaggerEndpointTests` with `/api/v1/auth/forgot-password` and `/api/v1/auth/reset-password`

- [x] **Angular web UI** (AC: 3, 5)
  - [x] `forgot-password.component` — email form, link from login, generic success state
  - [x] `reset-password.component` — read `token` from query param; new password + confirm; min 8 chars
  - [x] Extend `AuthSessionService` + `auth.models.ts` — `forgotPassword(email)`, `resetPassword(token, newPassword)`
  - [x] Routes: `/forgot-password`, `/reset-password` — **no `guestGuard`** (reset link must work with stale sessions)
  - [x] "Forgot password?" link on `login.component.html`
  - [x] Unit tests: components have `aria-live`; service methods mock HttpClient

- [x] **React Native mobile UI** (AC: 4, 5)
  - [x] `ForgotPasswordScreen`, `ResetPasswordScreen` in `AuthNavigator`
  - [x] Extend `AuthSessionService` + `auth.models.ts` — `forgotPassword`, `resetPassword`
  - [x] Update `shouldAttachBearer` / `PUBLIC_AUTH_PATHS` — add `/auth/forgot-password`, `/auth/reset-password`
  - [x] Login screen link → ForgotPassword; ResetPassword accepts optional `token` route param (deep link ready; manual token field for dev)
  - [x] Jest tests: accessible error region; navigation to Login on success

- [x] **Documentation** (AC: 5, 6)
  - [x] README + `infra/.env.example` — forgot/reset endpoints, `PasswordReset__WebResetUrl`, rate limits, enumeration policy
  - [x] Note admin force-reset deferred to Story 9.2

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — final story before epic retrospective. Stories 1.4–1.5 delivered login/OTP/session; 1.6–1.7 client shells; 1.8 RBAC policies. **1.9 completes FR-1 self-service recovery** so field and supervisor users can regain access without admin intervention.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `AuthController.cs` | login, verify-otp, refresh, logout, me | Add forgot-password, reset-password |
| `AuthService.cs` | Login, verify, refresh, logout, me | Add forgot/reset; reuse `IUserSessionService` |
| `AuditEventTypes.cs` | login/logout/refresh events | Add password reset events |
| `AuthServiceCollectionExtensions.cs` | auth-login/verify/refresh/logout rate limits | Add forgot/reset rate limit policies |
| `apps/web` login | No forgot link | forgot + reset routes/components |
| `apps/mobile` LoginScreen | No forgot link | ForgotPassword + ResetPassword screens |
| `packages/api-client` | auth routes through `/me` | Add forgot/reset types |

**Do not break:**
- 40 .NET tests (1 unit + 39 integration)
- 15 web + 15 mobile tests
- Deactivated 403 on login unchanged
- `IUserSessionService.InvalidateUserSessionsAsync` contract (Story 1.5)
- RBAC policies (Story 1.8) — reset endpoints are `[AllowAnonymous]` auth surface

### API contracts

**Forgot password** `POST /api/v1/auth/forgot-password`

```json
// Request
{ "email": "user@pilot.example" }

// Response 200 (always for valid JSON body)
{ "data": { "message": "If an account exists for that email, we sent reset instructions." }, "meta": { "requestId": "..." } }
```

**Reset password** `POST /api/v1/auth/reset-password`

```json
// Request
{ "token": "<opaque-token-from-email-link>", "newPassword": "newPassword123!" }

// Response 200
{ "data": { "message": "Password updated. Sign in with your new password." }, "meta": { ... } }
```

| Status | When |
|--------|------|
| 400 | Expired or already-used token; `newPassword` shorter than 8 characters |
| 401 | Unknown/malformed token (never issued) |
| 403 | Not used on forgot (enumeration-safe); deactivated handled silently on forgot |
| 429 | Rate limit exceeded |

**Password policy (v1):** minimum **8** characters — align with web `Validators.minLength(8)` and mobile login validation. No complexity rules in pilot.

**Email link format:** `{PasswordReset:WebResetUrl}?token={opaqueToken}`  
Example: `http://localhost:4200/reset-password?token=abc123…`

**Token storage:** Redis key `password-reset:token:{hash}`; revoked marker `password-reset:revoked:{hash}`; per-user active index `password-reset:user:{userId}`. Value JSON `{ userId }`. TTL from `ExpiryMinutes`.

**Reset execution order (mandatory):**

1. Validate `newPassword` length ≥ 8 → else **400** (do not burn token)
2. `TryPeekAsync(token)` → missing **401**; revoked/consumed **400**
3. Update `users.password_hash` + `SaveChanges`
4. `InvalidateUserSessionsAsync(userId)` — revokes refresh tokens, bumps `token_version`, writes `auth.session.invalidated`
5. `TryConsumeAsync(token)` — delete active key; set revoked marker
6. `auditService.RecordAsync(auth.password_reset.completed)`

**Forgot-password SMTP failures:** Log server-side; still return **200** generic message (never 503 — would leak account existence).

### Security requirements (mandatory)

1. **No enumeration** — forgot-password always returns same 200 message + envelope shape
2. **Deactivated users** — no token issued, no email sent, still 200 generic (do not reveal deactivation)
3. **Single-use tokens** — consume on successful reset; second attempt → 400
4. **Session kill** — `InvalidateUserSessionsAsync` after password change (forces OTP re-login)
5. **Rate limiting** — separate policies `auth-forgot-password`, `auth-reset-password` (same partition pattern as login)
6. **Never log raw tokens** — store hash only; email contains opaque token once
7. **Web routing** — `/reset-password` must **not** use `guestGuard` (authenticated users with stale sessions clicking email links would be redirected to `/home`)
8. **Forgot empty email** — return same 200 generic (no 400 validation leak)

### UX copy (EXPERIENCE.md / UX-DR8)

| Context | String |
|---------|--------|
| Login forgot link | "Forgot password?" |
| Forgot helper | "Enter the email for your account. We'll send reset instructions if it exists." |
| Forgot success | "If an account exists for that email, we sent reset instructions." |
| Reset helper | "Choose a new password (at least 8 characters)." |
| Reset confirm label | "Confirm new password" |
| Password mismatch | "Passwords do not match." |
| Reset success | "Password updated. Sign in with your new password." |
| Invalid/expired token | Use API `detail` in `aria-live` region |

### Suggested file structure

```
apps/api/
├── Infrastructure/Auth/
│   ├── PasswordResetTokenStore.cs       # NEW
│   ├── AuthService.cs                   # UPDATE
│   └── AuthOptions.cs                   # UPDATE — PasswordResetOptions
├── Infrastructure/Audit/AuditEventTypes.cs  # UPDATE
├── Controllers/V1/AuthController.cs     # UPDATE
├── Models/Auth/AuthDtos.cs              # UPDATE
tests/api.integration/
└── PasswordResetTests.cs              # NEW

apps/web/src/app/
├── core/auth/auth-session.service.ts    # UPDATE
├── core/auth/auth.models.ts             # UPDATE
├── features/auth/
│   ├── forgot-password/                 # NEW
│   └── reset-password/                  # NEW
└── app.routes.ts                        # UPDATE

apps/mobile/src/
├── services/api/apiClient.ts              # UPDATE — PUBLIC_AUTH_PATHS
├── services/auth/AuthSessionService.ts    # UPDATE
├── services/auth/auth.models.ts           # UPDATE
├── screens/auth/
│   ├── ForgotPasswordScreen.tsx         # NEW
│   └── ResetPasswordScreen.tsx          # NEW
├── navigation/AuthNavigator.tsx         # UPDATE
├── navigation/types.ts                  # UPDATE
└── screens/auth/LoginScreen.tsx         # UPDATE — forgot link
```

### Previous story intelligence (1.5–1.8)

- **Session invalidation:** Reuse `IUserSessionService.InvalidateUserSessionsAsync` — do not manually bump `token_version` without revoking refresh tokens
- **Redis stores:** Use `IConnectionMultiplexer` directly (not `IDistributedCache`) — Story 1.4/1.5 lesson
- **Email testing:** `FakeEmailSender` captures messages; extract reset link/token in integration tests via regex
- **Web HTTP:** `AuthSessionService` + envelope unwrap pattern; guest routes use no auth interceptors on anonymous endpoints
- **Mobile HTTP:** Extend `PUBLIC_AUTH_PATHS` in `apiClient.ts` for forgot/reset — **mandatory task**, not optional note
- **Audit:** Successful reset produces **`auth.password_reset.completed`** plus **`auth.session.invalidated`** from `InvalidateUserSessionsAsync` — both expected
- **Email testing:** Extract token from email body with regex: `token=([^&\s]+)` or similar

### Scope boundaries

| In scope (1.9) | Out of scope |
|----------------|--------------|
| Self-service forgot + reset API | Admin force-reset (Story 9.2) |
| Web + mobile forgot/reset UI | Password complexity beyond min 8 |
| Redis single-use tokens | SMS/WhatsApp reset channel |
| OpenAPI + api-client update | Change password while logged in |
| Rate limits + audit | Epic 2+ features |

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | 40 existing + new password reset tests pass |
| Web | `npm run test:web` | 15 existing + new component/service tests pass |
| Mobile | `npm run test:mobile` | 15 existing + new screen tests pass |

### Definition of Done

- [x] Forgot + reset API with enumeration-safe forgot response
- [x] Redis token store single-use + expiry
- [x] Session invalidation on reset success
- [x] Web forgot/reset flows with aria-live
- [x] Mobile forgot/reset flows with accessible errors
- [x] Integration + client tests pass
- [x] api-client regenerated
- [x] README updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.9, FR-1]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.2 Auth, email OTP pattern]
- [Source: `_bmad-output/specs/spec-kaval-online/roles-and-access.md` — password reset via email link]
- [Source: `_bmad-output/project-context.md` — api-client-only HTTP, audit on mutations]
- [Source: `_bmad-output/implementation-artifacts/1-5-refresh-logout-and-forced-session-invalidation.md` — `InvalidateUserSessionsAsync`, audit_events]
- [Source: `_bmad-output/implementation-artifacts/1-6-angular-pwa-shell-with-login-and-otp-flow.md` — login/OTP UX patterns]
- [Source: `_bmad-output/implementation-artifacts/1-7-react-native-shell-with-login-and-otp-flow.md` — mobile auth shell patterns]
- [Source: `_bmad-output/implementation-artifacts/1-8-rbac-policies-on-protected-endpoints.md` — auth surface AllowAnonymous pattern]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Implemented `PasswordResetTokenStore` (Redis SHA-256, peek/consume, revoked marker) mirroring refresh token patterns.
- `ForgotPasswordAsync` always returns generic 200; SMTP failures logged server-side without enumeration leak.
- `ResetPasswordAsync` follows mandatory order: validate password → peek → hash update → `InvalidateUserSessionsAsync` → consume → audit.
- Web `/forgot-password` and `/reset-password` routes omit `guestGuard`; login shows reset success via query param.
- Mobile `PUBLIC_AUTH_PATHS` extended; Forgot/Reset screens with accessible error regions.
- Tests: 52 .NET (1 unit + 51 integration), 19 web, 17 mobile — all passing.
- Code review (2026-06-15): enumeration-safe forgot on infrastructure/email failures, dynamic expiry copy, null-body guards, mobile success a11y.

### Code Review Findings (2026-06-15)

| Severity | Finding | Resolution |
|----------|---------|------------|
| HIGH | Redis/token issuance failure could return 500 on forgot-password, leaking account existence vs generic 200 | Broadened catch to all non-cancellation exceptions; always return generic 200 |
| MEDIUM | Email body hardcoded "one hour" while `PasswordReset:ExpiryMinutes` is configurable | Email copy uses `{ExpiryMinutes} minutes` from options |
| MEDIUM | SMTP failure on forgot-password untested — regression risk for enumeration policy | `ForgotPassword_EmailFailure_ReturnsGeneric200` + `FakeEmailSender.FailNextSend` |
| MEDIUM | Null JSON body on forgot/reset could throw or behave inconsistently | Nullable request DTOs; null forgot → generic 200; null reset → 400 |
| LOW | Mobile forgot-password success used `AccessibleErrorRegion` (red alert styling) | Success text with `accessibilityLiveRegion="polite"` and green styling |
| LOW | Unused `using System.Text` in `PasswordResetTokenStore` | Removed |

### File List

- apps/api/Infrastructure/Auth/AuthOptions.cs
- apps/api/Infrastructure/Auth/PasswordResetTokenStore.cs
- apps/api/Infrastructure/Auth/AuthService.cs
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Infrastructure/Email/FakeEmailSender.cs
- apps/api/Controllers/V1/AuthController.cs
- apps/api/Models/Auth/AuthDtos.cs
- tests/api.integration/PasswordResetTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- apps/web/src/app/core/auth/auth.models.ts
- apps/web/src/app/core/auth/auth-session.service.ts
- apps/web/src/app/core/auth/auth-session.service.spec.ts
- apps/web/src/app/features/auth/forgot-password/*
- apps/web/src/app/features/auth/reset-password/*
- apps/web/src/app/features/auth/login/login.component.*
- apps/web/src/app/app.routes.ts
- apps/mobile/src/services/auth/auth.models.ts
- apps/mobile/src/services/auth/AuthSessionService.ts
- apps/mobile/src/services/api/apiClient.ts
- apps/mobile/src/screens/auth/ForgotPasswordScreen.tsx
- apps/mobile/src/screens/auth/ResetPasswordScreen.tsx
- apps/mobile/src/screens/auth/LoginScreen.tsx
- apps/mobile/src/navigation/AuthNavigator.tsx
- apps/mobile/src/navigation/types.ts
- apps/mobile/src/context/AuthContext.tsx
- apps/mobile/__tests__/ForgotPasswordScreen.test.tsx
- apps/mobile/__tests__/ResetPasswordScreen.test.tsx
- README.md
- infra/.env.example

### Change Log

- 2026-06-15: Story 1.9 created — self-service password reset API + web/mobile UI.
- 2026-06-15: Validation pass — reset order, enumeration/SMTP, guestGuard, token semantics, audit events.
- 2026-06-15: Implementation complete — API, clients, tests, docs; status → review.
- 2026-06-15: Code review patches applied — 52 .NET tests, status done. Epic 1 stories complete.
