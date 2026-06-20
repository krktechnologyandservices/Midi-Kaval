---

baseline_commit: NO_VCS

---



# Story 4.5: Interventions UI and Overdue Job



Status: done



## Story



As a **Case Worker**,

I want overdue intervention alerts and case-detail intervention management,

so that nothing stalls (FR-14, Flow 5, UX-DR9).



*Scope: **Web + mobile interventions UI** on case detail (consume Story **4.4** API). **Minimal in-app notification store + daily overdue job** (subset of Epic 7 — full push delivery deferred to Story **7.2**, notification bell UI to Story **7.4**). Do **not** re-implement interventions CRUD API beyond overdue/notification hooks.*



## Acceptance Criteria



1. **Given** web case detail (`/cases/:id`)  

   **When** the page loads  

   **Then** an **Interventions** section lists `GET /api/v1/cases/{id}/interventions`  

   **And** each row shows direction, category, priority, status, due/provided dates, assignee, overdue styling when `Needed` + `Open` + past `dueAtUtc`  

   **And** empty state, loading, error+retry mirror notes timeline patterns



2. **Given** web case detail with case read access  

   **When** I add an intervention  

   **Then** form captures direction, categoryName, description, priority, status (default Open), assignee, direction-specific dates  

   **And** submit calls `POST /api/v1/cases/{id}/interventions`  

   **And** list refreshes on success; validation errors surface via `extractErrorMessage`



3. **Given** an intervention row on web  

   **When** I update status and outcome  

   **Then** `PATCH /api/v1/cases/{id}/interventions/{id}` is called  

   **And** overdue styling clears when terminal status + outcome recorded



4. **Given** mobile case detail (`CaseDetailPlaceholderScreen`)  

   **When** the screen loads  

   **Then** interventions list + add/update flows mirror web capabilities with mobile layout (row chips, compact form)  

   **And** pull-to-refresh reloads interventions  

   **And** overdue rows use critical accent styling



5. **Given** a `Needed` intervention with `Open` status and `dueAtUtc` in the past  

   **When** the daily overdue job runs  

   **Then** an in-app notification is created for `assignedStaffUserId` with type `intervention.overdue`  

   **And** duplicate notifications are suppressed (`overdue_notified_at_utc` on intervention)  

   **And** push delivery is **logged/stubbed** until Story 7.2 (no FCM in this story)



6. **Given** overdue notification exists  

   **When** assignee calls `GET /api/v1/notifications`  

   **Then** unread items include intervention overdue entries with case/intervention ids for deep linking  

   **And** `PATCH /api/v1/notifications/{id}/read` marks read



7. **Given** assignee updates intervention to terminal status (`Completed` or `Cancelled`) with outcome  

   **When** PATCH succeeds  

   **Then** related overdue notifications for that intervention are marked read (clears alert per Flow 5)



8. **Given** OpenAPI contract  

   **When** story ships  

   **Then** snapshot exported and `@midi-kaval/api-client` regenerated for notification types  

   **And** integration tests cover job dedup, notification list, UI service calls (web unit + API integration)



## Tasks / Subtasks



- [x] **API — notification store (minimal 7.1 slice)** (AC: 5–7)

  - [x] `in_app_notifications` table + entity + migration

  - [x] `overdue_notified_at_utc` on `interventions` for job dedup

  - [x] `NotificationService` — create, list, mark read, resolve on intervention update

  - [x] `NotificationsController` — GET list, PATCH read

  - [x] Register services in `Program.cs`



- [x] **API — overdue job** (AC: 5)

  - [x] `InterventionOverdueBackgroundService` (daily timer; disabled in Development)

  - [x] Integration tests for job + dedup + notification creation



- [x] **Web UI** (AC: 1–3)

  - [x] Extend `case.models.ts` + `CaseApiService` with intervention types/methods

  - [x] `CaseInterventionsComponent` on case detail

  - [x] Unit tests



- [x] **Mobile UI** (AC: 4)

  - [x] Extend `case.models.ts` + `CaseApiService`

  - [x] Interventions section on `CaseDetailPlaceholderScreen`

  - [x] Jest tests



- [x] **OpenAPI + api-client** (AC: 8)

  - [x] Export snapshot; regenerate and build api-client

  - [x] Integration tests for notifications API



- [x] **Docs**

  - [x] README — notifications endpoints + overdue job note



### Review Findings



- [x] [Review][Patch] `UsersSchemaTests` missing `in_app_notifications` table [`UsersSchemaTests.cs:54-97`] — migration adds table but expected list and TRUNCATE omit it; schema test will fail

- [x] [Review][Patch] Terminal status update without `outcome` does not clear overdue notifications [`InterventionService.cs:276`, `case-interventions.component.ts:168`] — AC7/Flow 5 require outcome; API validates outcome on terminal status; resolve gate unchanged

- [x] [Review][Patch] Web `submitUpdate` allows `Completed`/`Cancelled` without outcome [`case-interventions.component.ts:162-175`] — client validation blocks submit without outcome

- [x] [Review][Defer] Mobile interventions UI not implemented (AC4) — story status `in-progress`; remaining scope per tasks

- [x] [Review][Defer] Integration tests missing for overdue job, notifications API, resolve-on-update — story tasks unchecked

- [x] [Review][Defer] Web `case-interventions.component.spec.ts` missing — story tasks unchecked

- [x] [Review][Defer] README notifications endpoints + job note missing — story tasks unchecked

- [x] [Review][Patch] Mobile `listFieldWorkers` parses `envelope.data.items` but API returns array at `envelope.data` [`CaseApiService.ts:189-194`] — fixed: use `envelope.data ?? []` matching web client
- [x] [Review][Patch] README RBAC table omits notifications endpoints [`README.md:155-165`] — added GET/PATCH notifications rows
- [x] [Review][Defer] Notification bell UI not in scope — Story 7.4; in-app store + list API satisfy AC5–6 for v1
- [x] [Review][Defer] Integration test for `PATCH` terminal status without outcome → 400 — update path validated in web/mobile UI; explicit API negative test optional
- [x] [Review][Dismiss] Interventions gated on successful case detail load (web) — same pattern as notes timeline; acceptable
- [x] [Review][Dismiss] `CreateAsync` allows `Provided`+`Completed` without outcome — Story 4.4 historical-record path; update path enforces outcome per AC7
- [x] [Review][Dismiss] Push delivery stubbed in job log — documented deferral to Story 7.2 per AC5



## Dev Notes



### READ FIRST



1. **Story 4.4 API is complete** — use existing intervention endpoints; extend only for overdue notification hooks.

2. **Push deferred** — log `"Push deferred to Story 7.2"` in job; in-app notification satisfies AC5 centre requirement until 7.4 bell UI.

3. **UX-DR9** — row layout mirrors future court sitting rows: date/status chip, overdue critical accent.

4. **Flow 5** — notification payload includes `caseId` + `interventionId` for navigation.

5. **No offline** — interventions online-only like case notes.



### References



- Story 4.3 — notes timeline UI patterns (web component + mobile screen structure)

- Story 4.4 — intervention DTOs and validation

- EXPERIENCE.md Flow 5 — overdue intervention flow

- architecture.md §5.6 — intervention overdue daily job



## Dev Agent Record



### Agent Model Used



Composer



### Completion Notes List



- API: `in_app_notifications`, `NotificationService`, `NotificationsController`, `InterventionOverdueJobRunner` + background service (non-Development only). Push logged as deferred to Story 7.2.

- Web: `CaseInterventionsComponent` integrated on case detail; `CaseApiService` intervention + notification methods; unit tests (4 cases).

- Mobile: interventions section on `CaseDetailPlaceholderScreen` with list/add/update, overdue styling, pull-to-refresh; `patchApi` on `AuthSessionService`; Jest test for add intervention.

- Integration: `NotificationsAndOverdueJobTests` — job dedup, list, mark read, resolve-on-update.

- README: notifications endpoints + overdue job documentation.

- Code review patches applied: schema test, terminal outcome validation (API + web).



### File List



- apps/api/Domain/Entities/InAppNotification.cs

- apps/api/Domain/Entities/Intervention.cs (overdue_notified_at_utc)

- apps/api/Infrastructure/Notifications/NotificationService.cs

- apps/api/Infrastructure/Notifications/NotificationEventTypes.cs

- apps/api/Infrastructure/Persistence/InAppNotificationConfiguration.cs

- apps/api/Infrastructure/Cases/InterventionService.cs

- apps/api/Jobs/InterventionOverdueBackgroundService.cs

- apps/api/Models/Notifications/NotificationDtos.cs

- apps/api/Controllers/V1/NotificationsController.cs

- apps/api/Migrations/*_AddInAppNotificationsAndOverdueFlag.cs

- apps/api/Program.cs

- apps/web/src/app/features/cases/interventions/*

- apps/web/src/app/features/cases/models/case.models.ts

- apps/web/src/app/features/cases/services/case-api.service.ts

- apps/web/src/app/features/cases/detail/case-detail-placeholder.component.*

- apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx

- apps/mobile/src/services/cases/case.models.ts

- apps/mobile/src/services/cases/CaseApiService.ts

- apps/mobile/src/services/auth/AuthSessionService.ts

- apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx

- tests/api.integration/NotificationsAndOverdueJobTests.cs

- tests/api.integration/CaseCreateTests.cs (notification helpers)

- tests/api.integration/UsersSchemaTests.cs

- packages/api-client/

- README.md



## Change Log



- 2026-06-19: Story 4.5 created — interventions UI + overdue job + minimal notification store.

- 2026-06-19: Completed mobile UI, integration tests, web unit tests, README; story ready for review.
- 2026-06-19: Code review — fixed mobile listFieldWorkers envelope parsing; README notifications RBAC rows. Story done.


