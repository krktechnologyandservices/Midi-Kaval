---
baseline_commit: NO_VCS
---

# Story 7.1: Device Token Registration and Notification Store

<!-- Validated: 2026-06-20 — see 7-1-device-token-registration-and-notification-store-validation-report.md (10 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **user**,
I want notifications stored and devices registered,
so that alerts reach me (FR-19).

*Scope: **`user_devices` schema + registration API**, **logout/session-invalidation token cleanup**, **role-based notification preferences stub**, and **mobile FCM/APNs token capture + register**. The **in-app notification store already exists** (Story **4.5** — `in_app_notifications`, `GET/PATCH /api/v1/notifications`); **verify regression only** — do **not** recreate store/controller. **No** FCM/APNs dispatch (Story **7.2**), **no** email templates (Story **7.3**), **no** notification bell chrome (Story **7.4**), **no** web push registration (web v1 uses email + in-app centre per FR-19). Mobile **More → Notifications** list from Story **6.4** stays as-is — only add device registration hook.*

## Acceptance Criteria

1. **Given** the pilot database  
   **When** migrations run  
   **Then** table `user_devices` exists with columns: `id`, `organisation_id`, `user_id`, `device_install_id` (client-stable id, max 64), `platform` (`android` | `ios`), `push_token` (max 512), `created_at_utc`, `updated_at_utc`, `last_registered_at_utc`  
   **And** unique index on `(user_id, device_install_id)`  
   **And** index on `(user_id)` for bulk delete on session invalidation  
   **And** FK `user_id → users.id` with `ON DELETE RESTRICT`  
   **And** `UsersSchemaTests` application table list includes `user_devices`

2. **Given** I am an authenticated user (any role)  
   **When** I `PUT /api/v1/devices/me` with body `{ "deviceInstallId": "...", "platform": "android"|"ios", "pushToken": "..." }`  
   **Then** response is **200 OK** with `{ data: UserDeviceDto, meta }` (envelope via `ApiEnvelopeFilter`)  
   **And** row is **upserted** for `(userId, deviceInstallId)` — updates `push_token`, `platform`, `last_registered_at_utc`, `updated_at_utc` when same device re-registers  
   **And** `organisation_id` matches JWT user's org  
   **And** empty/missing `deviceInstallId`, `platform`, or `pushToken` → **400**  
   **And** invalid `platform` value → **400**  
   **And** unauthenticated → **401**

3. **Given** the same `push_token` is registered to a **different** user (device handoff / account switch on same phone)  
   **When** the new user calls `PUT /api/v1/devices/me`  
   **Then** any prior `user_devices` row with that exact `push_token` is **deleted** before upsert (one token → one active user)

4. **Given** I call `POST /api/v1/auth/logout` with valid refresh token (httpOnly cookie for web **or** JSON body for mobile) and optional `LogoutRequest` body `{ "refreshToken": "...", "deviceInstallId": "..." }`  
   **When** logout succeeds (**204**)  
   **Then** refresh token is revoked (existing Story **1.5** behavior unchanged)  
   **And** when `deviceInstallId` is provided, the matching `user_devices` row for **`userId` resolved from the revoked refresh token record** (logout is `[AllowAnonymous]` — **no** access JWT; do **not** read user from `HttpContext.User`) is **deleted**  
   **And** when `deviceInstallId` is omitted, logout still succeeds — only refresh revocation (backward compatible; web unchanged)

5. **Given** `IUserSessionService.InvalidateUserSessionsAsync(userId)` runs (role change / deactivation — Story **1.5**)  
   **When** session invalidation completes  
   **Then** **all** `user_devices` rows for that `userId` are deleted in the same transaction as `token_version` increment  
   **And** existing refresh-token revocation behavior unchanged

6. **Given** I am authenticated  
   **When** I `GET /api/v1/notifications/preferences`  
   **Then** **200 OK** `{ data: NotificationPreferencesDto, meta }` with **role-based v1 stub defaults** (no DB persistence, no PATCH in this story):  
   - **CaseWorker / SocialWorker:** `{ pushEnabled: true, emailEnabled: false, channels: { visits: true, court: true, interventions: true, claims: true } }`  
   - **Coordinator / Director:** `{ pushEnabled: false, emailEnabled: true, channels: { visits: false, court: true, interventions: true, claims: true, reports: true, assignments: true } }`  
   **And** unauthenticated → **401**

7. **Given** in-app notifications infrastructure from Stories **4.5**, **5.3**, **5.4**, **6.3**, **6.4**  
   **When** I call existing `GET /api/v1/notifications` and `PATCH /api/v1/notifications/{id}/read`  
   **Then** behavior is **unchanged** — list newest-first for current user, mark-read idempotent  
   **And** regression: `NotificationsAndOverdueJobTests`, `TravelClaimNotificationApiTests`, `CourtReminderJobTests`, `CourtMissEscalationJobTests` still pass

8. **Given** I log in on **mobile** (any authenticated role)  
   **When** OTP verification succeeds or app bootstraps an existing session  
   **Then** app obtains a push token (FCM on Android, APNs via FCM on iOS when configured)  
   **And** calls `PUT /api/v1/devices/me` with stable `deviceInstallId` (UUID persisted in **Keychain** via `secureStorage.ts` on first launch — **not** AsyncStorage) and `platform` from `Platform.OS` (`android` \| `ios`)  
   **And** on logout, sends `deviceInstallId` in `LogoutRequest` body **before** clearing local session  
   **And** when Firebase is not configured (dev/CI), app uses config flag / `__DEV__` stub token path so registration API is still testable — **do not block login** if push registration fails (log + retry on next app foreground via `AppState` listener — mirror `useSyncOnForeground.ts` pattern)

9. **Given** regression safety  
   **When** story ships  
   **Then** integration tests cover: device upsert, token handoff delete, logout device removal, session invalidation purge, preferences by role, notifications list regression  
   **And** mobile unit tests cover device registration service (success, API error swallowed on bootstrap, logout unregister)  
   **And** `SwaggerEndpointTests` asserts `/devices/me`, `/notifications/preferences`  
   **And** OpenAPI snapshot + `@midi-kaval/api-client` regenerated  
   **And** README documents device registration, preferences stub, logout `deviceInstallId`, and clarifies in-app store predates this story

## Tasks / Subtasks

- [x] **API — schema** (AC: 1)
  - [x] `Domain/Entities/UserDevice.cs` + `UserDeviceConfiguration.cs`
  - [x] EF migration `AddUserDevices`
  - [x] Register `DbSet<UserDevice>` on `AppDbContext`
  - [x] Update `UsersSchemaTests` table list + `ClearUsersAsync` TRUNCATE includes `user_devices`

- [x] **API — device service + controller** (AC: 2–3)
  - [x] `Infrastructure/Notifications/UserDeviceService.cs` — `RegisterAsync(deviceInstallId, platform, pushToken)` with upsert + cross-user token dedup delete
  - [x] `Models/Notifications/UserDeviceDtos.cs` — `RegisterUserDeviceRequest`, `UserDeviceDto`
  - [x] `Controllers/V1/DevicesController.cs` — `PUT /api/v1/devices/me` `[Authorize]`
  - [x] Register in DI (`Program.cs`)

- [x] **API — logout + session invalidation hooks** (AC: 4–5)
  - [x] Add **`LogoutRequest`** DTO `{ refreshToken?, deviceInstallId? }` — **do not** add `deviceInstallId` to `RefreshRequest` (shared with refresh endpoint)
  - [x] `AuthController.Logout` — read refresh via `AuthTokenHelpers.ReadRefreshToken`; parse `deviceInstallId` from `LogoutRequest` body only
  - [x] `AuthService.LogoutAsync(refreshToken, deviceInstallId?)` — after `RevokeAsync` returns `record`, delete `(record.UserId, deviceInstallId)` when provided
  - [x] `UserSessionService.InvalidateUserSessionsAsync` — delete all `user_devices` for user in same transaction before save
  - [x] Audit optional: `device.registered` / `device.removed` — **skip in v1** unless trivial; logout audit unchanged

- [x] **API — preferences stub** (AC: 6)
  - [x] `NotificationPreferencesDto` + static `NotificationPreferencesDefaults.ForRole(role)`
  - [x] `GET /api/v1/notifications/preferences` on `NotificationsController` (or dedicated controller — prefer extending existing notifications controller)

- [x] **API — tests + OpenAPI** (AC: 7–9)
  - [x] `DeviceRegistrationApiTests.cs` — upsert, validation 400, token handoff, logout removal, preferences by role
  - [x] Extend `AuthSessionTests` — `InvalidateUserSessionsAsync` deletes all `user_devices` for user (reuse existing invalidate test pattern)
  - [x] Extend `CaseTestData` — `RegisterUserDeviceAsync`, `SendRegisterUserDeviceAsync`, `LogoutWithDeviceAsync`
  - [x] Extend `SwaggerEndpointTests` — assert `/api/v1/devices/me`, `/api/v1/notifications/preferences`
  - [x] Regression filter: `NotificationsAndOverdueJob`, `TravelClaimNotification`
  - [x] Export OpenAPI + regenerate api-client
  - [x] README section update

- [x] **Mobile — device registration** (AC: 8)
  - [x] Add `@react-native-firebase/app` + `@react-native-firebase/messaging` (document native setup in README — `google-services.json`, APNs capability)
  - [x] Extend `secureStorage.ts` — `getOrCreateDeviceInstallId()` persisted in Keychain (dedicated service name)
  - [x] `services/devices/DeviceRegistrationService.ts` — get token + `PUT /devices/me`; dev stub when Firebase unavailable
  - [x] Add `putApi<T>` to `AuthSessionService.ts` (missing today — required for device register)
  - [x] Hook: after `verifyOtp` success + `bootstrapSession` when authenticated; `useDeviceRegistrationOnForeground` (mirror `useSyncOnForeground.ts`)
  - [x] Extend mobile logout — send `LogoutRequest` with `deviceInstallId` before `clearSession`
  - [x] Unit tests with mocked fetch/Firebase

### Review Findings (2026-06-20)

- [x] [Review][Patch] Missing max-length validation for `deviceInstallId` (64) and `pushToken` (512) — oversize values hit DB constraint instead of **400** [`UserDeviceService.cs:111-124`]
- [x] [Review][Patch] Device registration failures swallowed without log — AC8 requires log on failure (foreground retry exists but no diagnostic) [`DeviceRegistrationService.ts:31-33`]
- [x] [Review][Patch] No mobile unit test asserting logout sends `deviceInstallId` in body — AC9 explicit [`AuthSessionService.ts:170-189`]
- [x] [Review][Defer] Integration tests not executed in dev session (Docker/Testcontainers unavailable) — run `dotnet test tests/api.integration --filter DeviceRegistration` before marking done
- [x] [Review][Defer] `RemoveAllForUserAsync` unused after inline delete in `UserSessionService` — dead code; harmless for v1 [`UserDeviceService.cs:96-109`]

## Dev Notes

### READ FIRST — do not reinvent

1. **In-app notification store is DONE** — Story **4.5** created `in_app_notifications`, `NotificationService`, `NotificationsController`. Stories **5.3**, **5.4**, **6.3**, **6.4** added event types and producers. This story adds **device tokens only** + preferences stub. **Do not** duplicate list/mark-read logic.

2. **Push delivery is Story 7.2** — all producers already log *"Push deferred to Story 7.2..."*. Story **7.1** registers tokens so **7.2** can query `user_devices` by `user_id`. **Do not** call FCM/APNs send APIs here.

3. **Web device registration out of scope** — FR-19 assigns **web email** + in-app centre to desk roles; **mobile push** to field roles. Web may call `GET /notifications/preferences` but does **not** register FCM/APNs tokens in v1.

4. **Mobile notifications UI exists** — Story **6.4** built `NotificationsListScreen` under More. Do **not** rebuild list UI or bell chrome (Story **7.4**).

5. **Logout contract extension** — Story **1.5** established refresh-token-only logout. Add separate **`LogoutRequest`** DTO — **never** extend `RefreshRequest` with `deviceInstallId` (refresh endpoint would inherit unused field). Web logout unchanged (cookie + no device id).

6. **Logout resolves user from refresh token record** — `POST /auth/logout` is `[AllowAnonymous]`; device row delete uses `RefreshTokenStore.RevokeAsync` → `record.UserId`, not JWT claims.

7. **Session invalidation must purge devices** — when `token_version` bumps, stale push tokens must not receive alerts after role change/deactivation (FR-2). Extend existing `AuthSessionTests` invalidate pattern.

8. **Epic AC reconciliation** — Epic 7.1 bundles existing `GET/PATCH /notifications` (done in **4.5**) with new device registration. Web FCM/APNs registration deferred: FR-19 assigns desk roles **email + in-app**; field roles **mobile push**. Preferences stub serves all roles.

9. **POCSO** — push tokens are device identifiers, not beneficiary PII. Preferences stub has no case-level data.

10. **Envelope pattern** — new success endpoints return raw DTO from controller; `ApiEnvelopeFilter` wraps `{ data, meta }`. Logout stays **204** without envelope.

11. **`organisation_id` without organisations FK** — mirror `in_app_notifications`: column for tenant-readiness, FK only on `user_id → users` (no `organisations` table in v1).

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `InAppNotification.cs` / `NotificationService.cs` | List + mark read + create helpers | **No change** to list/mark-read; optional: none |
| `NotificationsController.cs` | GET list, PATCH read | **Add** `GET preferences` only |
| `AuthService.LogoutAsync` | Revokes refresh token | Delete device row when `deviceInstallId` provided |
| `UserSessionService.cs` | Revokes refresh + bumps token_version | Delete all `user_devices` for user |
| `RefreshRequest` | `{ refreshToken? }` | **No change** — use new `LogoutRequest` for logout |
| `AuthSessionService.ts` (mobile) | Logout sends `{ refreshToken }`; no `putApi` | Add `putApi`; logout sends `LogoutRequest` |
| `secureStorage.ts` | Tokens only | Add `getOrCreateDeviceInstallId()` |
| `AuthContext.tsx` | Bootstrap after login | Trigger device registration |
| `UsersSchemaTests.cs` | 15 tables | Add `user_devices` |

### `user_devices` schema contract

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid PK | Generated server-side on first insert |
| `organisation_id` | uuid | From authenticated user |
| `user_id` | uuid FK | Owner |
| `device_install_id` | varchar(64) | Client UUID persisted in secure storage |
| `platform` | varchar(16) | `android` \| `ios` only |
| `push_token` | varchar(512) | FCM registration token |
| `created_at_utc` | timestamptz | Set on insert |
| `updated_at_utc` | timestamptz | Touch on upsert |
| `last_registered_at_utc` | timestamptz | Set on every successful register |

**Upsert key:** `(user_id, device_install_id)`  
**Token handoff:** delete `WHERE push_token = @token AND user_id <> @currentUserId` before upsert

### API contract

| Action | Method | Path | Policy | Body / response |
|--------|--------|------|--------|-----------------|
| Register device | PUT | `/api/v1/devices/me` | Authenticated | Request: `{ deviceInstallId, platform, pushToken }` → `UserDeviceDto` |
| Notification prefs (stub) | GET | `/api/v1/notifications/preferences` | Authenticated | `NotificationPreferencesDto` |
| List notifications | GET | `/api/v1/notifications` | Authenticated | **Unchanged** |
| Mark read | PATCH | `/api/v1/notifications/{id}/read` | Authenticated | **Unchanged** |
| Logout | POST | `/api/v1/auth/logout` | AllowAnonymous + valid refresh | `LogoutRequest`: optional `{ refreshToken, deviceInstallId }` → **204** |

**UserDeviceDto (response):** `{ id, deviceInstallId, platform, lastRegisteredAtUtc }` — **omit** `pushToken` from response (security).

**NotificationPreferencesDto:** `{ pushEnabled, emailEnabled, channels: { visits?, court?, interventions?, claims?, reports?, assignments? } }` — all booleans; extra keys ignored by clients.

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Domain/Entities/UserDevice.cs` |
| NEW | `apps/api/Infrastructure/Persistence/UserDeviceConfiguration.cs` |
| NEW | `apps/api/Infrastructure/Notifications/UserDeviceService.cs` |
| NEW | `apps/api/Models/Notifications/UserDeviceDtos.cs` |
| NEW | `apps/api/Models/Notifications/NotificationPreferencesDtos.cs` |
| NEW | `apps/api/Infrastructure/Notifications/NotificationPreferencesDefaults.cs` |
| NEW | `apps/api/Controllers/V1/DevicesController.cs` |
| NEW | `apps/api/Migrations/*_AddUserDevices.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/AppDbContext.cs` |
| UPDATE | `apps/api/Infrastructure/Auth/AuthService.cs` |
| UPDATE | `apps/api/Infrastructure/Auth/UserSessionService.cs` |
| NEW | `LogoutRequest` in `apps/api/Models/Auth/AuthDtos.cs` |
| UPDATE | `apps/api/Controllers/V1/AuthController.cs` — bind `LogoutRequest`; pass `deviceInstallId` to LogoutAsync |
| UPDATE | `apps/api/Controllers/V1/NotificationsController.cs` — GET preferences |
| UPDATE | `apps/api/Program.cs` |
| NEW | `tests/api.integration/DeviceRegistrationApiTests.cs` |
| UPDATE | `tests/api.integration/UsersSchemaTests.cs` |
| NEW | `apps/mobile/src/services/devices/DeviceRegistrationService.ts` |
| NEW | `apps/mobile/src/services/devices/useDeviceRegistrationOnForeground.ts` |
| UPDATE | `apps/mobile/src/services/auth/secureStorage.ts` |
| UPDATE | `apps/mobile/src/services/auth/AuthSessionService.ts` — `putApi` + `LogoutRequest` |
| UPDATE | `apps/mobile/src/context/AuthContext.tsx` |
| UPDATE | `apps/mobile/package.json` — Firebase messaging deps |
| NEW | `apps/mobile/__tests__/DeviceRegistrationService.test.tsx` |
| UPDATE | `README.md` |

**Reuse without modification:** `NotificationService.ListForCurrentUserAsync`, `MarkReadAsync`, all notification producers, `NotificationsListScreen.tsx`, web `CaseApiService.listNotifications` (unused until 7.4).

### Previous story intelligence (6.4)

- Mobile uses hand-rolled services + `auth.getApi`/`patchApi` (no `@midi-kaval/api-client` on mobile). Follow same pattern for `DeviceRegistrationService` — **`putApi` must be added** to `AuthSessionService` (not present today).
- Push defer log already at claim approve/return — **7.2** will read `user_devices` before send.
- Notifications list uses `useFocusEffect` reload — device registration is separate concern; register on auth bootstrap not on notifications screen focus.
- Field-worker guard on notifications UI — device registration runs for **all authenticated mobile roles** (future coordinator mobile if any).

### Previous story intelligence (4.5)

- Created minimal notification store intentionally labeled "subset of Epic 7". **7.1 completes the store half** (devices); **7.2** completes delivery.
- Integration test helpers: `CaseTestData.ListNotificationsAsync`, `MarkNotificationReadAsync` — reuse for regression.
- `UsersSchemaTests` TRUNCATE order matters — add `user_devices` before `users`. Table list is **alphabetical** — insert `user_devices` between `travel_claims` and `users`.

### Previous story intelligence (1.5)

- Logout requires valid refresh token (cookie or body); returns **204** without envelope.
- `InvalidateUserSessionsAsync` is the hook for staff deactivation (Story **9.2**) — device purge must live here.

### Mobile Firebase setup notes

- **React Native 0.76.9** — use `@react-native-firebase/app` and `@react-native-firebase/messaging` versions compatible with RN 0.76 (check npm peer deps; pin exact versions in `package.json`).
- Android: `google-services.json` in `apps/mobile/android/app/`; apply Google services plugin in Gradle (document in README — not committed secrets).
- iOS: APNs key in Firebase console; enable Push Notifications capability.
- **Dev/CI fallback:** when `messaging().getToken()` fails (no Firebase config), use `Push:DevFallbackToken` from app config OR skip registration with warning log — **never** block auth flow.
- Request notification permission (Android 13+ / iOS) before `getToken()` — use `messaging().requestPermission()` on iOS; Android 13+ POST_NOTIFICATIONS.

### Testing requirements

- API: `dotnet test tests/api.integration --filter DeviceRegistration`
- API regression: `dotnet test tests/api.integration --filter "FullyQualifiedName~Notification"`
- Mobile: `npm test -w apps/mobile -- DeviceRegistration`
- Manual: login mobile → verify `user_devices` row in DB → logout → row deleted → login different user same device → token handoff

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 7 Story 7.1]
- [Source: `_bmad-output/planning-artifacts/architecture.md` §5.6 — `user_devices`, notification jobs]
- [Source: `_bmad-output/planning-artifacts/epics.md` FR-19 — multi-channel per role]
- [Source: `_bmad-output/implementation-artifacts/4-5-interventions-ui-and-overdue-job.md` — notification store origin]
- [Source: `_bmad-output/implementation-artifacts/6-4-claim-status-notifications.md` — push defer, mobile list]
- [Source: `_bmad-output/implementation-artifacts/1-5-refresh-logout-and-forced-session-invalidation.md` — logout contract]
- [Source: `apps/api/Infrastructure/Notifications/NotificationService.cs`]
- [Source: `apps/api/Controllers/V1/NotificationsController.cs`]
- [Source: `apps/api/Infrastructure/Auth/UserSessionService.cs`]
- [Source: `apps/mobile/src/services/auth/AuthSessionService.ts`]
- [Source: `_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

Composer (Cursor)

### Debug Log References

- API build succeeded; integration tests require Docker/Testcontainers (Docker Desktop not running in dev session).
- OpenAPI snapshot exported via `Export_Swagger_WhenRequested` + `@midi-kaval/api-client` regenerated.
- Mobile: 3/3 DeviceRegistrationService tests passed.

### Completion Notes List

- Added `user_devices` table, `UserDeviceService`, `PUT /api/v1/devices/me` with upsert + cross-user token handoff.
- Added `LogoutRequest` DTO; logout and session invalidation purge device rows (same transaction for invalidate).
- Added `GET /api/v1/notifications/preferences` role-based stub.
- Mobile: Keychain `deviceInstallId`, `DeviceRegistrationService`, foreground retry hook, Firebase deps with dev stub fallback.
- Integration tests authored (`DeviceRegistrationApiTests`); run with Docker for full pass.

### File List

- apps/api/Domain/Entities/UserDevice.cs (new)
- apps/api/Infrastructure/Persistence/UserDeviceConfiguration.cs (new)
- apps/api/Infrastructure/Notifications/UserDeviceService.cs (new)
- apps/api/Infrastructure/Notifications/NotificationPreferencesDefaults.cs (new)
- apps/api/Models/Notifications/UserDeviceDtos.cs (new)
- apps/api/Models/Notifications/NotificationPreferencesDtos.cs (new)
- apps/api/Controllers/V1/DevicesController.cs (new)
- apps/api/Migrations/*_AddUserDevices.cs (new)
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Infrastructure/Auth/AuthService.cs
- apps/api/Infrastructure/Auth/UserSessionService.cs
- apps/api/Infrastructure/Auth/AuthTokenHelpers.cs
- apps/api/Models/Auth/AuthDtos.cs
- apps/api/Controllers/V1/AuthController.cs
- apps/api/Controllers/V1/NotificationsController.cs
- apps/api/Program.cs
- tests/api.integration/DeviceRegistrationApiTests.cs (new)
- tests/api.integration/UsersSchemaTests.cs
- tests/api.integration/CaseCreateTests.cs
- tests/api.integration/SwaggerEndpointTests.cs
- apps/mobile/src/services/auth/secureStorage.ts
- apps/mobile/src/services/auth/AuthSessionService.ts
- apps/mobile/src/services/devices/pushMessaging.ts (new)
- apps/mobile/src/services/devices/DeviceRegistrationService.ts (new)
- apps/mobile/src/services/devices/useDeviceRegistrationOnForeground.ts (new)
- apps/mobile/src/context/AuthContext.tsx
- apps/mobile/src/navigation/RootNavigator.tsx
- apps/mobile/package.json
- apps/mobile/__tests__/DeviceRegistrationService.test.tsx (new)
- packages/api-client/openapi-snapshot.json
- packages/api-client/src/generated/api.ts
- README.md

## Change Log

- 2026-06-20: Story 7.1 created — user_devices schema, device registration API, logout/session device cleanup, role preferences stub, mobile FCM registration; in-app store verified as pre-existing from 4.5; push send deferred to 7.2.
- 2026-06-20: Validation — 10 fixes (LogoutRequest vs RefreshRequest, logout userId from refresh record, Keychain deviceInstallId, putApi, AuthSessionTests purge, CaseTestData helpers, Swagger asserts, AC8 all roles, Platform.OS, epic web-push reconciliation).
- 2026-06-20: Implementation complete — API device store + preferences stub, logout/session cleanup, mobile registration hook, tests, OpenAPI/client regen, README; ready for code review.
