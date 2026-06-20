---
baseline_commit: NO_VCS
---

# Story 1.7: React Native Shell with Login and OTP Flow

Status: done

<!-- Validated: inline at create-story 2026-06-14 -->

## Story

As a **Social Worker or Case Worker**,
I want to log in on mobile with OTP,
so that I can access field tools (FR-1, UX-DR8).

## Acceptance Criteria

1. **Given** the React Native app launches unauthenticated  
   **When** I open the app  
   **Then** I see a **login screen** with labelled email and password fields  
   **And** validation errors are announced via an accessible live region (`accessibilityLiveRegion="polite"` on error container) (UX-DR8)

2. **Given** valid field-worker credentials  
   **When** I submit login  
   **Then** the app calls `POST /api/v1/auth/login` and navigates to an **OTP screen** with expiry countdown  
   **And** helper copy reads **"Enter the 6-digit code from your email."**  
   **And** OTP input is accessible (single `TextInput` with `keyboardType="number-pad"` and visible label, or 6-box group with `accessibilityLabel`)  
   **And** wrong password shows generic **"Invalid email or password."**; deactivated account shows **"Contact your coordinator"** (no enumeration)

3. **Given** a valid OTP for a **SocialWorker** or **CaseWorker**  
   **When** I submit verify-otp  
   **Then** the app calls `POST /api/v1/auth/verify-otp` and stores **`accessToken` and `refreshToken` in secure storage** (not AsyncStorage)  
   **And** **Director** or **Coordinator** users see **"Use the web app for your role"** and cannot access field tabs (UX-DR14 inverse of web Story 1.6)  
   **And** field roles land on the **Today tab** inside bottom tabs **Today · Cases · More** (UX-DR10)

4. **Given** an authenticated field session  
   **When** the access token expires on an API call  
   **Then** the API client calls `POST /api/v1/auth/refresh` with **`{ refreshToken }` in JSON body** (mobile contract — not cookie)  
   **And** on success new tokens are stored in secure storage and the original request retries once  
   **And** on refresh failure the user is routed to a **session expired** screen with CTA **"Sign in again"** → login (UX-DR14)

5. **Given** the user logs out from the More tab (or shell logout action)  
   **When** logout is triggered  
   **Then** the app calls `POST /api/v1/auth/logout` with `{ refreshToken }` body, clears secure storage, and routes to login

6. **Given** Story 1.1 mobile scaffold  
   **When** I run `npm run test:mobile`  
   **Then** existing tests pass  
   **And** new tests cover `AuthSessionService` (login/verify/refresh/logout), field-role routing, and login/OTP accessible error regions

7. **Given** Stories 1.2–1.6 baseline  
   **When** I run `dotnet test Midi-Kaval.slnx` and `npm run test:mobile`  
   **Then** all existing tests pass  
   **And** no API changes are required (auth API complete from 1.4–1.5; CORS from 1.6 is web-only)

## Tasks / Subtasks

- [x] **Dependencies + monorepo wiring** (AC: 2–5)
  - [x] Add `@midi-kaval/api-client` to `apps/mobile/package.json`
  - [x] Add React Navigation: `@react-navigation/native`, `@react-navigation/native-stack`, `@react-navigation/bottom-tabs`, `react-native-screens`, `react-native-safe-area-context`, `react-native-gesture-handler`
  - [x] Add secure storage: `react-native-keychain` (store access + refresh tokens)
  - [x] Update `metro.config.js` + `tsconfig.json` aliases for `@midi-kaval/api-client` (mirror shared-types pattern)
  - [x] `import 'react-native-gesture-handler'` at top of `index.js`

- [x] **Environment + API client** (AC: 2–5)
  - [x] `src/config/environment.ts` — `apiBaseUrl` default `http://localhost:5049` (document Android emulator `http://10.0.2.2:5049`, iOS simulator `localhost`)
  - [x] `src/services/api/apiClient.ts` — fetch wrapper: unwrap `{ data, meta }`, attach Bearer, 401 → refresh once → retry, map Problem Details `detail`
  - [x] Typed models in `src/services/auth/auth.models.ts` from `@midi-kaval/api-client` `components['schemas']` (mirror web `auth.models.ts`)

- [x] **Auth session service** (`src/services/auth/`) (AC: 2–5)
  - [x] `secureStorage.ts` — read/write/clear access + refresh tokens via Keychain
  - [x] `AuthSessionService.ts` — login challenge state, verifyOtp, refreshSession, logout, loadCurrentUser, bootstrap on app start
  - [x] **Mobile refresh contract:** always send `{ refreshToken }` in JSON body on refresh/logout — **never rely on cookies** (opposite of web Story 1.6)
  - [x] Persist OTP challenge in memory + optional AsyncStorage (non-secret) for OTP screen survive; tokens **only** in Keychain
  - [x] `extractErrorMessage()` — use API `detail`; generic 401 login message; 429 rate limit message
  - [x] Role routing: `isFieldRole` (SocialWorker | CaseWorker), `isSupervisorRole` (Director | Coordinator)

- [x] **Navigation** (`src/navigation/`) (AC: 3–4)
  - [x] `AuthNavigator` — Login → OTP → SessionExpired
  - [x] `MainTabNavigator` — Today | Cases | More placeholders (UX-DR10)
  - [x] `RootNavigator` — auth gate: unauthenticated → AuthNavigator; authenticated field role → tabs; authenticated supervisor → WebOnlyScreen

- [x] **Auth screens** (`src/screens/auth/`) (AC: 1–3, UX-DR8)
  - [x] `LoginScreen.tsx` — email/password, accessible error region
  - [x] `OtpScreen.tsx` — 6-digit input, expiry timer, aria-live timeout **"Code expired — request new code"**, resend = navigate back to login (v1)
  - [x] `SessionExpiredScreen.tsx` — forced logout copy + **"Sign in again"** button
  - [x] `WebOnlyScreen.tsx` — **"Use the web app for your role"** for Director/Coordinator

- [x] **Tab placeholder screens** (`src/screens/`) (AC: 3)
  - [x] `TodayScreen.tsx` — "Today — Command Strip in Epic 3" placeholder; show user email/role
  - [x] `CasesScreen.tsx` — "Cases — Epic 2" placeholder
  - [x] `MoreScreen.tsx` — profile summary + Log out button

- [x] **App shell** (AC: 3)
  - [x] Replace `App.tsx` placeholder with `NavigationContainer` + auth context/provider + `RootNavigator`
  - [x] Remove or replace `src/screens/Placeholder.ts` and `src/components/Placeholder.ts`

- [x] **Tests** (AC: 6–7)
  - [x] `__tests__/AuthSessionService.test.ts` — mock fetch + secureStorage; login → challenge; verify → tokens; refresh body contract; logout clears storage
  - [x] `__tests__/LoginScreen.test.tsx` — accessible error region present
  - [x] `__tests__/OtpScreen.test.tsx` — accessible error region present
  - [x] `__tests__/RootNavigator.test.tsx` — field role → tabs; supervisor → web-only
  - [x] Update `__tests__/App.test.tsx` for navigation shell

- [x] **Documentation** (AC: 7)
  - [x] README mobile section: Metro start, API URL notes for emulator/simulator, auth flow, secure storage, refresh-in-body contract
  - [x] Build order: `npm run build -w @midi-kaval/api-client` before mobile if types stale

## Dev Notes

### Epic context

**Epic 1: Platform Bootstrap & Secure Access** — Story 1.7 is the **mobile client mirror** of Story 1.6. Story 1.8 adds RBAC on business endpoints — do not add API policies here. Story 1.9 adds password reset UI on both clients.

### Brownfield state — READ BEFORE CODING

| Area | Current state | This story changes |
|------|---------------|-------------------|
| `apps/mobile` | RN 0.76.9 shell; fake text tabs in `App.tsx`; no navigation, auth, api-client | Full auth + tab navigation |
| `apps/mobile/package.json` | `react`, `react-native`, `@midi-kaval/shared-types` only | Navigation, keychain, api-client |
| `packages/api-client` | Auth routes typed | Consume from mobile services |
| `apps/web` Story 1.6 | Complete web auth (cookie refresh) | **Mirror patterns, opposite refresh contract** |
| `apps/api` | Auth + CORS for web | **No API changes** in this story |

**Do not break:** Story 1.2 `TestingWebApplicationFactory`; 30 .NET tests; 15 web tests.

### API contracts (mobile-specific)

**Login** `POST /api/v1/auth/login` → `{ data: { challengeId, expiresInSeconds } }`

**Verify** `POST /api/v1/auth/verify-otp` → `{ data: { accessToken, expiresIn, refreshToken, user } }`  
Mobile stores both tokens in Keychain (web uses httpOnly cookie for refresh).

**Refresh** `POST /api/v1/auth/refresh` — **body `{ refreshToken }` required** on mobile  
Response `{ data: { accessToken, expiresIn, refreshToken } }` — persist rotated refresh token.

**Logout** `POST /api/v1/auth/logout` — body `{ refreshToken }`; **204** no body

**Me** `GET /api/v1/auth/me` — Bearer; `{ data: { id, email, role } }`

**Errors:** RFC 7807; read `detail`. **403 inactive:** "Contact your coordinator".

**Critical (Story 1.5):** `AuthTokenHelpers` prefers **body `refreshToken` over cookie** when both present. Mobile must send body; web must not. Never mix contracts.

### Role routing matrix (AC: 3, UX-DR14)

| Role | After OTP success |
|------|-------------------|
| `SocialWorker` | Main tabs → Today (default) |
| `CaseWorker` | Main tabs → Today (default) |
| `Director` | WebOnlyScreen — "Use the web app for your role" |
| `Coordinator` | WebOnlyScreen |

Use `AppRole` from `@midi-kaval/shared-types` — values match JWT `role` claim.

### UX requirements (mandatory copy)

| Context | String |
|---------|--------|
| OTP helper | "Enter the 6-digit code from your email." |
| Deactivated | "Contact your coordinator" |
| Web-only (mobile) | "Use the web app for your role" |
| OTP timeout | "Code expired — request new code" |
| Session expired | Clear heading + button "Sign in again" → login |

[Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 7, UX-DR8/10/14]

### Suggested mobile structure

```
apps/mobile/src/
├── config/
│   └── environment.ts
├── services/
│   ├── api/
│   │   └── apiClient.ts
│   └── auth/
│       ├── auth.models.ts
│       ├── secureStorage.ts
│       └── AuthSessionService.ts
├── navigation/
│   ├── RootNavigator.tsx
│   ├── AuthNavigator.tsx
│   └── MainTabNavigator.tsx
├── screens/
│   ├── auth/
│   │   ├── LoginScreen.tsx
│   │   ├── OtpScreen.tsx
│   │   ├── SessionExpiredScreen.tsx
│   │   └── WebOnlyScreen.tsx
│   ├── today/TodayScreen.tsx
│   ├── cases/CasesScreen.tsx
│   └── more/MoreScreen.tsx
├── components/
│   └── AccessibleErrorRegion.tsx   # optional shared wrapper
└── App.tsx
```

### Technical requirements

| Item | Requirement |
|------|-------------|
| RN | 0.76.9 (existing); TypeScript strict |
| HTTP | `fetch` + `@midi-kaval/api-client` types only — no axios unless already in repo |
| Tokens | Keychain only for secrets; no `localStorage`/`AsyncStorage` for refresh token |
| Navigation | React Navigation 6.x stack + bottom tabs |
| Envelope | Unwrap `response.data` manually |
| Touch targets | ≥ 44pt per DESIGN.md |
| Tab labels | Today · Cases · More (exact UX-DR10) |

### Architecture compliance

- Mobile auth in `src/services/auth/`; screens in `src/screens/` [Source: architecture §7]
- Generated client only [Source: architecture §5.3, project-context]
- JWT + refresh in secure storage [Source: architecture §5.2]
- No offline sync / WatermelonDB in this story [Source: architecture §5.5 — Epic 3]
- No RBAC policies on API [Source: Story 1.8 boundary]
- No pull-to-refresh implementation on tabs yet (UX-DR10 — stub only)

### Library / framework requirements

**Add to `apps/mobile/package.json`:**
```json
"@midi-kaval/api-client": "*",
"@react-navigation/native": "^7.x",
"@react-navigation/native-stack": "^7.x",
"@react-navigation/bottom-tabs": "^7.x",
"react-native-screens": "^4.x",
"react-native-safe-area-context": "^5.x",
"react-native-gesture-handler": "^2.x",
"react-native-keychain": "^9.x"
```

**Build order:**
```bash
npm run build -w @midi-kaval/shared-types
npm run build -w @midi-kaval/api-client
npm run start:mobile
```

### Testing requirements

| Test | Location | Minimum coverage |
|------|----------|------------------|
| AuthSessionService | `__tests__/AuthSessionService.test.ts` | Challenge; verify stores tokens; refresh sends body; logout clears |
| Field role routing | `__tests__/RootNavigator.test.tsx` | SocialWorker → tabs; Director → web-only |
| Login a11y | `__tests__/LoginScreen.test.tsx` | `accessibilityLiveRegion` on error container |
| OTP a11y | `__tests__/OtpScreen.test.tsx` | Error region present |
| Regression | `__tests__/App.test.tsx` | Shell renders with navigation |
| API regression | `dotnet test` | All 30 tests still pass |

**Manual smoke (no field-worker seed yet):**
1. API on :5049 + Metro `npm run start:mobile`
2. Login `director@pilot.example` → OTP → verify **WebOnlyScreen** shows
3. For field-role flow: unit tests use mocked `SocialWorker`; optional DB role change for manual test only (do not commit)

### Previous story intelligence (1.6)

- Web auth complete: login → OTP → role routing, interceptor single-retry, session-expired, deactivated 403 handling
- **Web uses cookie-only refresh** — mobile must use **body refreshToken** (Story 1.5 CR)
- OTP challenge persisted in sessionStorage on web — mirror with non-secret storage on mobile
- `bootstrapSession` / APP_INITIALIZER validates restored session — mirror with app-start `loadCurrentUser()`
- Interceptor retry bug fixed: wrap retry in `catchError` for session-expired
- 15 web tests + 30 .NET tests baseline

### Previous story intelligence (1.5)

- Refresh rotation returns new `refreshToken` in envelope — mobile must persist rotated value
- Logout requires valid refresh token; 204 response
- `token_version` bump invalidates refresh — client treats as session expired
- Inactive user 403 on login, verify, refresh, `/me`

### Anti-patterns (do NOT do in this story)

- Do not hand-edit `packages/api-client/src/generated/*`
- Do not store refresh token in AsyncStorage or plain filesystem
- Do not use cookie/`withCredentials` on mobile (no browser cookie jar)
- Do not copy web's empty-body refresh — **must send `{ refreshToken }`**
- Do not implement Command Strip, cases list, or sync queue (Epics 2–3)
- Do not add API CORS/RBAC/password-reset endpoints
- Do not add `ios/`/`android/` native projects unless required for Keychain in your test env (document if skipped)

### Scope boundaries

| In scope (1.7) | Out of scope |
|----------------|--------------|
| Mobile login + OTP + session refresh/logout | Web login changes (1.6 done) |
| Bottom tabs placeholder (Today/Cases/More) | Command Strip UI (3.2) |
| Secure token storage | Offline DB / sync (Epic 3) |
| Web-only screen for supervisors | Field-worker seed user (optional manual DB) |
| Session expired screen | Password reset (1.9) |

### Definition of Done

- [x] Login → OTP → Today tab works for mocked/test SocialWorker role
- [x] Director seed login shows web-only message on mobile
- [x] Refresh via body `refreshToken`; session-expired on hard failure
- [x] Tokens in Keychain only
- [x] `npm run test:mobile` and `dotnet test Midi-Kaval.slnx` pass
- [x] README mobile auth section updated

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.7, UX-DR8, UX-DR10]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.2 Auth, §5.3 API, §7 mobile layout]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 7, Mobile IA, UX-DR8/10/14]
- [Source: `_bmad-output/project-context.md` — RN rules, api-client-only HTTP]
- [Source: `_bmad-output/implementation-artifacts/1-6-angular-pwa-shell-with-login-and-otp-flow.md` — web mirror + inverse refresh contract]
- [Source: `_bmad-output/implementation-artifacts/1-5-refresh-logout-and-forced-session-invalidation.md` — refresh/logout/me contracts]
- [Source: `_bmad-output/implementation-artifacts/1-6-angular-pwa-shell-with-login-and-otp-flow-validation-report.md` — interceptor retry learnings]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- React Native auth shell: login → OTP → role routing (field tabs vs web-only), Keychain token storage, body-based refresh/logout.
- React Navigation 6.x + bottom tabs Today/Cases/More placeholders.
- 10 mobile Jest tests + 30 .NET tests passing; no API changes.
- Code review (2026-06-14): Bearer omitted on refresh/logout; retried-401 session-expired; deactivated callback; OTP resume routing; timer fix; 15 mobile tests.

### Code Review Findings (2026-06-14)

| Severity | Finding | Resolution |
|----------|---------|------------|
| HIGH | Refresh/logout sent stale Bearer token | `shouldAttachBearer()` excludes all public auth paths |
| HIGH | Retried 401 after successful refresh did not fire session-expired | `onSessionExpired` on `retried` 401 path |
| HIGH | OTP challenge lost on cold start showed Login not Otp | `RootNavigator` initial route `Otp` when challenge present |
| MEDIUM | Deactivated 403 cleared session but UI stale | `onDeactivated` callback wired in `AuthContext` |
| MEDIUM | OTP countdown timer leaked / double-fired | `useRef` interval cleanup in `OtpScreen` |
| MEDIUM | `apiClient.ts` was thin re-export only | Added `shouldAttachBearer` helper + tests |
| LOW | Corrupt OTP challenge JSON on verify | try/catch + remove corrupt key (mirrors bootstrap) |

### File List

- apps/mobile/package.json
- apps/mobile/metro.config.js
- apps/mobile/tsconfig.json
- apps/mobile/index.js
- apps/mobile/jest.config.js
- apps/mobile/jest.setup.js
- apps/mobile/src/App.tsx
- apps/mobile/src/config/environment.ts
- apps/mobile/src/context/AuthContext.tsx
- apps/mobile/src/components/AccessibleErrorRegion.tsx
- apps/mobile/src/services/api/apiClient.ts
- apps/mobile/src/services/auth/auth.models.ts
- apps/mobile/src/services/auth/secureStorage.ts
- apps/mobile/src/services/auth/AuthSessionService.ts
- apps/mobile/src/services/auth/roleRouting.ts
- apps/mobile/src/navigation/types.ts
- apps/mobile/src/navigation/AuthNavigator.tsx
- apps/mobile/src/navigation/MainTabNavigator.tsx
- apps/mobile/src/navigation/RootNavigator.tsx
- apps/mobile/src/screens/auth/LoginScreen.tsx
- apps/mobile/src/screens/auth/OtpScreen.tsx
- apps/mobile/src/screens/auth/SessionExpiredScreen.tsx
- apps/mobile/src/screens/auth/WebOnlyScreen.tsx
- apps/mobile/src/screens/today/TodayScreen.tsx
- apps/mobile/src/screens/cases/CasesScreen.tsx
- apps/mobile/src/screens/more/MoreScreen.tsx
- apps/mobile/__tests__/AuthSessionService.test.ts
- apps/mobile/__tests__/LoginScreen.test.tsx
- apps/mobile/__tests__/OtpScreen.test.tsx
- apps/mobile/__tests__/RootNavigator.test.ts
- apps/mobile/__tests__/apiClient.test.ts
- README.md

### Change Log

- 2026-06-14: Story 1.7 created — React Native login/OTP shell mirroring web auth with mobile refresh contract.
- 2026-06-14: Story 1.7 implemented — mobile auth shell, navigation, tests, docs.
- 2026-06-14: Code review patches applied — 15 mobile tests, status done.
