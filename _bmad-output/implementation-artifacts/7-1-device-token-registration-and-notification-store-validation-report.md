# Story Validation Report — 7.1 Device Token Registration and Notification Store

**Story:** `7-1-device-token-registration-and-notification-store`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied)

---

## Summary

Story 7.1 correctly scopes `user_devices` schema, device registration API, logout/session device cleanup, role preferences stub, and mobile FCM token capture — building on the **existing** in-app notification store from Story **4.5**. Alignment with Stories **7.2–7.4** boundaries is sound. Ten gaps could cause wrong DTO reuse on logout, failed mobile registration (missing `putApi`), insecure device ID storage, or incorrect user resolution on anonymous logout.

| Check | Result |
|-------|--------|
| Epic AC (device registration + notification store) | Pass — store exists (4.5); devices new; GET/PATCH regression AC7 |
| Epic "mobile or web" token registration | **Reconciled** — web push deferred; FR-19 desk = email + in-app |
| Do not recreate `in_app_notifications` / controller | Pass |
| Push send deferred to 7.2 | Pass |
| Logout DTO design | **Fix** — separate `LogoutRequest`, not `RefreshRequest` extension |
| Logout user resolution | **Fix** — `record.UserId` from refresh revoke, not JWT |
| Mobile `putApi` missing | **Fix** — explicit task |
| Device install ID storage | **Fix** — Keychain via `secureStorage.ts`, not AsyncStorage |
| Session invalidation device purge test | **Fix** — extend `AuthSessionTests` pattern |
| CaseTestData / Swagger helpers | **Fix** — explicit helpers and asserts |
| AC8 role scope | **Fix** — all authenticated mobile roles, not field-worker only |
| `organisation_id` FK pattern | **Fix** — mirror `in_app_notifications` (no organisations table) |
| Scope vs 7.2/7.3/7.4 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Add **`LogoutRequest`** DTO — **do not** extend `RefreshRequest` with `deviceInstallId` (shared with refresh endpoint) |
| 2 | AC4 + dev notes — logout device delete uses **`userId` from revoked refresh token record**; endpoint is `[AllowAnonymous]` |
| 3 | AC8 — **all authenticated mobile roles** (not field-worker only); `platform` from `Platform.OS` |
| 4 | Device install ID in **Keychain** via extended `secureStorage.ts` — not AsyncStorage or separate loose file |
| 5 | Add **`putApi<T>`** to `AuthSessionService.ts` — required for `PUT /devices/me` |
| 6 | Foreground retry via **`useDeviceRegistrationOnForeground`** — mirror `useSyncOnForeground.ts` |
| 7 | Extend **`AuthSessionTests`** for `InvalidateUserSessionsAsync` device purge |
| 8 | Extend **`CaseTestData`** — `RegisterUserDeviceAsync`, `LogoutWithDeviceAsync` helpers |
| 9 | Extend **`SwaggerEndpointTests`** — assert `/devices/me`, `/notifications/preferences` |
| 10 | Dev note — **`organisation_id` without organisations FK**; `UsersSchemaTests` alphabetical insert between `travel_claims` and `users`; epic web-push reconciliation |

---

## Remaining Notes (non-blocking)

- **Firebase native setup** is environment-specific — README documentation sufficient; do not commit `google-services.json`.
- **Max devices per user** — not capped in v1; Story **7.2** may add cleanup for stale tokens if needed.
- **Web `GET /notifications/preferences`** — optional client call in Story **7.4**; no web UI in this story.
- **7.2 query contract** — will use `SELECT * FROM user_devices WHERE user_id = @userId`; no schema change expected.

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `7-1-device-token-registration-and-notification-store`.

**Out of scope confirmed:** FCM/APNs send (7.2), email templates (7.3), notification bell (7.4), web push registration, rebuilding in-app notification store or mobile notifications list UI (6.4).
