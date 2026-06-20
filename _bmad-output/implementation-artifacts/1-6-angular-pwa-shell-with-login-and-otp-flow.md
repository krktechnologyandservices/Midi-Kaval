---
baseline_commit: NO_VCS
---

# Story 1.6: Angular PWA Shell with Login and OTP Flow

Status: done

<!-- Validated: inline at create-story 2026-06-14 -->

## Story

As a **Project Coordinator or Director**,
I want to log in on the web app with OTP,
so that I can access supervisor tools (FR-1, UX-DR8, UX-DR14).

## Acceptance Criteria

1. **Given** the Angular PWA loads at `http://localhost:4200`  
   **When** I open the app unauthenticated  
   **Then** I am routed to a **login screen** with labelled email and password fields (Angular Material)  
   **And** form validation shows accessible errors via an `aria-live="polite"` region (UX-DR8)

2. **Given** valid supervisor credentials (seeded Director)  
   **When** I submit login  
   **Then** the app calls `POST /api/v1/auth/login` and navigates to an **OTP step** showing `challengeId` expiry context  
   **And** OTP input is accessible (single field or 6-box group with visible label)  
   **And** copy uses EXPERIENCE tone: "Enter the 6-digit code from your email."  
   **And** wrong password / unknown email shows generic error (no enumeration); deactivated account shows **"Contact your coordinator"**

3. **Given** a valid OTP  
   **When** I submit verify-otp  
   **Then** the app calls `POST /api/v1/auth/verify-otp` with `withCredentials: true` so the httpOnly `refresh_token` cookie is stored  
   **And** the access token is held in memory (or `sessionStorage`) for `Authorization: Bearer` on API calls  
   **And** **Director** or **Coordinator** users route to a **supervisor home placeholder** (e.g. `/home`) with role shown  
   **And** **SocialWorker** or **CaseWorker** users see **"Use the mobile app for your role"** and cannot access supervisor routes (UX-DR14)

4. **Given** an authenticated supervisor session  
   **When** the access token expires  
   **Then** the auth interceptor calls `POST /api/v1/auth/refresh` with **cookie only** (no `refreshToken` body) and `withCredentials: true`  
   **And** on success the new access token is stored and the original request retries  
   **And** on refresh failure the user is routed to a **session expired** screen with CTA back to login (UX-DR14)

5. **Given** the user logs out from the web shell  
   **When** logout is triggered  
   **Then** the app calls `POST /api/v1/auth/logout` with credentials, clears local access token, and routes to login

6. **Given** Story 1.1 PWA scaffold  
   **When** I build the web app for production  
   **Then** `@angular/service-worker` remains registered (`registerWhenStable:30000`, disabled in dev)  
   **And** `ngsw-config.json` keeps **app-shell prefetch only** — no API `dataGroups` in this story

7. **Given** the web app calls the API from `localhost:4200`  
   **When** Development API runs on `localhost:5049`  
   **Then** API **CORS** allows the web origin with credentials (`Access-Control-Allow-Credentials: true`)  
   **And** cookie `Set-Cookie` from verify-otp/refresh is accepted by the browser

8. **Given** Stories 1.2–1.5 baseline  
   **When** I run `dotnet test Midi-Kaval.slnx` and `npm run test:web`  
   **Then** all existing tests pass  
   **And** new Angular unit tests cover auth service (login/verify/refresh/logout), role guard, and session-expired routing

## Tasks / Subtasks

- [x] **API CORS for web origin** (AC: 7)
  - [x] Add `Cors:AllowedOrigins` config (default `http://localhost:4200` in Development)
  - [x] Register CORS policy with `AllowCredentials()` — **only when `!Testing`** (mirror auth pattern)
  - [x] `UseCors()` before auth middleware in `Program.cs`
  - [x] Document in README + `infra/.env.example` (`Cors__AllowedOrigins__0=http://localhost:4200`)

- [x] **Wire api-client + HttpClient** (AC: 2–5)
  - [x] Add `@midi-kaval/api-client` to `apps/web/package.json`
  - [x] `provideHttpClient(withFetch(), withInterceptors([authInterceptor, errorInterceptor]))` in `app.config.ts`
  - [x] `environment.ts` / `environment.development.ts` — `apiBaseUrl` (default `http://localhost:5049`)
  - [x] Typed requests using `components['schemas']` from `@midi-kaval/api-client`; unwrap `{ data, meta }` envelope manually in auth service

- [x] **Core auth layer** (`src/app/core/auth/`) (AC: 2–5)
  - [x] `AuthSessionService` (signals) — access token, user profile (`/auth/me`), login challenge state
  - [x] `auth.interceptor.ts` — attach Bearer; on 401 attempt refresh once; queue/retry or redirect session-expired
  - [x] `error.interceptor.ts` — map Problem Details `detail` to user-facing messages
  - [x] `auth.guard.ts` — requires authenticated session
  - [x] `supervisor.guard.ts` — allows `Director` | `Coordinator` only
  - [x] **Web refresh contract:** always `withCredentials: true`; **never** send `refreshToken` in JSON body (API body-first precedence would bypass cookie — Story 1.5 CR patch)

- [x] **Auth feature UI** (`src/app/features/auth/`) (AC: 1–3, UX-DR8)
  - [x] `login.component` — email/password form, Material fields, `aria-live` error region
  - [x] `otp.component` — 6-digit input, resend hint after 60s (UI-only timer; resend = re-login for v1)
  - [x] `session-expired.component` — forced logout copy per UX-DR14
  - [x] `mobile-only.component` — "Use the mobile app for your role" for SocialWorker/CaseWorker

- [x] **Supervisor home placeholder** (`src/app/features/home/`) (AC: 3)
  - [x] Minimal shell: toolbar + "Supervisor home — Crisis Queue in Epic 8" placeholder
  - [x] Display user email/role from session

- [x] **Routing** (`app.routes.ts`) (AC: 3–4)
  - [x] `/login`, `/login/otp`, `/session-expired`, `/mobile-only`, `/home` (supervisor guard)
  - [x] Default redirect: authenticated supervisor → `/home`; unauthenticated → `/login`
  - [x] Replace `AppComponent` placeholder copy with `<router-outlet />` shell + Material toolbar on authenticated routes

- [x] **PWA verification** (AC: 6)
  - [x] Confirm `provideServiceWorker` unchanged; `ngsw-config.json` app-shell prefetch only
  - [x] Do **not** add API caching dataGroups (Story 8.7 / architecture §5.4)

- [x] **Tests** (AC: 8)
  - [x] `AuthSessionService` specs — mock HttpClient; login → challenge; verify → token; refresh; logout
  - [x] `supervisor.guard` spec — allows Director, blocks SocialWorker
  - [x] Component smoke tests for login + OTP with `aria-live` region present
  - [x] Keep existing `app.component.spec.ts` green (update if shell text changes)

- [x] **Documentation** (AC: 7–8)
  - [x] README web section: start API + `npm run start:web`, CORS note, auth flow summary
  - [x] Root `package.json` / web README: build api-client before web if types stale

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.6 is the **first client surface** consuming Stories 1.4–1.5 auth APIs. Story 1.7 mirrors login on React Native. Story 1.8 adds RBAC policies on **business** endpoints — do not add API role policies here.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `apps/web` | Angular 19 shell; empty routes; no HttpClient; no api-client dep | Full auth feature + core interceptors/guards |
| `apps/api/Program.cs` | No CORS | Add credentialed CORS for web origin |
| `packages/api-client` | Auth routes typed (`login`, `verify-otp`, `refresh`, `logout`, `me`) | Consume from web auth service |
| `packages/shared-types` | `AppRole` enum wired in shell | Use for role routing/guards |
| PWA | `ngsw-config.json` app-shell prefetch; SW off in dev | Verify only — no expansion |

**Do not break:** Story 1.2 `TestingWebApplicationFactory` (no CORS/auth in Testing env). CORS registration must be inside `!IsTesting()` block or use permissive Testing policy that does not require Redis.

### API contracts (web-specific)

**Login** `POST /api/v1/auth/login` → `{ data: { challengeId, expiresInSeconds } }`

**Verify** `POST /api/v1/auth/verify-otp` → `{ data: { accessToken, expiresIn, refreshToken, user } }` + `Set-Cookie: refresh_token` (httpOnly, path `/api/v1/auth`)

**Refresh** `POST /api/v1/auth/refresh` — **cookie only** on web; response `{ data: { accessToken, expiresIn, refreshToken } }` + rotated cookie

**Logout** `POST /api/v1/auth/logout` — cookie; **204** no body

**Me** `GET /api/v1/auth/me` — Bearer; `{ data: { id, email, role } }`

**Errors:** RFC 7807 `application/problem+json`; read `detail` field.  
**403 inactive:** "Contact your coordinator" on login, verify, refresh, and authenticated `/me`.

### Role routing matrix (AC: 3, UX-DR14)

| Role | After OTP success |
|------|-------------------|
| `Director` | `/home` supervisor placeholder |
| `Coordinator` | `/home` supervisor placeholder |
| `SocialWorker` | `/mobile-only` (no supervisor routes) |
| `CaseWorker` | `/mobile-only` |

Use `AppRole` from `@midi-kaval/shared-types` — values match JWT `role` claim (`Director`, `Coordinator`, etc.).

### UX requirements (mandatory copy)

| Context | String |
|---------|--------|
| OTP helper | "Enter the 6-digit code from your email." |
| Deactivated | "Contact your coordinator" |
| Mobile-only | "Use the mobile app for your role" |
| OTP timeout (aria-live) | "Code expired — request new code" (show when `expiresInSeconds` elapsed; v1 resend = navigate back to login) |
| Session expired | Clear heading + button "Sign in again" → `/login` |

[Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 7, State Patterns, UX-DR8/14]

### Suggested Angular structure

```
apps/web/src/app/
├── core/
│   └── auth/
│       ├── auth-session.service.ts
│       ├── auth.interceptor.ts
│       ├── error.interceptor.ts
│       ├── auth.guard.ts
│       └── supervisor.guard.ts
├── features/
│   ├── auth/
│   │   ├── login/
│   │   ├── otp/
│   │   ├── session-expired/
│   │   └── mobile-only/
│   └── home/
│       └── supervisor-home.component.ts
├── environments/
│   ├── environment.ts
│   └── environment.development.ts
├── app.config.ts          # + HttpClient, interceptors
├── app.routes.ts          # guarded routes
└── app.component.ts       # shell toolbar + outlet
```

### Technical requirements

| Item | Requirement |
|------|-------------|
| Angular | 19 standalone components; signals for session state |
| HTTP | `@midi-kaval/api-client` types only; `HttpClient` with `withCredentials: true` on all `/api/v1/auth/*` calls |
| Access token | Memory or `sessionStorage` — **not** localStorage (XSS surface) |
| Refresh token | httpOnly cookie only — never read/document in TS |
| Material | `MatFormField`, `MatInput`, `MatButton`; existing M3 theme in `styles.scss` (full `#0D6E6E` token swap is Story 9.4) |
| Envelope | Unwrap `response.data` / handle `meta.requestId` optionally in logs |

### Architecture compliance

- Web auth in `core/`; features in `features/auth` [Source: architecture §5.4, §7]
- PWA app-shell prefetch only [Source: architecture §5.4, AC6]
- CORS allowlist for web origin [Source: architecture §5.2 — deferred from 1.4 validation]
- No RBAC policies on case endpoints [Source: Story 1.8 boundary]
- No offline visit capture on web [Source: architecture §5.4]

### Library / framework requirements

**Web (`apps/web`)** — add dependency:
```json
"@midi-kaval/api-client": "*"
```

**API (`apps/api`)** — no new NuGet; built-in `Microsoft.AspNetCore.Cors`.

**Build order:**
```bash
npm run build -w @midi-kaval/shared-types
npm run build -w @midi-kaval/api-client   # if stale
npm run start:web
```

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| AuthSessionService login/verify | `auth-session.service.spec.ts` | Challenge stored; token set on verify |
| Refresh on 401 | interceptor spec or service | Mock 401 → refresh → retry |
| Supervisor guard | `supervisor.guard.spec.ts` | Director OK; SocialWorker blocked |
| Login aria-live | `login.component.spec.ts` | `[aria-live="polite"]` present |
| Regression | `app.component.spec.ts` | Shell still renders |
| API regression | `dotnet test` | All 30 tests still pass |

**Manual smoke:** API on :5049 + web on :4200; login as `director@pilot.example` (seed password from env); verify OTP from dev SMTP or API logs; confirm cookie in DevTools → Application → Cookies.

### Previous story intelligence (1.5)

- Auth API complete: refresh rotation, logout 204, `/auth/me`, `token_version` enforcement, audit events
- **Web must not send `refreshToken` in body** — `AuthTokenHelpers` prefers body when present (1.5 CR); cookie-only refresh on web
- Refresh uses `withCredentials`; peek-then-consume on server — client should treat 401 after refresh as session expired
- `ActiveUserAuthorizationHandler` returns 403 for inactive JWT users — map to coordinator message in UI
- Integration tests: 29 total; `[Collection("AuthIntegration")]` serialized — do not break Testing env
- `FakeEmailSender` in API tests only — web dev needs real SMTP or read OTP from API log/email in Development

### Previous story intelligence (1.4)

- OTP email body already says "Enter the 6-digit code from your email." — reuse in UI helper text
- Login 403 deactivated before OTP — show message on login step, do not navigate to OTP
- Rate limit 429 on auth — show "Too many attempts" in aria-live region
- CORS explicitly deferred to **this story** [Source: `1-4-login-and-email-otp-api-validation-report.md`]

### Anti-patterns (do NOT do in this story)

- Do not hand-edit `packages/api-client/src/generated/*`
- Do not store refresh token in localStorage/sessionStorage/JS — cookie only
- Do not send `{ refreshToken }` body from web refresh/logout
- Do not use raw `fetch` — use `HttpClient` + api-client types
- Do not build Crisis Queue / case UI (Epic 2/8)
- Do not add RN mobile login (Story 1.7)
- Do not add `dataGroups` API caching to `ngsw-config.json`
- Do not add RBAC policies to API controllers (Story 1.8)
- Do not skip CORS `AllowCredentials` — cookies will silently fail

### Scope boundaries

| In scope (1.6) | Out of scope |
|----------------|--------------|
| Web login + OTP + session refresh/logout | RN login (1.7) |
| CORS for local web dev | Production multi-origin deploy hardening |
| Supervisor home **placeholder** | Crisis Queue UI (8.2) |
| Session expired screen | Password reset UI (1.9) |
| Role-based route gate (supervisor vs mobile-only) | Server RBAC on business APIs (1.8) |

### Definition of Done

- [x] Login → OTP → supervisor home works against local API with seed Director
- [x] SocialWorker seed user (if created in test) sees mobile-only message
- [x] Session refresh via cookie; session-expired screen on hard failure
- [x] CORS + credentials working on localhost
- [x] PWA service worker unchanged (shell prefetch only)
- [x] `npm run test:web` and `dotnet test Midi-Kaval.slnx` pass
- [x] README updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.6, UX-DR8, UX-DR14]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.2 Auth, §5.4 Web PWA]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 7, OTP, session expired]
- [Source: `_bmad-output/project-context.md` — Angular rules, api-client-only HTTP]
- [Source: `_bmad-output/implementation-artifacts/1-4-login-and-email-otp-api.md` — login/verify contracts]
- [Source: `_bmad-output/implementation-artifacts/1-5-refresh-logout-and-forced-session-invalidation.md` — refresh/logout/me, cookie contract]
- [Source: `_bmad-output/implementation-artifacts/1-4-login-and-email-otp-api-validation-report.md` — CORS deferral]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Implemented credentialed CORS for `http://localhost:4200` (skipped in Testing env).
- Angular auth shell: login → OTP → role-based routing (`/home` or `/mobile-only`), cookie-only refresh/logout, 401 interceptor with session-expired fallback.
- Code review patches: single-retry interceptor, APP_INITIALIZER session bootstrap, OTP challenge persistence, deactivated 403 handling.
- 15 Angular unit tests + 30 .NET tests passing.

### File List

- apps/api/Infrastructure/CorsOptions.cs
- apps/api/Infrastructure/CorsServiceCollectionExtensions.cs
- apps/api/Program.cs
- apps/api/appsettings.Development.json
- apps/web/angular.json
- apps/web/package.json
- apps/web/src/environments/environment.ts
- apps/web/src/environments/environment.development.ts
- apps/web/src/app/app.config.ts
- apps/web/src/app/app.routes.ts
- apps/web/src/app/app.component.ts
- apps/web/src/app/app.component.html
- apps/web/src/app/app.component.scss
- apps/web/src/app/app.component.spec.ts
- apps/web/src/app/core/auth/auth.models.ts
- apps/web/src/app/core/auth/auth-session.service.ts
- apps/web/src/app/core/auth/auth-session.service.spec.ts
- apps/web/src/app/core/auth/auth-http.context.ts
- apps/web/src/app/core/auth/auth.interceptor.ts
- apps/web/src/app/core/auth/auth.interceptor.spec.ts
- apps/web/src/app/core/auth/error.interceptor.ts
- apps/web/src/app/core/auth/auth.guard.ts
- apps/web/src/app/core/auth/supervisor.guard.ts
- apps/web/src/app/core/auth/supervisor.guard.spec.ts
- apps/web/src/app/features/auth/login/login.component.ts
- apps/web/src/app/features/auth/login/login.component.html
- apps/web/src/app/features/auth/login/login.component.scss
- apps/web/src/app/features/auth/login/login.component.spec.ts
- apps/web/src/app/features/auth/otp/otp.component.ts
- apps/web/src/app/features/auth/otp/otp.component.html
- apps/web/src/app/features/auth/otp/otp.component.scss
- apps/web/src/app/features/auth/otp/otp.component.spec.ts
- apps/web/src/app/features/auth/session-expired/session-expired.component.ts
- apps/web/src/app/features/auth/session-expired/session-expired.component.html
- apps/web/src/app/features/auth/session-expired/session-expired.component.scss
- apps/web/src/app/features/auth/mobile-only/mobile-only.component.ts
- apps/web/src/app/features/auth/mobile-only/mobile-only.component.html
- apps/web/src/app/features/auth/mobile-only/mobile-only.component.scss
- apps/web/src/app/features/home/supervisor-home.component.ts
- apps/web/src/app/features/home/supervisor-home.component.html
- apps/web/src/app/features/home/supervisor-home.component.scss
- infra/.env.example
- README.md

### Change Log

- 2026-06-14: Story 1.6 created — Angular PWA login/OTP shell consuming auth API.
- 2026-06-14: Story 1.6 implemented — CORS, Angular auth shell, tests, docs.
- 2026-06-14: Code review — 9 patches applied (interceptor retry fix, session bootstrap, OTP persistence, CORS hardening).
