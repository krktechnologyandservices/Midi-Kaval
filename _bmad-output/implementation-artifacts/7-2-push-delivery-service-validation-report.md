# Story Validation Report — 7.2 Push Delivery Service

**Story:** `7-2-push-delivery-service`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (11 fixes applied)

---

## Summary

Story 7.2 correctly scopes FCM push delivery wired to **five existing in-app notification producers**, preference/channel gating from Story **7.1**, stale token cleanup, fake sender for tests, and minimal mobile tap handling. Alignment with Stories **7.3** (email), **7.4** (bell UI), and visit push (no producer) is sound. Eleven gaps could cause background-job send failures (HttpContext on `UserDeviceService`), lost push dispatch after ForSave (void return), broken mobile navigation (nested More stack), or FCM data validation errors.

| Check | Result |
|-------|--------|
| Epic AC (FCM/APNs delivery for existing events) | Pass — five producers identified |
| Epic mentions visits | **Reconciled** — no visit producer/job; five events only |
| Do not recreate in-app store / device registration | Pass |
| Email deferred to 7.3 | Pass |
| Bell UI deferred to 7.4 | Pass |
| ForSave return value for push dispatch | **Fix** — return `InAppNotification` / list |
| Device lookup in background jobs | **Fix** — `PushDeliveryService` uses `AppDbContext`, not `UserDeviceService` |
| Court miss multi-notification push | **Fix** — loop returned list after save |
| Mobile nested navigation | **Fix** — `More → TravelClaimForm` via root `navigationRef` in `App.tsx` |
| Mark-read on push tap | **Fix** — PATCH before navigate |
| FCM data null keys | **Fix** — omit null fields; all values strings |
| Unknown event types | **Fix** — skip with info log, never throw |
| Integration test token | **Fix** — `integration-fcm-token-{guid}`, not dev-stub |
| TrySendAsync exception boundary | **Fix** — internal catch; producers never throw |
| Scope vs 7.3/7.4 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Refactor **`Create*ForSave`** methods to **return** `InAppNotification` (or `IReadOnlyList<>` for court miss) — callers push after save without DB re-query |
| 2 | **`PushDeliveryService` queries `db.UserDevices` directly** — do not use `UserDeviceService` (requires HttpContext; breaks jobs) |
| 3 | Court miss escalation — **loop each returned notification** for `TrySendAsync` after single `SaveChangesAsync` |
| 4 | Mobile push tap — **nested navigation** `More → TravelClaimForm` via **`navigationRef`** on `NavigationContainer` in **`App.tsx`** |
| 5 | Push tap — **`PATCH /notifications/{id}/read`** when `notificationId` in payload (mirror `handleNotificationPress`) |
| 6 | FCM **data payload** — omit null keys; all values must be strings |
| 7 | **Unknown event types** — channel mapper skips with info log; never throws |
| 8 | Integration tests use **`integration-fcm-token-{guid}`** — not `dev-stub-token-*` (server skips stubs) |
| 9 | **`TrySendAsync` catches all exceptions** internally — producers need no try/catch |
| 10 | **`AuthWebApplicationFactory.PushSender`** property — mirror `EmailSender` fake swap pattern |
| 11 | **Epic visit push reconciliation** — document gap; no overdue visit job in codebase |

---

## Remaining Notes (non-blocking)

- **Firebase credentials** are environment-specific — README + env vars sufficient; never commit service account JSON.
- **Court miss push** will skip for coordinators (`pushEnabled: false`) — expected; in-app + email remain.
- **Foreground push display** on iOS/Android — system handles notification block; app handler logs foreground `onMessage` without requiring custom in-app banner in v1.
- **OpenAPI / api-client** — no changes (no new HTTP endpoints).
- **Docker/Testcontainers** — integration tests require Docker; defer note if unavailable in dev session.

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `7-2-push-delivery-service`.

**Out of scope confirmed:** visit overdue job/producer, SMS/WhatsApp (v1.1), email templates (7.3), notification bell chrome (7.4), device registration schema changes (7.1), new REST endpoints.
