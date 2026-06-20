---
baseline_commit: NO_VCS
---

# Story 7.2: Push Delivery Service

<!-- Validated: 2026-06-20 — see 7-2-push-delivery-service-validation-report.md (11 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **field worker**,
I want push for visits, court, interventions, and claims,
so that I act on time (FR-19).

*Scope: **Server-side FCM push delivery** wired to **existing in-app notification producers** (Stories **4.5**, **5.3**, **5.4**, **6.3**, **6.4**), reading device tokens from **`user_devices`** (Story **7.1**) and honouring the **role-based preferences stub** (Story **7.1**). **Minimal mobile push tap handling** for known event types (reuse `handleNotificationPress` navigation). **No** visit push producer (overdue visit daily job not built — architecture FR-8 deferred), **no** SMS/WhatsApp (v1.1), **no** email templates (Story **7.3**), **no** notification bell chrome (Story **7.4**), **no** new REST endpoints, **no** preferences PATCH persistence.*

## Acceptance Criteria

1. **Given** `PushNotifications:Enabled` is true and Firebase credentials are configured  
   **When** the API starts  
   **Then** `FirebaseApp` initializes once via `FirebaseAdmin` SDK  
   **And** `IPushNotificationSender` resolves to `FirebasePushNotificationSender`  
   **And** when disabled (Development default / missing credentials), resolves to `FakePushNotificationSender` that logs payloads without calling Google

2. **Given** an in-app notification is created for a user with `pushEnabled: true` and matching channel enabled (Story **7.1** stub defaults)  
   **When** `PushDeliveryService.TrySendAsync` runs after the in-app row is persisted  
   **Then** service loads active `user_devices` rows for `(organisationId, userId)`  
   **And** skips tokens starting with `dev-stub-token-` (Story **7.1** dev fallback — never call FCM)  
   **And** sends FCM message per device with **notification** block (`title`, `body`) plus **data** payload — **all values strings**; omit keys when source field is null (FCM requirement):  
   `{ notificationId, eventType, title, body, caseId?, resourceType?, resourceId? }`  
   **And** Android uses high priority; iOS uses `apns` headers for alert display  
   **And** push failure for one device **does not** roll back the in-app notification or producer transaction

3. **Given** event type → channel mapping  

   | `eventType` | Channel key (`NotificationPreferenceChannelsDto`) |
   |-------------|---------------------------------------------------|
   | `intervention.overdue` | `interventions` |
   | `court.reminder.24h` | `court` |
   | `court.miss.escalated` | `court` |
   | `travel.claim.approved` | `claims` |
   | `travel.claim.returned` | `claims` |

   **When** channel is `false` for user's role defaults **or** `pushEnabled` is `false` **or** event type is unmapped  
   **Then** push is skipped with structured info log (not error)  
   **And** in-app notification still exists  
   **And** unmapped/unknown `eventType` never throws — skip only

4. **Given** existing producers that today log *"Push deferred to Story 7.2..."*  
   **When** each producer completes successfully  
   **Then** push dispatch replaces defer log:

   | Producer | File | Trigger |
   |----------|------|---------|
   | Intervention overdue | `InterventionOverdueJobRunner` / `NotificationService.CreateInterventionOverdueNotificationAsync` | After `SaveChangesAsync` |
   | Court reminder 24h | `CourtReminderJobRunner.ProcessSittingAsync` | After `SaveChangesAsync` — assignee only |
   | Court miss escalation | `CourtMissEscalationJobRunner` | After `SaveChangesAsync` — per coordinator row (expect skip: desk roles `pushEnabled: false`) |
   | Travel claim approved/returned | `TravelClaimService.ApproveAsync` / `ReturnAsync` | After `SaveChangesAsync` — claimant |

   **And** `Create*ForSave` helpers **return** the persisted `InAppNotification` (or `IReadOnlyList<InAppNotification>` for court miss) so callers invoke `TrySendAsync` with the in-memory entity — **do not** re-query DB by resource id  
   **And** defer log lines removed  
   **And** `TrySendAsync` catches all exceptions internally — producers never need try/catch  
   **And** on per-device send failure, log `LogWarning` with `userId`, `eventType`, `deviceInstallId`, error — never throw to caller

5. **Given** FCM returns token invalid / unregistered (`NotRegistered`, `InvalidArgument` for bad token)  
   **When** send fails for a device  
   **Then** matching `user_devices` row is **deleted** (stale token cleanup)  
   **And** other devices for same user still receive push

6. **Given** regression safety  
   **When** story ships  
   **Then** unit tests cover: channel gating, `pushEnabled` false skip, stub token skip, multi-device fan-out, invalid token deletion (mock sender)  
   **And** integration test: register device with token **`integration-fcm-token-{guid}`** (not `dev-stub-token-*`) → trigger travel claim approve → `FakePushNotificationSender` captures payload with correct `eventType` and `resourceId`  
   **And** integration regression: `NotificationsAndOverdueJobTests`, `CourtReminderJobTests`, `CourtMissEscalationJobTests`, `TravelClaimNotificationApiTests` still pass  
   **And** README documents `PushNotifications` config, Firebase service account setup, and dev fake sender behavior

7. **Given** mobile app with registered device (Story **7.1**)  
   **When** user taps a push notification (app background/quit) or receives one in foreground  
   **Then** `pushNotificationHandlers.ts` parses FCM data payload  
   **And** when `notificationId` present, calls `PATCH /api/v1/notifications/{id}/read` before navigate (mirror `handleNotificationPress`)  
   **And** `travel.claim.approved` / `travel.claim.returned` navigates via **root** `navigationRef` to nested route:  
   `More → TravelClaimForm` with `{ claimId: resourceId, mode: 'view' }` — **not** flat `TravelClaimForm` (screen lives in `MoreStackNavigator`)  
   **And** reuse logic from exported `handleNotificationPress` (`NotificationsListScreen.tsx`) — extract shared helper if needed to avoid duplicate mark-read + navigate  
   **And** other event types: log + no crash (full deep links deferred Story **7.4**)  
   **And** handlers registered from `App.tsx` using `NavigationContainer` ref + auth session gate; cleanup on logout  
   **And** unit test covers payload parse + mark-read + nested navigation callback for travel claim event

## Tasks / Subtasks

- [x] **API — push infrastructure** (AC: 1, 2, 5)
  - [x] Add `FirebaseAdmin` NuGet to `MidiKaval.Api.csproj`
  - [x] `PushNotificationsOptions` — `{ SectionName = "PushNotifications", Enabled, ProjectId, CredentialsPath?, CredentialsJson? }`
  - [x] `IPushNotificationSender` + `PushSendRequest` / `PushSendResult` (per token success/failure + error code)
  - [x] `FakePushNotificationSender` — capture sent messages (mirror `FakeEmailSender`)
  - [x] `FirebasePushNotificationSender` — FCM HTTP v1 via FirebaseAdmin; map invalid token errors
  - [x] Register in DI: fake when `!Enabled` or credentials missing; else Firebase singleton init

- [x] **API — delivery orchestration** (AC: 2, 3, 4, 5)
  - [x] `PushDeliveryService` — query `db.Users` + `db.UserDevices` directly (**not** `UserDeviceService` — requires HttpContext)
  - [x] `PushNotificationChannelMapper` — event type → channel bool; unknown types → skip
  - [x] Refactor `Create*ForSave` methods to **return** notification entity/entities for push dispatch
  - [x] `RemoveStaleDeviceByPushTokenAsync` on `PushDeliveryService` or `AppDbContext` extension — delete by `(organisationId, pushToken)`
  - [x] Wire `PushDeliveryService` into producers (table AC4) — **after** `SaveChangesAsync`
  - [x] Remove all *"Push deferred to Story 7.2"* log lines

- [x] **API — tests + docs** (AC: 6)
  - [x] `tests/api.unit/PushDeliveryServiceTests.cs`
  - [x] `AuthWebApplicationFactory.PushSender` (`FakePushNotificationSender`) + swap in `ConfigureTestServices` (mirror `EmailSender`)
  - [x] `PushDeliveryApiTests.cs` or extend `TravelClaimNotificationApiTests` — device register + approve → captured push
  - [x] README `PushNotifications` section + env var examples (`PushNotifications__CredentialsJson`)

- [x] **Mobile — notification handlers** (AC: 7)
  - [x] `services/devices/pushNotificationHandlers.ts` — `registerPushNotificationHandlers(navigationRef, auth)`
  - [x] `navigation/navigationRef.ts` — export ref; wire `NavigationContainer ref=` in `App.tsx`
  - [x] Extract shared `navigateFromNotificationPayload` (optional) from `handleNotificationPress` pattern
  - [x] `__tests__/pushNotificationHandlers.test.ts`

### Review Findings (2026-06-20)

- [x] [Review][Patch] `InvalidArgument` mapped as stale token too broadly — non-token FCM errors could delete valid `user_devices` rows [`FirebasePushNotificationSender.cs:64-67`]
- [x] [Review][Patch] Foreground `onMessage` logs raw data but does not parse payload — AC7 requires `parsePushData` on foreground receive [`pushNotificationHandlers.ts:80-82`]
- [x] [Review][Patch] Push tap handler uses bare `void` on async navigation — mark-read/network failures become unhandled rejections [`pushNotificationHandlers.ts:68-69`]
- [x] [Review][Defer] Integration tests (`PushDeliveryApiTests`, notification regression) not executed in dev session — Docker/Testcontainers unavailable
- [x] [Review][Defer] Visit push producer still absent — explicit story scope; documented in README

## Dev Notes

### READ FIRST — do not reinvent

1. **Device tokens are DONE (7.1)** — query `user_devices` by `user_id` + `organisation_id`. Do **not** add registration endpoints or schema changes.

2. **In-app notifications are DONE (4.5+)** — push is a **delivery channel** alongside existing rows. Do **not** duplicate notification copy or change `GET/PATCH /notifications` contracts.

3. **Preferences stub is DONE (7.1)** — use `NotificationPreferencesDefaults.ForRole(user.Role)` for gating. Do **not** add DB preferences table or PATCH endpoint (future story).

4. **Email is separate (7.3)** — court reminder / miss jobs already send email. Push supplements mobile field workers only. Coordinators (`pushEnabled: false`) get email + in-app, not push.

5. **Visit push producer does NOT exist** — architecture mentions overdue visit daily job (FR-8) but no job/producer is implemented. **Do not** build overdue visit job in this story. Design `PushDeliveryService.TrySendAsync(InAppNotification)` so a future visit producer can call it. Document gap in README.

6. **SyncPushService is unrelated** — `apps/api/Infrastructure/Sync/SyncPushService.cs` is offline **mutation sync**, not FCM. Do **not** modify.

7. **POCSO** — push title/body come from existing in-app copy (already sanitized — crime number on court miss is supervisor-facing). Do **not** add beneficiary names to push payload beyond what in-app body already contains.

8. **Transaction boundary** — in-app row commits first; push is best-effort after save. Never call FCM inside EF transaction before `SaveChangesAsync`.

9. **Dev stub tokens** — mobile registers `dev-stub-token-{platform}` in `__DEV__`. Server must skip these (FCM would fail); integration tests use real-format fake tokens.

10. **Bell UI is Story 7.4** — do not add header bell badge. Mobile handler only needs tap-from-system-tray path.

11. **Epic AC reconciliation** — Epic 7.2 mentions visit push (FR-8); **no visit notification producer or overdue visit job exists** in codebase. Deliver push for the **five existing event types only**; document visit gap in README for a future job story.

12. **`UserDeviceService` is HTTP-scoped** — background jobs and `TravelClaimService` have no HttpContext. `PushDeliveryService` queries `db.UserDevices` directly — never inject `UserDeviceService` for send path.

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `NotificationService.cs` | Creates in-app rows; ForSave helpers void return | Return entities from ForSave; call `PushDeliveryService` after intervention overdue save |
| `InterventionOverdueJobRunner.cs` | Logs push deferred | Remove defer log only — push dispatched inside `NotificationService` |
| `CourtReminderJobRunner.cs` | Email + in-app + defer log | Capture returned notification; push after save for assignee |
| `CourtMissEscalationJobRunner.cs` | Email + in-app per coordinator + defer log | Loop returned notifications; push each after save (expect skip for coordinators) |
| `TravelClaimService.cs` | In-app + defer log on approve/return | Capture returned notification; push after save |
| `NotificationEventTypes.cs` | 5 event constants | **No change** unless adding visit constant (defer) |
| `NotificationPreferencesDefaults.cs` | Role stub | **Reuse** — no change |
| `UserDeviceService.cs` | Register/remove devices (HttpContext) | **No change** for push send — stale token delete in `PushDeliveryService` |
| `Program.cs` | Registers notification services | Register push services + options in same block as `NotificationService` |
| `AuthWebApplicationFactory.cs` | Swaps `IEmailSender` | Add `PushSender` fake + swap `IPushNotificationSender` |
| `pushMessaging.ts` | Token resolution only | **No change** to token path |
| `NotificationsListScreen.tsx` | `handleNotificationPress` export | **Reuse** / extract shared navigate helper |
| `App.tsx` | `NavigationContainer` without ref | Add `navigationRef` + push handler registration |

### Push delivery contract

**Entry point:**

```csharp
Task PushDeliveryService.TrySendAsync(
    InAppNotification notification,
    CancellationToken cancellationToken = default);
```

**Gating order:**
1. Load user by `notification.UserId` — inactive/missing → skip info log
2. `NotificationPreferencesDefaults.ForRole(user.Role)` → `PushEnabled` false → skip
3. `PushNotificationChannelMapper.IsEnabled(prefs.Channels, notification.EventType)` → false or unmapped → skip
4. Query `db.UserDevices.Where(d => d.OrganisationId == notification.OrganisationId && d.UserId == notification.UserId)` — empty → skip info log
5. Filter out `dev-stub-token-*`
6. Send per device; delete stale row on permanent token failure via separate `SaveChangesAsync` (do not share EF transaction with producer)

**ForSave refactor (required):**

```csharp
// Before: void CreateTravelClaimDecisionNotificationForSave(...)
InAppNotification CreateTravelClaimDecisionNotificationForSave(...);

// Court miss:
IReadOnlyList<InAppNotification> CreateCourtMissEscalationNotificationsForSave(...);
```

Callers hold reference(s), `SaveChangesAsync`, then `await pushDeliveryService.TrySendAsync(notification)`.

**FCM message shape (conceptual):**

```json
{
  "notification": { "title": "...", "body": "..." },
  "data": {
    "notificationId": "uuid",
    "eventType": "travel.claim.approved",
    "title": "...",
    "body": "...",
    "caseId": "uuid",
    "resourceType": "TravelClaim",
    "resourceId": "uuid"
  },
  "android": { "priority": "high" },
  "apns": { "headers": { "apns-priority": "10" }, "payload": { "aps": { "alert": { "title": "...", "body": "..." } } } }
}
```

Use platform from `user_devices.platform` when building per-device message if needed; FCM accepts one message per token.

### Configuration

| Key | Default | Notes |
|-----|---------|-------|
| `PushNotifications:Enabled` | `false` in Development | Set `true` in staging/prod |
| `PushNotifications:ProjectId` | — | Firebase project id |
| `PushNotifications:CredentialsPath` | — | Path to service account JSON file |
| `PushNotifications:CredentialsJson` | — | Alternative: inline JSON from env var (Docker/K8s) |

**Never commit** service account JSON. Document env-var pattern in README (mirror blob/JWT secrets).

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Infrastructure/Notifications/PushNotificationsOptions.cs` |
| NEW | `apps/api/Infrastructure/Notifications/IPushNotificationSender.cs` |
| NEW | `apps/api/Infrastructure/Notifications/FakePushNotificationSender.cs` |
| NEW | `apps/api/Infrastructure/Notifications/FirebasePushNotificationSender.cs` |
| NEW | `apps/api/Infrastructure/Notifications/PushDeliveryService.cs` |
| NEW | `apps/api/Infrastructure/Notifications/PushNotificationChannelMapper.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` — return types on ForSave |
| UPDATE | `apps/api/Jobs/CourtReminderBackgroundService.cs` (runner section) |
| UPDATE | `apps/api/Jobs/CourtMissEscalationBackgroundService.cs` (runner section) |
| UPDATE | `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs` |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `apps/api/MidiKaval.Api.csproj` |
| NEW | `tests/api.unit/PushDeliveryServiceTests.cs` |
| NEW | `tests/api.integration/PushDeliveryApiTests.cs` (or extend travel claim notification tests) |
| UPDATE | `tests/api.integration/AuthWebApplicationFactory.cs` |
| NEW | `apps/mobile/src/navigation/navigationRef.ts` |
| NEW | `apps/mobile/src/services/devices/pushNotificationHandlers.ts` |
| UPDATE | `apps/mobile/src/App.tsx` |
| NEW | `apps/mobile/__tests__/pushNotificationHandlers.test.ts` |
| UPDATE | `README.md` |

**No OpenAPI / api-client changes** — no new HTTP endpoints.

### Previous story intelligence (7.1)

- `user_devices` indexed by `user_id`; tokens max 512 chars; cross-user token handoff already handled on register.
- Logout + session invalidation purge devices — push to stale sessions impossible after invalidation (correct).
- `NotificationPreferencesDefaults`: field workers `pushEnabled: true`; coordinators/directors `pushEnabled: false`.
- Mobile `dev-stub-token-{platform}` when Firebase unavailable — server must skip.
- Integration tests need Docker/Testcontainers — document if unavailable in dev session.

### Previous story intelligence (6.4)

- Travel claim push copy already in `TravelClaimNotificationCopy` / in-app body — reuse `notification.Title` and `notification.Body` for FCM display.
- Mobile `handleNotificationPress` exported from `NotificationsListScreen.tsx` — import into push handler.
- Claim navigation: `TravelClaimForm` with `{ claimId: resourceId, mode: 'view' }`.

### Previous story intelligence (5.3 / 5.4)

- Court reminder targets **assignee** (field worker) — primary push beneficiary for court channel.
- Court miss targets **coordinators** — push likely skipped by prefs; still call `TrySendAsync` for consistency (info log skip).

### Previous story intelligence (4.5)

- Intervention overdue assigns to `intervention.AssignedStaffUserId` — field role, push expected.
- Job runner invoked directly in integration tests — push fake capturable in same tests.

### Library requirements

- **FirebaseAdmin** NuGet (`FirebaseAdmin` package) — pin latest stable compatible with .NET 8; uses FCM HTTP v1 API (legacy server key deprecated).
- Guard `FirebaseApp.Create` — check `FirebaseApp.DefaultInstance == null` before create (test host may restart).
- Initialize once in `FirebasePushNotificationSender` static ctor or hosted startup — **not** per message.
- **Do not** add Node firebase-admin or raw REST without SDK — follow existing C# infrastructure patterns (`IEmailSender`).

### Testing requirements

- Unit: `dotnet test tests/api.unit --filter PushDelivery`
- Integration: `dotnet test tests/api.integration --filter PushDelivery`
- Regression: `dotnet test tests/api.integration --filter "FullyQualifiedName~Notification|CourtReminder|CourtMiss|TravelClaimNotification"`
- Mobile: `npm test -w apps/mobile -- pushNotificationHandlers`
- Manual: Firebase console test message → device with prod config; verify tap opens claim detail

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 7 Story 7.2]
- [Source: `_bmad-output/planning-artifacts/architecture.md` §5.6 — FCM/APNs unified service]
- [Source: `_bmad-output/planning-artifacts/epics.md` FR-19 — push for field roles]
- [Source: `_bmad-output/implementation-artifacts/7-1-device-token-registration-and-notification-store.md`]
- [Source: `_bmad-output/implementation-artifacts/6-4-claim-status-notifications.md`]
- [Source: `apps/api/Infrastructure/Notifications/NotificationService.cs`]
- [Source: `apps/api/Jobs/CourtReminderBackgroundService.cs`]
- [Source: `apps/api/Infrastructure/Email/FakeEmailSender.cs` — fake sender pattern]
- [Source: `apps/mobile/src/screens/notifications/NotificationsListScreen.tsx` — `handleNotificationPress`]

## Dev Agent Record

### Agent Model Used

Claude (dev-story)

### Debug Log References

- Integration tests require Docker/Testcontainers — not run in this session (Docker unavailable).

### Completion Notes List

- Implemented `PushDeliveryService` with role/channel gating, dev-stub token skip, multi-device fan-out, and stale token cleanup.
- Wired push dispatch to intervention overdue, court reminder, court miss, and travel claim approve/return producers after `SaveChangesAsync`.
- Refactored `Create*ForSave` notification helpers to return entities for push dispatch.
- Added `FakePushNotificationSender` + `FirebasePushNotificationSender` with DI toggle via `PushNotificationsOptions`.
- Mobile: shared `notificationNavigation.ts`, FCM tap handlers via `navigationRef`, travel claim deep link to More stack.
- Unit tests: 5 passed (`PushDelivery`). Mobile tests: 4 passed (pushNotificationHandlers + App.test).
- Integration test `PushDeliveryApiTests` added; run when Docker available.

### File List

- apps/api/Infrastructure/Notifications/PushNotificationsOptions.cs (new)
- apps/api/Infrastructure/Notifications/IPushNotificationSender.cs (new)
- apps/api/Infrastructure/Notifications/FakePushNotificationSender.cs (new)
- apps/api/Infrastructure/Notifications/FirebasePushNotificationSender.cs (new)
- apps/api/Infrastructure/Notifications/PushDeliveryService.cs (new)
- apps/api/Infrastructure/Notifications/PushNotificationChannelMapper.cs (new)
- apps/api/Infrastructure/Notifications/NotificationService.cs (modified)
- apps/api/Jobs/CourtReminderBackgroundService.cs (modified)
- apps/api/Jobs/CourtMissEscalationBackgroundService.cs (modified)
- apps/api/Jobs/InterventionOverdueBackgroundService.cs (modified)
- apps/api/Infrastructure/TravelClaims/TravelClaimService.cs (modified)
- apps/api/Program.cs (modified)
- apps/api/MidiKaval.Api.csproj (modified)
- tests/api.unit/PushDeliveryServiceTests.cs (new)
- tests/api.unit/MidiKaval.Api.UnitTests.csproj (modified)
- tests/api.integration/PushDeliveryApiTests.cs (new)
- tests/api.integration/AuthWebApplicationFactory.cs (modified)
- apps/mobile/src/navigation/navigationRef.ts (new)
- apps/mobile/src/services/notifications/notificationNavigation.ts (new)
- apps/mobile/src/services/devices/pushNotificationHandlers.ts (new)
- apps/mobile/src/App.tsx (modified)
- apps/mobile/src/screens/notifications/NotificationsListScreen.tsx (modified)
- apps/mobile/__tests__/pushNotificationHandlers.test.ts (new)
- apps/mobile/__tests__/App.test.tsx (modified)
- README.md (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)

## Change Log

- 2026-06-20: Story 7.2 created — FCM push delivery service wired to existing notification producers, preference/channel gating, stale token cleanup, fake sender for tests, minimal mobile tap handler; visit push producer explicitly deferred (no job exists).
- 2026-06-20: Validated — 11 fixes (ForSave return types, DbContext device query, nested mobile nav, mark-read on tap, integration token format, epic visit reconciliation, etc.).
- 2026-06-20: Implemented — FCM push delivery, producer wiring, unit/mobile tests, README; integration tests deferred pending Docker.
- 2026-06-20: Code review patches — narrowed stale-token detection, foreground payload parse, push tap error handling.
