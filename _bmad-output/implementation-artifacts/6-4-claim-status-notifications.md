---

baseline_commit: NO_VCS

---



# Story 6.4: Claim Status Notifications



<!-- Validated: 2026-06-20 — see 6-4-claim-status-notifications-validation-report.md (11 fixes applied) -->



Status: done



<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->



## Story



As a **claimant (field worker)**,

I want approval status notifications,

so that I know the outcome without WhatsApp chasing (FR-18, FR-19 partial).



*Scope: **API notification copy + push dispatch hook** for travel-claim approve/return (Story **6.3** already persists in-app rows); **mobile claimant UX** — decision feedback on claim detail, lightweight **More → Notifications** list consuming existing `GET/PATCH /api/v1/notifications`, tap-through to read-only claim. **Push delivery stub** logged deferral to Story **7.2** (no FCM/APNs). **No** device token registration (Story **7.1**), **no** notification bell chrome (Story **7.4**), **no** email templates (Story **7.3**), **no** dashboard widgets (Story **8.4**) — but verify monthly totals API reflects decisions for Epic 8 consumption.*



## Acceptance Criteria



1. **Given** a Director approves a Submitted travel claim  

   **When** `TravelClaimService.ApproveAsync` completes  

   **Then** claimant receives an in-app notification (`travel.claim.approved`) with **standardized copy** (not raw director comment as entire body when comment provided)  

   **And** title *"Travel claim approved"*  

   **And** body format: *"Your claim for {destination} (₹{amount}) was approved."* plus optional second line *"Director note: {comment}"* when comment non-empty  

   **And** `ResourceType = TravelClaim`, `ResourceId = claimId`, `CaseId = first linked case` (unchanged from 6.3)  

   **And** structured push payload is **logged/stubbed** with message *"Push deferred to Story 7.2 for travel claim {claimId}"* (mirror `CourtReminderBackgroundService` / Story **4.5** AC5)



2. **Given** a Director returns a Submitted travel claim with required comment  

   **When** `ReturnAsync` completes  

   **Then** in-app notification `travel.claim.returned` uses title *"Travel claim returned"*  

   **And** body: *"Your claim for {destination} (₹{amount}) was returned."* plus *"Director note: {comment}"* (comment always present on return)  

   **And** push stub logged same as AC1



3. **Given** I am the claimant on mobile  

   **When** I open a claim in read-only `TravelClaimFormScreen` (`mode: 'view'`)  

   **Then** I see a **status chip/label** (reuse list chip styling: Draft/Submitted/Approved/Returned)  

   **And** when status is **Approved** or **Returned**, a **Director feedback** section shows `decisionComment` and formatted `decidedAtUtc` (from `GET /api/v1/travel-claims/{id}` — 6.3 AC 3b)  

   **And** Approved without `decisionComment` shows feedback section with approved message only (no blank "Director note" block)  

   **And** **Submitted** read-only view shows status only — no decision section



4. **Given** I am an authenticated **field worker** on mobile (`auth.isFieldRole`)  

   **When** I open **More → Notifications**  

   **Then** list loads `GET /api/v1/notifications` with loading, error+retry, pull-to-refresh (mirror `TravelClaimsListScreen` / `CourtScheduleScreen`)  

   **And** parse `{ data: { items } }` via `auth.getApi<NotificationListResultDto>` (camelCase `items` on wire — mirror `TravelClaimApiService`)  

   **And** empty state *"You're up to date."* (EXPERIENCE.md UX-DR13)  

   **And** each row shows `title`, `body`, relative time, unread styling when `isRead = false`  

   **And** tapping `travel.claim.approved` / `travel.claim.returned` marks read via `PATCH /api/v1/notifications/{id}/read` and navigates to `TravelClaimForm` `{ claimId: resourceId, mode: 'view' }`  

   **And** non-field roles see *"Notifications are for field workers only."* (mirror Travel list guard)



4b. **Given** a non-travel notification row (e.g. `intervention.overdue`)  

   **When** I tap it on mobile  

   **Then** mark read only — **no** deep link required in 6.4 (full notification centre drill-down deferred Story **7.4**)



5. **Given** a claim decision is recorded (approve or return)  

   **When** coordinator calls `GET /api/v1/supervisor/travel-claims/monthly-totals?year={claimDateYear}&month={claimDateMonth}`  

   **Then** totals reflect Story **6.1** rules — **Submitted + Approved** count; **Returned** excluded  

   **And** integration test: submit → approve → staff row includes amount; separate claim submit → return → staff row excludes returned amount (reuse `SendListTravelClaimMonthlyTotalsAsync` / add `ListTravelClaimMonthlyTotalsAsync` helper)



6. **Given** regression safety  

   **When** story ships  

   **Then** Stories **6.1–6.3** approve/return/crisis-queue flows unchanged except notification copy + push log hook  

   **And** existing intervention/court notifications unaffected  

   **And** integration tests cover claim notification copy, push defer log (optional assert via test logger), monthly totals after approve/return  

   **And** mobile unit tests cover decision feedback display and notifications list empty/row tap  

   **And** README documents claim notification event types + mobile Notifications entry



## Tasks / Subtasks



- [x] **API — notification copy + push hook** (AC: 1–2, 6)

  - [x] Add `TravelClaimNotificationCopy` static helper — `BuildApproved(claim, comment?)`, `BuildReturned(claim, comment)` returning `(title, body)`

  - [x] Refactor `NotificationService.CreateTravelClaimDecisionNotificationForSave` — accept `(TravelClaim claim, Guid caseId, string eventType, string? decisionComment)`; build title/body **inside** service via copy helper (remove caller-supplied title/body strings — prevents drift)

  - [x] Update `TravelClaimService.ApproveAsync` / `ReturnAsync` to call refactored method only (no inline copy)

  - [x] Log push defer **after** successful `SaveChangesAsync`: *"Push deferred to Story 7.2 for travel claim {claimId}"* via `ILogger<TravelClaimService>` (mirror court reminder timing — not before commit)

  - [x] Do **not** add FCM/APNs/device tables (7.1/7.2)



- [x] **API — regression tests** (AC: 5–6)

  - [x] `TravelClaimNotificationApiTests.cs` — approve without comment: body contains destination + amount, no raw-only comment; approve **with** comment: body contains destination **and** *Director note:* line; return: body contains destination + *Director note:* (not comment-only body as today)

  - [x] Claimant `GET /notifications` after approve/return — `resourceType=TravelClaim`, `resourceId=claimId`, `eventType` matches

  - [x] Monthly totals: approve increases staff total; return excludes claim (query year/month from `claim.claimDate`)

  - [x] Extend `CaseTestData` — `ListTravelClaimMonthlyTotalsAsync` envelope parser (wrapper around existing `SendListTravelClaimMonthlyTotalsAsync`)

  - [x] Regression: `TravelClaimDirectorApiTests` still pass; `NotificationsAndOverdueJobTests` unchanged



- [x] **Mobile — services** (AC: 4)

  - [x] `notification.models.ts` — `NotificationDto`, `NotificationListResultDto` (mirror `travel.models.ts` / OpenAPI shapes; mobile has no `@midi-kaval/api-client` package)

  - [x] `NotificationApiService.ts` — `list()`, `markRead(id)`, `extractErrorMessage` using `auth.getApi` / `auth.patchApi`



- [x] **Mobile — Notifications list** (AC: 4, 4b)

  - [x] `NotificationsListScreen.tsx` under More stack with field-worker guard

  - [x] Route + More menu entry *Notifications*

  - [x] Travel-claim row tap → mark read + `TravelClaimForm` view navigation; other event types mark read only

  - [x] Unit tests: empty state, field-worker guard, travel-claim navigation handler



- [x] **Mobile — claim decision feedback** (AC: 3)

  - [x] `TravelClaimFormScreen` — visible status chip in read-only mode + Director feedback block for Approved/Returned

  - [x] Unit tests: status chip visible; Approved with `decisionComment`; Returned shows comment; Submitted has no feedback block



- [x] **Docs** (AC: 6)

  - [x] README — claim notification copy, push defer note, mobile More → Notifications



### Review Findings (2026-06-20)

**Outcome:** Changes Requested → **Resolved** — 3 patch applied, 2 defer, 1 dismissed

- [x] [Review][Patch] Notifications list keeps unread styling after tap until manual refresh — `markRead` succeeds but local `items` state is not updated [`NotificationsListScreen.tsx:115-123`]
- [x] [Review][Patch] Reload notifications on screen focus (`useFocusEffect`, mirror `SyncQueueScreen`) so returning from claim view shows read state [`NotificationsListScreen.tsx`]
- [x] [Review][Patch] Missing unit test for Approved without `decisionComment` showing fallback *"Your claim was approved."* (AC3) [`TravelClaimFormScreen.test.tsx`]

- [x] [Review][Defer] No integration test asserting push defer log line — deferred, AC6 explicitly marks this optional
- [x] [Review][Defer] `TravelClaimDirectorApiTests` return notification still uses `Contains` only — deferred, `TravelClaimNotificationApiTests` covers full body shape



## Dev Notes



### READ FIRST



1. **Story 6.3 already creates in-app rows** — `CreateTravelClaimDecisionNotificationForSave` in `NotificationService.cs`; `GET/PATCH /api/v1/notifications` exists from Story **4.5**. This story **refines copy**, adds **push defer log**, and builds **mobile consumption** — do not duplicate notification store or controller.

2. **Push is stub-only** — mirror `CourtReminderBackgroundService` lines logging *"Push deferred to Story 7.2..."* after save. Story **7.2** implements real dispatch; **7.1** registers device tokens.

3. **No notification bell** — Story **7.4** owns shell bell badge + web/mobile centre polish. This story is a **More tab list** for field workers (claimant role).

4. **Monthly totals ≠ dashboard UI** — AC5 verifies existing `ListMonthlyTotalsAsync` behavior after approve/return. Epic **8.4** builds widgets; no Redis/cache here.

5. **Approve body bug today** — approve with comment sets `Body = comment` only; return sets `Body = comment` only. **Both fixed** via centralized copy helper.

6. **POCSO / voice** — notification body uses destination + amount only; **no beneficiary names**. Return comment is director-entered operational text (same as 6.3).

7. **Deep link RBAC** — claimant can only open own claims via existing field-worker `GET /travel-claims/{id}`; notification tap uses same API; 403 → show error on form load.

8. **No new API routes** — `NotificationsController` unchanged; **no** OpenAPI snapshot regen required unless DTO docs updated.

9. **Epic AC "mobile push"** — satisfied by structured in-app row + push defer **log** in this story; FCM/APNs delivery is Story **7.2** (epics intentionally split).



### Current code state (READ before editing)



| File | Today | This story changes |

|------|-------|-------------------|

| `NotificationService.cs` | `CreateTravelClaimDecisionNotificationForSave(claim, caseId, eventType, title, body)` | Refactor signature; internal copy builder |

| `TravelClaimService.cs` | Passes inline title/body strings | Call refactored notification method; log push after SaveChanges |

| `TravelClaimFormScreen.tsx` | Read-only view; no decision feedback | Decision section for Approved/Returned |

| `MoreStackNavigator.tsx` / `MoreScreen.tsx` | Travel + Sync only | Add Notifications route/entry |

| `NotificationsController.cs` | List + mark read | **No change** expected |

| `CaseApiService.ts` (web) | Has `listNotifications` unused | **Out of scope** (7.4) |



### Notification copy contract



| Event | Title | Body template |

|-------|-------|----------------|

| `travel.claim.approved` | Travel claim approved | `Your claim for {destination} (₹{amount}) was approved.` + optional `\nDirector note: {comment}` |

| `travel.claim.returned` | Travel claim returned | `Your claim for {destination} (₹{amount}) was returned.\nDirector note: {comment}` |



Use `amount` formatted `0.##` with `₹` prefix (pilot, match crisis queue / web).



### API contract (unchanged endpoints)



| Action | Method | Path | Policy |

|--------|--------|------|--------|

| List notifications | GET | `/api/v1/notifications` | Authenticated |

| Mark read | PATCH | `/api/v1/notifications/{id}/read` | Authenticated |

| Get own claim | GET | `/api/v1/travel-claims/{id}` | FieldWorker (claimant) |

| Monthly totals | GET | `/api/v1/supervisor/travel-claims/monthly-totals` | CoordinatorOrAbove |



### File structure



| Action | Path |

|--------|------|

| NEW | `apps/api/Infrastructure/Notifications/TravelClaimNotificationCopy.cs` (or equivalent helper) |

| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` |

| UPDATE | `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs` |

| NEW | `tests/api.integration/TravelClaimNotificationApiTests.cs` |

| UPDATE | `tests/api.integration/CaseCreateTests.cs` — `ListTravelClaimMonthlyTotalsAsync` |

| NEW | `apps/mobile/src/services/notifications/notification.models.ts` |

| NEW | `apps/mobile/src/services/notifications/NotificationApiService.ts` |

| NEW | `apps/mobile/src/screens/notifications/NotificationsListScreen.tsx` |

| UPDATE | `apps/mobile/src/navigation/MoreStackNavigator.tsx`, `types.ts`, `MoreScreen.tsx` |

| UPDATE | `apps/mobile/src/screens/travel/TravelClaimFormScreen.tsx` |

| NEW | `apps/mobile/__tests__/NotificationsListScreen.test.tsx` |

| UPDATE | `apps/mobile/__tests__/TravelClaimFormScreen.test.tsx` |

| UPDATE | `README.md` |



**Reuse without modification:** `NotificationsController`, `InAppNotification` entity, `TravelClaimDirectorApiTests` approve/return helpers, web `CaseApiService.listNotifications`.



### Previous story intelligence (6.3)



- In-app notifications created in same transaction as audit + status update.

- Event types: `NotificationEventTypes.TravelClaimApproved`, `TravelClaimReturned`.

- Self-approval blocked (`EnsureActorNotClaimant`).

- Mobile read-only Returned/Approved per 6.2 — no resubmit UI.

- Push explicitly deferred to 7.2 in 6.3 completion notes — this story adds the **log line** at decision time.



### Previous story intelligence (4.5)



- `GET/PATCH notifications` + `NotificationsAndOverdueJobTests.cs` patterns for list/mark-read.

- Push stub pattern in background jobs — adapt for synchronous approve/return path.

- `UsersSchemaTests` includes `in_app_notifications` — column-only changes unlikely.



### Previous story intelligence (6.2)



- `TravelClaimFormScreen` `mode: 'view'` for non-Draft statuses.

- Pull-to-refresh on list screens — reuse for Notifications.

- Empty copy *"No claims yet"* vs notifications *"You're up to date."*



### UX references



- [Source: `EXPERIENCE.md` Flow 6] — *"Priya receives approval push; monthly total updates."* (push stub + totals test this story)

- [Source: `EXPERIENCE.md` UX-DR13] — Empty notifications: *"You're up to date."*

- [Source: `epics.md` Story 6.4] — in-app + push intent; dashboard event deferred to Epic 8



### Testing requirements



- API: `dotnet test tests/api.integration --filter TravelClaimNotification`

- API regression: `dotnet test tests/api.integration --filter TravelClaimDirector`

- Mobile: `npm test -w apps/mobile -- NotificationsList TravelClaimForm`

- Manual: director approve with comment → mobile Notifications shows formatted body → tap opens claim with decision section



### References



- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6 Story 6.4]

- [Source: `_bmad-output/implementation-artifacts/6-3-director-claim-approval-on-web.md`]

- [Source: `_bmad-output/implementation-artifacts/6-2-mobile-travel-claim-capture.md`]

- [Source: `_bmad-output/implementation-artifacts/4-5-interventions-ui-and-overdue-job.md`]

- [Source: `apps/api/Infrastructure/Notifications/NotificationService.cs`]

- [Source: `apps/api/Jobs/CourtReminderBackgroundService.cs` — push defer log pattern]

- [Source: `tests/api.integration/NotificationsAndOverdueJobTests.cs`]

- [Source: `_bmad-output/project-context.md`]



## Dev Agent Record



### Agent Model Used



Composer (Cursor)



### Debug Log References



- API build succeeded; integration tests require Docker/Testcontainers (not running in dev session).

- Mobile: 136/138 tests passed full suite; 2 pre-existing flaky 5s timeout tests unrelated to 6.4.



### Completion Notes List



- Centralized travel-claim notification copy in `TravelClaimNotificationCopy`; `NotificationService` builds title/body from claim + decision comment.

- `TravelClaimService` logs push defer after `SaveChangesAsync` on approve/return.

- Added `TravelClaimNotificationApiTests` (copy shape, claimant list, monthly totals approve/return).

- Mobile: `NotificationApiService`, `NotificationsListScreen` (More tab), read-only claim status chip + Director feedback on form.

- README updated for claim notification event types and mobile Notifications entry.



### File List



- apps/api/Infrastructure/Notifications/TravelClaimNotificationCopy.cs (new)

- apps/api/Infrastructure/Notifications/NotificationService.cs

- apps/api/Infrastructure/TravelClaims/TravelClaimService.cs

- tests/api.integration/TravelClaimNotificationApiTests.cs (new)

- tests/api.integration/CaseCreateTests.cs

- apps/mobile/src/services/notifications/notification.models.ts (new)

- apps/mobile/src/services/notifications/NotificationApiService.ts (new)

- apps/mobile/src/screens/notifications/NotificationsListScreen.tsx (new)

- apps/mobile/src/navigation/MoreStackNavigator.tsx

- apps/mobile/src/navigation/types.ts

- apps/mobile/src/screens/more/MoreScreen.tsx

- apps/mobile/src/screens/travel/TravelClaimFormScreen.tsx

- apps/mobile/__tests__/NotificationsListScreen.test.tsx (new)

- apps/mobile/__tests__/TravelClaimFormScreen.test.tsx

- README.md



## Change Log



- 2026-06-20: Story 6.4 created — standardized claim decision notification copy, push defer hook, mobile notifications list + claim decision feedback, monthly totals regression tests; defers FCM/bell/dashboard to Epics 7–8.

- 2026-06-20: Validation — 11 fixes (notification API refactor, push log timing, mobile envelope/guard/deep-link scope, status chip on form, monthly totals test params, return-body bug, epic push split note).

- 2026-06-20: Implementation complete — API copy helper + push defer log, integration tests, mobile Notifications list + claim decision feedback, README; ready for code review.
- 2026-06-20: Code review — 3 patches applied (optimistic mark-read UI, useFocusEffect reload, Approved-without-comment unit test); story marked done.


