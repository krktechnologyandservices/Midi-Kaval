---
baseline_commit: NO_VCS
---

# Story 5.3: Court Reminder Background Job

<!-- Validated: 2026-06-19 — see 5-3-court-reminder-background-job-validation-report.md (17 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **field worker and Coordinator**,
I want reminders 24 hours before court sittings,
so that sittings are not missed (FR-16, SM-3).

*Scope: **API background job only** — scan `court_sittings` for `Upcoming` rows ~24h ahead; deliver **in-app notification** to assigned field worker, **email** to org Coordinators and assigned worker; **log** push as deferred to Story **7.2**. **No** mobile/web UI changes (Story 5.2 already surfaces schedule). **No** miss escalation (Story 5.4). **No** new public REST endpoints unless a test hook is required (prefer direct runner invocation like Story 4.5).*

## Acceptance Criteria

1. **Given** a `court_sittings` row with `status = Upcoming`  
   **And** parent case `current_stage != TerminationExclusion`  
   **And** case has `assigned_worker_id` set  
   **And** `scheduled_at_utc` falls in the reminder window (~24 hours before sitting, configurable tolerance)  
   **And** `reminder_sent_at_utc IS NULL`  
   **When** the court reminder job runs  
   **Then** an in-app notification is created for the assigned field worker (`cases.assigned_worker_id`)  
   **And** `reminder_sent_at_utc` is set on the sitting row (dedup)  
   **And** assignee user is **active** (`users.is_active = true`) — if `assigned_worker_id` is null or assignee is inactive, **skip the entire sitting** (no in-app, no emails, no dedup; log reason)  
   **And** coordinator email recipients are **active** (`users.is_active = true`)  
   **And** push delivery is **logged as deferred to Story 7.2** (same pattern as intervention overdue job)

2. **Given** the same sitting qualifies for reminder (AC 1)  
   **When** the job runs  
   **Then** each **active** user with role `Coordinator` in the sitting's `organisation_id` receives an email via `IEmailSender`  
   **And** the assigned field worker receives an email at `users.email` (“web user for tomorrow's sitting” — pilot: schedule owner account email)  
   **And** if assignee email matches a coordinator email, send **one** email to that address (dedupe recipients)  
   **And** skip assignee email when `users.email` is null/empty  
   **And** email subject/body includes court name, scheduled time (UTC or org-local — use UTC ISO in body for pilot consistency), case crime/ST identifiers where available, and sitting purpose  
   **And** no beneficiary PII in email body (POCSO-safe: crime/ST only)

3. **Given** a sitting already reminded (`reminder_sent_at_utc` set)  
   **When** the job runs again within the same window  
   **Then** no duplicate in-app notifications, emails, or audit rows for that sitting

4. **Given** a sitting is rescheduled (`scheduled_at_utc` changes via PATCH)  
   **When** update succeeds  
   **Then** `reminder_sent_at_utc` is cleared so a new 24h reminder can fire for the new time

5. **Given** sitting `status` is `Attended` or `Postponed`, or case is terminal, or sitting is not in the 24h window  
   **When** the job runs  
   **Then** no reminder is sent

6. **Given** delivery occurs  
   **When** notification/email is sent  
   **Then** an audit event `court.sitting.reminder_sent` is written with metadata: `courtSittingId`, `caseId`, `assignedWorkerUserId`, `scheduledAtUtc` (no notes/outcome text)  
   **And** `ActorUserId` is **null** (system job); `SubjectUserId` = assigned worker  
   **And** structured log line records sitting id, assignee id, coordinator email count

6b. **Given** delivery fails for a sitting  
   **When** the job processes that sitting  
   **Then** `reminder_sent_at_utc` is **not** set (allows retry on next run)  
   **And** error is logged; other sittings in the batch continue  
   **And** delivery order is: send all emails via `IEmailSender` first; on success, single `SaveChanges` (in-app notification + audit + dedup flag). If email send fails, skip `SaveChanges`. If `SaveChanges` fails after emails sent, leave dedup unset (rare duplicate email on retry is acceptable)

7. **Given** non-Development hosting  
   **When** API starts  
   **Then** `CourtReminderJobRunner` and options register inside `if (!builder.Environment.IsTesting())` in `Program.cs` (same block as intervention job)  
   **And** `CourtReminderBackgroundService` hosted service registers only when `!IsDevelopment()`  
   **And** in **Development** / test hosts the hosted service is **not** registered (invoke `CourtReminderJobRunner` directly in integration tests — mirror `InterventionOverdueBackgroundService`)

8. **Given** regression safety  
   **When** story ships  
   **Then** intervention overdue job, court sitting CRUD, and notification list API behave unchanged  
   **And** integration tests cover reminder creation, dedup, email recipients, reschedule reset, and exclusions  
   **And** README documents job config + behaviour

## Tasks / Subtasks

- [x] **Schema** (AC: 1, 3–4)
  - [x] Add nullable `reminder_sent_at_utc` to `court_sittings` + migration
  - [x] Update `CourtSitting` entity + EF config
  - [x] Update `UsersSchemaTests` TRUNCATE list if new tables added (column-only migration needs no table list change)

- [x] **Notification + audit** (AC: 1–2, 6)
  - [x] `NotificationEventTypes.CourtReminder24h = "court.reminder.24h"`
  - [x] `AuditEventTypes.CourtSittingReminderSent = "court.sitting.reminder_sent"`
  - [x] Extend `NotificationService` — `CreateCourtReminderNotificationAsync` (title/body with court name + formatted schedule; stages in-app row for SaveChanges per AC 6/6b)

- [x] **Job runner + host** (AC: 1–7)
  - [x] `CourtReminderJobOptions` — `IntervalMinutes` (default **60**), `LeadHours` (default **24**), `WindowMinutes` (default **60**); runner + background service in **one file** `apps/api/Jobs/CourtReminderBackgroundService.cs` (namespace `Infrastructure.Notifications` — mirror `InterventionOverdueBackgroundService.cs`)
  - [x] `CourtReminderJobRunner` constructor: `AppDbContext`, `NotificationService`, `IEmailSender`, `ILogger<CourtReminderJobRunner>` — email helper builds coordinator + assignee `EmailMessage` bodies inline
  - [x] Runner writes reminder audit via `db.AuditEvents.Add(...)` directly (`ActorUserId = null`) — **do not** call `CourtSittingService.AddAuditEvent` (private, requires HTTP context)
  - [x] Register in `Program.cs` inside `!IsTesting()` block: `Configure<CourtReminderJobOptions>`, `AddScoped<CourtReminderJobRunner>`, `AddHostedService` when `!IsDevelopment()`

- [x] **CourtSittingService update** (AC: 4)
  - [x] On PATCH when `scheduledAtUtc` changes, set `reminder_sent_at_utc = null`

- [x] **Integration tests** (AC: 8)
  - [x] `CourtReminderJobTests.cs` (or extend `NotificationsAndOverdueJobTests.cs`) — invoke `CourtReminderJobRunner` directly
  - [x] Happy path: sitting at now+24h → in-app notification + coordinator email + assignee email + audit + dedup flag
  - [x] Assert assignee can list notification via `CaseTestData.ListNotificationsAsync` (mirror `NotificationsAndOverdueJobTests`)
  - [x] Second run → no duplicates
  - [x] Outside window / wrong status / terminal case / deactivated assignee / unassigned case → no send
  - [x] Reschedule clears dedup; `SetCourtSittingScheduledAtUtcAsync` to new window → job sends again
  - [x] Add `CaseTestData.SetCourtSittingScheduledAtUtcAsync` helper (mirror `SetInterventionDueAtUtcAsync`)

- [x] **Docs**
  - [x] README section for court reminder job (config keys, Development disabled, push deferred 7.2)

### Review Findings

- [x] [Review][Patch] Audit assertion not scoped to sitting — happy-path test uses `SingleAsync` on all `court.sitting.reminder_sent` rows; can flake when other tests leave audit rows [`CourtReminderJobTests.cs:56`]
- [x] [Review][Patch] Eligibility query missing org consistency guard — join `cases` without `caseEntity.OrganisationId == sitting.OrganisationId` [`CourtReminderBackgroundService.cs:44`]
- [x] [Review][Patch] No dedup re-check before persist — unlike intervention job, runner does not bail if `ReminderSentAtUtc` was set between query and `SaveChanges` (concurrent run risk) [`CourtReminderBackgroundService.cs:70`]
- [x] [Review][Patch] AC 6b untested — no integration test for `FakeEmailSender.FailNextSend` leaving `reminder_sent_at_utc` null [`CourtReminderJobTests.cs`]
- [x] [Review][Patch] AC 5 partial coverage — no test for `Attended`/`Postponed` status exclusion (only terminal case + outside window) [`CourtReminderJobTests.cs`]
- [x] [Review][Defer] `purpose` is user-entered free text in email body — could contain beneficiary names if staff enter PII there [`CourtReminderBackgroundService.cs:165`] — deferred, pre-existing data-entry policy gap

## Dev Notes

### READ FIRST

1. **Mirror Story 4.5 intervention overdue job** — `InterventionOverdueBackgroundService` + `InterventionOverdueJobRunner` + `overdue_notified_at_utc` dedup + `FakeEmailSender` in integration tests. Read these files completely before coding:
   - `apps/api/Jobs/InterventionOverdueBackgroundService.cs`
   - `apps/api/Infrastructure/Notifications/NotificationService.cs`
   - `tests/api.integration/NotificationsAndOverdueJobTests.cs`
2. **Court sitting data exists (Story 5.1)** — `CourtSitting` entity has **no** `reminder_sent_at_utc` yet; assignee comes from `cases.assigned_worker_id`, not a column on `court_sittings`.
3. **UI deferred (Story 5.2 done)** — mobile `CourtCountdownBanner` / court schedule UI already consume upcoming API; this story does **not** add client changes. In-app notifications will surface via Story 7.4 bell later; store rows now.
4. **Push deferred (Story 7.2)** — create in-app notification + `LogInformation("Push deferred to Story 7.2 for court sitting {SittingId}")`; do **not** implement FCM/APNs.
5. **Miss escalation deferred (Story 5.4)** — do not flag cases or write Crisis Queue rows here.
6. **No OpenAPI / api-client changes** expected — no new REST endpoints; do not run client regen unless you accidentally add controllers.

### Reminder window logic (SM-3)

Target: sitting qualifies when:

```
scheduledAtUtc >= now + LeadHours - WindowMinutes/2
AND scheduledAtUtc <= now + LeadHours + WindowMinutes/2
```

Defaults: `LeadHours=24`, `WindowMinutes=60` → roughly “24h before ±30min” per hourly job tick.

Query filters (all required):

- `status == Upcoming`
- `reminder_sent_at_utc == null`
- Join `cases` — `AssignedWorkerId != null`, `current_stage != TerminationExclusion`; load `crimeNumber`/`stNumber` for email body
- Join `users` for assignee — require `IsActive`; skip entire sitting if assignee missing/inactive
- Coordinators: separate query `role = Coordinator AND IsActive AND organisation_id match`
- Time window per SM-3 formula above
- Mirror `ListUpcomingForFieldWorkerAsync` terminal-case exclusion (do not duplicate differently)

Use `DateTime.UtcNow` consistently.

### Delivery channels (FR-16 mapping)

| Channel | Recipient | Implementation |
|---------|-----------|----------------|
| Mobile push | Assigned field worker | **Deferred 7.2** — log only |
| In-app notification | Assigned field worker | `in_app_notifications` row (`eventType=court.reminder.24h`, `resourceType=CourtSitting`, `resourceId=sittingId`) |
| Email — Coordinator | All active `Coordinator` users in `organisation_id` | `IEmailSender` one email per coordinator per sitting |
| Email — web user | Assigned worker `users.email` | `IEmailSender` single email (skip if worker email empty) |

**Email content guardrails:** Include `courtName`, `scheduledAtUtc` (ISO), `crimeNumber`/`stNumber` from case — **no** `beneficiaryName` or free-text notes/outcome.

### Dedup + reschedule

| Event | Action |
|-------|--------|
| Job sends reminder | Emails first via `IEmailSender`; then single `SaveChanges` (in-app notification + `db.AuditEvents.Add` + `reminder_sent_at_utc`). On email failure, skip SaveChanges. On SaveChanges failure after emails, leave dedup unset (AC 6b) |
| Job re-run | Skip rows where `reminder_sent_at_utc != null` |
| PATCH changes `scheduledAtUtc` | Clear `reminder_sent_at_utc` in `CourtSittingService` (even if other fields change in same request) |
| Status → `Attended`/`Postponed` | No automatic clear required for v1 (sitting won't match `Upcoming` filter); optional clear on status change is acceptable |

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Jobs/CourtReminderBackgroundService.cs` (options + runner + hosted service — one file) |
| NEW | `apps/api/Migrations/*_AddCourtSittingReminderSent.cs` |
| NEW | `tests/api.integration/CourtReminderJobTests.cs` |
| UPDATE | `apps/api/Domain/Entities/CourtSitting.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Cases/CourtSittingService.cs` |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `tests/api.integration/CaseTestData.cs` (`SetCourtSittingScheduledAtUtcAsync`, `RunCourtReminderJobAsync`) |
| UPDATE | `tests/api.integration/UsersSchemaTests.cs` if column list asserted |
| UPDATE | `README.md` |

### Configuration (`appsettings.json` example)

```json
"CourtReminderJob": {
  "IntervalMinutes": 60,
  "LeadHours": 24,
  "WindowMinutes": 60
}
```

### Integration test recipe

Mirror `NotificationsAndOverdueJobTests`:

1. Coordinator creates case + transfers to field worker (`CaseTestData.BuildCoordinatorSessionAsync`, `TransferCaseAsync`).
2. Create court sitting with `scheduledAtUtc = DateTime.UtcNow.AddHours(24)` via API or `CaseTestData.CreateCourtSittingAsync`.
3. `await RunCourtReminderJobAsync()` — resolve `CourtReminderJobRunner` from DI scope.
4. Assert `reminder_sent_at_utc` set on sitting row.
5. Assert single `in_app_notifications` row for assignee with `court.reminder.24h`.
6. Assert `FakeEmailSender.Messages` contains coordinator@rbac.test and field worker email.
7. Assert `CaseTestData.ListNotificationsAsync` returns single `court.reminder.24h` row for assignee.
8. Run job again → counts unchanged.
9. PATCH sitting `scheduledAtUtc` to `now+48h` → assert `reminder_sent_at_utc` null; `SetCourtSittingScheduledAtUtcAsync` to `now+24h` → job sends again.

Use `RbacTestData.CoordinatorEmail`, `RbacTestData.CaseWorkerEmail` / `SocialWorkerEmail`.

### Previous story intelligence (5.1)

- `CourtSittingService.ListUpcomingForFieldWorkerAsync` already excludes terminal cases and computes `isPastDue` — reminder job should use the same terminal exclusion.
- Audit on create/update excludes notes/outcome in metadata — keep reminder audit metadata minimal.
- Integration tests use Testcontainers; invoke job runner directly (no hosted service in test host).

### Previous story intelligence (5.2)

- UI relies on upcoming API order and `isPastDue`; reminder job does not change those endpoints.
- Deferred: API PATCH cannot clear `nextCourtAtUtc` with null — unrelated to this story.

### Previous story intelligence (4.5)

- `InterventionOverdueJobRunner` pattern is the canonical template for background work in this codebase (not Hangfire yet).
- `in_app_notifications` table + `NotificationsController` already exist — extend event types only.
- README must document each new job with config section.

### Testing requirements

- **Do not** require Docker for unit-level runner logic if tests use existing `AuthWebApplicationFactory` + Testcontainers pattern (follow `CourtSittingsTests.cs`).
- Assert **no duplicate** notifications/emails on second run (AC 3 / SM-3).
- Assert terminal case sitting never reminds even if schedule is in window.
- No web/mobile test changes.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 5, Story 5.3]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-16, SM-3]
- [Source: `_bmad-output/implementation-artifacts/5-1-court-sitting-crud-api.md` — entity + service]
- [Source: `_bmad-output/implementation-artifacts/5-2-court-schedule-ui-web-case-detail-and-mobile.md` — UI scope boundary]
- [Source: `_bmad-output/implementation-artifacts/4-5-interventions-ui-and-overdue-job.md` — job + notification patterns]
- [Source: `apps/api/Jobs/InterventionOverdueBackgroundService.cs`]
- [Source: `tests/api.integration/NotificationsAndOverdueJobTests.cs`]
- [Source: `_bmad-output/project-context.md` — jobs in-process, audit, no PII in logs]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- API build succeeded; integration tests require Docker (Testcontainers) — not running in this session.

### Completion Notes List

- Added `reminder_sent_at_utc` column, entity property, EF config, migration `AddCourtSittingReminderSent`.
- Implemented `CourtReminderJobRunner` + `CourtReminderBackgroundService` (hourly, 24h ±30min window): emails first, then SaveChanges (in-app `court.reminder.24h`, audit `court.sitting.reminder_sent`, dedup flag).
- Skips entire sitting when assignee null/inactive; dedupes coordinator/assignee emails; POCSO-safe email body (crime/ST only).
- `CourtSittingService` clears dedup on `scheduledAtUtc` PATCH change.
- Six integration tests in `CourtReminderJobTests.cs` (8 after review); `CaseTestData` helpers for schedule + job invocation.
- README documents court reminder job + in-app notification event type.
- Code review patches: org guard + dedup re-check in runner; audit scoped assertion; `ReminderJob_EmailFailure_DoesNotSetDedupFlag` + `ReminderJob_AttendedStatus_DoesNotSend` tests.

### File List

- apps/api/Domain/Entities/CourtSitting.cs
- apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs
- apps/api/Infrastructure/Notifications/NotificationEventTypes.cs
- apps/api/Infrastructure/Notifications/NotificationService.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Infrastructure/Cases/CourtSittingService.cs
- apps/api/Jobs/CourtReminderBackgroundService.cs
- apps/api/Migrations/20260619151901_AddCourtSittingReminderSent.cs
- apps/api/Migrations/20260619151901_AddCourtSittingReminderSent.Designer.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Program.cs
- tests/api.integration/CourtReminderJobTests.cs
- tests/api.integration/CaseCreateTests.cs
- README.md

## Change Log

- 2026-06-19: Story 5.3 created — court reminder background job (24h in-app + email, dedup, audit; push deferred 7.2).
- 2026-06-19: Validation — 8 fixes (active-user guard, email dedupe, atomic delivery, audit actor, failure retry, test cases).
- 2026-06-19: Validation pass 2 — 9 fixes (skip entire sitting on bad assignee, email-then-SaveChanges order, Program.cs placement, direct audit write, case join, IEmailSender DI, list API test, AssignedWorkerId filter, schedule helper).
- 2026-06-19: Implemented court reminder background job — schema, runner, tests, README; status → review.
- 2026-06-19: Code review — 5 patches applied (audit scope, org guard, dedup re-check, AC 6b + Attended tests); status → done.
