---
baseline_commit: NO_VCS
---

# Story 5.4: Court Miss Escalation and Crisis Queue Feed

<!-- Validated: 2026-06-19 — see 5-4-court-miss-escalation-and-crisis-queue-feed-validation-report.md (10 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Project Coordinator**,
I want missed court sittings escalated automatically,
so that I intervene before harm (FR-17, UJ-2).

*Scope: **API + background job** — hourly scan for **past-due** `Upcoming` court sittings; **flag case**, **notify Coordinators** (in-app + email), expose **court-miss rows** on `GET /api/v1/supervisor/crisis-queue` with `severity=critical` / `badgeLabel=Court miss` (UX-DR3 `crisis-row-critical` token mapping deferred to Story **8.2** UI). **Pre-feed** crisis queue data for Epic 8 — **do not** implement visit/handoff/claim rows, Redis cache, or full prioritization merge (Story **8.1**). **No** web/mobile UI changes. Push deferred to Story **7.2**. Mirror Story **5.3** job patterns (`CourtMissEscalationJobRunner`, direct runner in tests).*

## Acceptance Criteria

1. **Given** a `court_sittings` row with `status = Upcoming`  
   **And** `scheduled_at_utc < now` (UTC)  
   **And** parent case `current_stage != TerminationExclusion`  
   **And** `miss_escalated_at_utc IS NULL`  
   **When** the court miss escalation job runs  
   **Then** `miss_escalated_at_utc` is set on the sitting (dedup)  
   **And** parent case `court_miss_flagged_at_utc` is set to `now` when absent; on re-escalation after reschedule cycle, set again if flag was cleared  
   **And** each **active** `Coordinator` in the sitting's `organisation_id` receives **one** in-app notification row (`court.miss.escalated`, `UserId` = coordinator — **not** assignee)  
   **And** each active Coordinator receives an email via `IEmailSender` (dedupe addresses; POCSO-safe: crime/ST only in body)  
   **And** push delivery is **logged as deferred to Story 7.2**  
   **And** audit event `court.sitting.miss_escalated` is written (`ActorUserId` null; metadata: `courtSittingId`, `caseId`, `assignedWorkerUserId` (omit or null when unassigned), `scheduledAtUtc` — no notes/outcome)  
   **And** structured log line records sitting id, case id, coordinator notification count

1b. **Given** email send fails for a sitting  
   **When** the job processes that sitting  
   **Then** delivery order is: send all coordinator emails first; on success, single `SaveChanges` (one in-app notification **per active Coordinator** + audit + sitting dedup + case flag)  
   **And** if email send fails, skip `SaveChanges` (no notifications, audit, or dedup)  
   **And** if `SaveChanges` fails after emails sent, leave dedup unset (rare duplicate email on retry acceptable)  
   **And** error is logged; other sittings in the batch continue

2. **Given** the same sitting already escalated (`miss_escalated_at_utc` set)  
   **When** the job runs again  
   **Then** no duplicate notifications, emails, or audit rows for that sitting

3. **Given** a Coordinator or Director with org access  
   **When** `GET /api/v1/supervisor/crisis-queue`  
   **Then** response envelope `{ data: { items: CrisisQueueItemDto[] }, meta: { requestId, totalCount } }`  
   **And** items include **only court-miss rows** in v1 (past-due `Upcoming` sittings with `miss_escalated_at_utc` set, terminal cases excluded)  
   **And** each row has `severity = critical`, `badgeLabel = "Court miss"`, `rowType = court_miss`, `caseId`, `courtSittingId`, `assignedWorkerUserId` (nullable), crime/ST identifiers, `scheduledAtUtc`, composed `title`/`detail` (no beneficiary PII)  
   **And** rows sorted by `scheduledAtUtc` ascending (oldest miss first)  
   **And** field workers receive **403**

4. **Given** a sitting on the crisis queue (`miss_escalated_at_utc` set, past-due `Upcoming`)  
   **When** PATCH updates `status` to `Attended` or `Postponed`  
   **Then** sitting no longer appears in crisis-queue results  
   **And** if the case has **no** other past-due `Upcoming` escalated sittings, `cases.court_miss_flagged_at_utc` is cleared

5. **Given** a sitting was escalated  
   **When** PATCH changes `scheduledAtUtc` to a **future** time (still `Upcoming`)  
   **Then** `miss_escalated_at_utc` is cleared (allows re-escalation if date passes again)  
   **And** case miss flag is recalculated (cleared when no remaining qualifying sittings)

6. **Given** sitting not past-due, or status `Attended`/`Postponed`, or case terminal, or not yet escalated  
   **When** crisis-queue endpoint is called  
   **Then** that sitting does not appear as a court-miss row

7. **Given** non-Development hosting  
   **When** API starts  
   **Then** `CourtMissEscalationJobRunner` registers inside `if (!builder.Environment.IsTesting())` in `Program.cs`  
   **And** `CourtMissEscalationBackgroundService` registers only when `!IsDevelopment()`  
   **And** integration tests invoke `CourtMissEscalationJobRunner` directly

8. **Given** regression safety  
   **When** story ships  
   **Then** court reminder job (5.3), court sitting CRUD, and notification list API behave unchanged  
   **And** integration tests cover escalation, dedup, crisis-queue feed, flag clear on resolve, reschedule reset, and exclusions  
   **And** README documents miss escalation job + crisis-queue endpoint (court-miss rows only until Epic 8)

## Tasks / Subtasks

- [x] **Schema** (AC: 1, 4–5)
  - [x] Add nullable `miss_escalated_at_utc` to `court_sittings` + migration
  - [x] Add nullable `court_miss_flagged_at_utc` to `cases` + migration
  - [x] Update `CourtSitting` + `Case` entities + EF configs
  - [x] Column-only migration — no `UsersSchemaTests` table list change

- [x] **Notification + audit** (AC: 1–2)
  - [x] `NotificationEventTypes.CourtMissEscalated = "court.miss.escalated"`
  - [x] `AuditEventTypes.CourtSittingMissEscalated = "court.sitting.miss_escalated"`
  - [x] `NotificationService.CreateCourtMissEscalationNotificationForSave` — one staged in-app row **per active Coordinator** `userId` (not assignee); atomic with SaveChanges per AC 1/1b)

- [x] **Escalation job** (AC: 1–2, 7)
  - [x] `CourtMissEscalationJobOptions` — `IntervalMinutes` (default **60**)
  - [x] `CourtMissEscalationJobRunner` + `CourtMissEscalationBackgroundService` in one file `apps/api/Jobs/CourtMissEscalationBackgroundService.cs`
  - [x] Constructor: `AppDbContext`, `NotificationService`, `IEmailSender`, `ILogger`, `IOptions`
  - [x] Query: past-due `Upcoming`, `miss_escalated_at_utc == null`, terminal exclusion, org join; re-check `miss_escalated_at_utc` before SaveChanges; skip inactive coordinators for email/notification
  - [x] Emails then single `SaveChanges` (notifications + audit + sitting dedup + case flag)
  - [x] Register in `Program.cs` (mirror `CourtReminderJobRunner`)

- [x] **Crisis queue API** (AC: 3, 6)
  - [x] `SupervisorController` or `CrisisQueueController` — `GET /api/v1/supervisor/crisis-queue`
  - [x] `CrisisQueueService.ListAsync` — live query court-miss rows only; `[Authorize(Policy = Policies.CoordinatorOrAbove)]`
  - [x] DTOs: `CrisisQueueItemDto`, `CrisisQueueListResultDto` with `severity`, `badgeLabel`, `rowType`, case/sitting/worker ids, title/detail

- [x] **CourtSittingService + Case flag** (AC: 4–5)
  - [x] On status → `Attended`/`Postponed`: recalculate `court_miss_flagged_at_utc` via shared helper
  - [x] On `scheduledAtUtc` change to future: clear `miss_escalated_at_utc` + recalculate case flag
  - [x] Extract `CourtMissFlagService` or internal helper on `CourtSittingService` — `RecalculateCaseCourtMissFlagAsync(caseId)`

- [x] **Integration tests** (AC: 8)
  - [x] `CourtMissEscalationJobTests.cs` — invoke runner directly
  - [x] Happy path: past-due sitting → flag + notification + coordinator email + audit + dedup
  - [x] Second run → no duplicates
  - [x] `GET /supervisor/crisis-queue` returns critical court-miss row; field worker **403**; Director **200** (CoordinatorOrAbove)
  - [x] PATCH Attended → row gone, flag cleared
  - [x] Reschedule to future → dedup cleared; re-escalate after past-due again
  - [x] Terminal case / not past-due / not escalated / unassigned case still escalates → exclusions
  - [x] `FakeEmailSender.FailNextSend` → no dedup flag (AC 1b)
  - [x] **Past-due setup:** API rejects past `scheduledAtUtc` for `Upcoming` create/update — use `SetCourtSittingScheduledAtUtcAsync` in tests
  - [x] `CaseTestData.SetCourtSittingScheduledAtUtcAsync` reuse; add `RunCourtMissEscalationJobAsync` helper

- [x] **Docs + OpenAPI**
  - [x] README: miss escalation job config, crisis-queue endpoint (court-miss only v1)
  - [x] Regenerate `packages/api-client` after new endpoint (start API locally, then `npm run generate:api-client` + `npm run build -w @midi-kaval/api-client` — see root README)

### Review Findings

- [x] [Review][Patch] Pre-save eligibility re-check too narrow — `stillEligible` only tests `miss_escalated_at_utc == null`; does not re-verify `Upcoming`, past-due, or non-terminal case before `SaveChanges` (race if sitting resolved concurrently) [`CourtMissEscalationBackgroundService.cs:92`]
- [x] [Review][Patch] AC 6 / story testing gap — no `Attended`/`Postponed` status exclusion test (mirror `ReminderJob_AttendedStatus_DoesNotSend` from 5.3) [`CourtMissEscalationJobTests.cs`]
- [x] [Review][Patch] Story testing requirements — no deactivated-coordinator exclusion test (job should skip inactive coordinators for email/notification) [`CourtMissEscalationJobTests.cs`]
- [x] [Review][Patch] AC 4 partial — `Postponed` path not covered; only `Attended` tested for crisis-queue removal and flag clear [`CourtMissEscalationJobTests.cs`]
- [x] [Review][Defer] `purpose` is free text in escalation email body — same beneficiary-PII data-entry risk as court reminder job [`CourtMissEscalationBackgroundService.cs:151`] — deferred, pre-existing policy gap

## Dev Notes

### READ FIRST

1. **Mirror Story 5.3 court reminder job** — same file layout, email-then-SaveChanges, dedup column, `FakeEmailSender`, direct runner in tests:
   - `apps/api/Jobs/CourtReminderBackgroundService.cs`
   - `apps/api/Jobs/CourtMissEscalationBackgroundService.cs` (NEW — copy pattern)
   - `tests/api.integration/CourtReminderJobTests.cs`
2. **Story 5.3 boundary** — reminder job fires ~24h **before** sitting; miss escalation fires **after** `scheduled_at_utc` passes. Independent dedup columns (`reminder_sent_at_utc` vs `miss_escalated_at_utc`).
3. **Epic 8 boundary** — Story **8.1** adds visit/handoff/claim rows, Redis 30s cache, global severity sort (`critical` → `warning` → `info` → `neutral`). This endpoint returns **court-miss rows only** until 8.1 extends the merge. Story **8.2** wires web Crisis Queue UI + `crisis-row-critical` CSS.
4. **No mobile/web UI** — `crisis-queue-page.component.ts` remains placeholder until 8.2.
5. **Push deferred (7.2)** — in-app + email only for Coordinators.

### Escalation eligibility query

Past-due sitting qualifies when:

```
status == Upcoming
AND scheduledAtUtc < DateTime.UtcNow
AND miss_escalated_at_utc == null
AND case.current_stage != TerminationExclusion
AND case.organisation_id == sitting.organisation_id
```

Unlike reminder job: **do not require** `assigned_worker_id` or active assignee — coordinator must see miss even on unassigned cases.

### Crisis queue read query (AC 3)

Return rows where:

```
miss_escalated_at_utc != null
AND status == Upcoming
AND scheduledAtUtc < now
AND case not terminal
AND sitting.organisation_id == actor organisation
```

Compose `title` e.g. `{crimeNumber} — court sitting past due` and `detail` e.g. `{assigneeEmail ?? 'Unassigned'} · {courtName}` — **no** `beneficiaryName`.

| Field | Value |
|-------|-------|
| `severity` | `critical` |
| `badgeLabel` | `Court miss` |
| `rowType` | `court_miss` |

### Case flag recalculation

| Event | Action |
|-------|--------|
| Job escalates sitting | Set `court_miss_flagged_at_utc = now` (set when null; **re-set** when null after prior clear + new escalation) |
| Sitting resolved (`Attended`/`Postponed`) | If no other past-due escalated `Upcoming` sittings on case → `court_miss_flagged_at_utc = null` |
| `scheduledAtUtc` patched to future | Clear `miss_escalated_at_utc`; recalculate flag |
| Crisis queue read | Live query — no materialized queue table in v1 |

### Delivery (FR-17)

| Channel | Recipient | Implementation |
|---------|-----------|----------------|
| In-app | Active Coordinators | `court.miss.escalated`, `resourceType=CourtSitting` |
| Email | Active Coordinators | `IEmailSender`, dedupe by address |
| Push | Coordinators | **Deferred 7.2** — log only |
| Crisis queue | Coordinators/Directors | `GET /supervisor/crisis-queue` |

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Jobs/CourtMissEscalationBackgroundService.cs` |
| NEW | `apps/api/Migrations/*_AddCourtMissEscalation.cs` |
| NEW | `apps/api/Controllers/V1/SupervisorController.cs` (or `CrisisQueueController.cs`) |
| NEW | `apps/api/Infrastructure/Supervisor/CrisisQueueService.cs` |
| NEW | `apps/api/Models/Supervisor/CrisisQueueDtos.cs` |
| NEW | `tests/api.integration/CourtMissEscalationJobTests.cs` |
| UPDATE | `apps/api/Domain/Entities/CourtSitting.cs` |
| UPDATE | `apps/api/Domain/Entities/Case.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs` |
| UPDATE | `apps/api/Infrastructure/Persistence/CaseConfiguration.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Audit/AuditEventTypes.cs` |
| UPDATE | `apps/api/Infrastructure/Cases/CourtSittingService.cs` |
| UPDATE | `apps/api/Program.cs` |
| UPDATE | `tests/api.integration/CaseTestData.cs` |
| UPDATE | `README.md` |
| UPDATE | `packages/api-client` (regen after endpoint) |

### Configuration (`appsettings.json` example)

```json
"CourtMissEscalationJob": {
  "IntervalMinutes": 60
}
```

### Integration test recipe

1. Coordinator creates case (+ optional transfer to field worker; add **unassigned** case path — escalation still fires).
2. Create `Upcoming` sitting with **future** `scheduledAtUtc` via API (`now + 7 days`).
3. **`SetCourtSittingScheduledAtUtcAsync(sittingId, now - 1 hour)`** — API rejects past dates for `Upcoming` create/update (`CourtSittingService` 422).
4. `await RunCourtMissEscalationJobAsync()`.
5. Assert `miss_escalated_at_utc`, `cases.court_miss_flagged_at_utc`, one in-app row **per coordinator**, email, audit.
6. `GET /api/v1/supervisor/crisis-queue` as coordinator → single `court_miss` / `critical` row; repeat as **Director** → 200.
7. PATCH sitting `Attended` + outcome → queue empty, flag null.
8. Second job run → no duplicate side effects.
9. `FakeEmailSender.FailNextSend = true` → run job → no dedup (AC 1b).

Use `RbacTestData.CoordinatorEmail`, `AuthTestData` / `RbacAuthorizationTests` Director session helpers.

### Previous story intelligence (5.3)

- Email-then-`SaveChanges`, dedup re-check before persist, org join on cases, skip patterns for terminal cases.
- `reminder_sent_at_utc` cleared on reschedule — mirror with `miss_escalated_at_utc`.
- `CourtReminderJobTests` patterns for exclusions and `FakeEmailSender.FailNextSend` (add failure test for AC delivery order if applicable).

### Previous story intelligence (5.2)

- `isPastDue` on upcoming API uses `Upcoming` + `scheduledAtUtc < now` — crisis queue uses same past-due definition plus escalation dedup flag.

### Previous story intelligence (5.1)

- `CourtSittingService.UpdateAsync` already clears `reminder_sent_at_utc` on schedule change — extend for miss escalation columns.
- Audit metadata excludes notes/outcome — keep escalation audit minimal.

### Testing requirements

- **Past-due setup:** never use API to set past `scheduledAtUtc` on `Upcoming` — use `SetCourtSittingScheduledAtUtcAsync` only.
- Assert crisis-queue **403** for field worker (`CaseWorker` / `SocialWorker`); **200** for Director.
- Assert unassigned case still escalates and appears in crisis queue.
- Assert **no duplicate** coordinator notifications on second job run.
- Assert `FailNextSend` leaves `miss_escalated_at_utc` null.
- Assert terminal case never escalates or appears in queue.
- Run `dotnet test tests/api.integration --filter CourtMissEscalation` with Docker (Testcontainers).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 5 Story 5.4, Epic 8 Story 8.1]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-12/prd.md` — FR-17, UJ-2]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.6 court miss hourly job, crisis-queue endpoint]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 2, crisis-row-critical]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — `crisis-row-critical` tokens]
- [Source: `_bmad-output/specs/spec-kaval-online/field-and-court-operations.md` — CAP-8]
- [Source: `_bmad-output/implementation-artifacts/5-3-court-reminder-background-job.md`]
- [Source: `apps/api/Jobs/CourtReminderBackgroundService.cs`]
- [Source: `_bmad-output/project-context.md` — dedicated crisis-queue endpoint, court miss hourly]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

- Integration tests require Docker/Testcontainers (`dotnet test tests/api.integration --filter CourtMissEscalation`).
- api-client regen via `API_OPENAPI_FILE=openapi-snapshot.json` after manual snapshot update.

### Completion Notes List

- Hourly `CourtMissEscalationJobRunner`: past-due `Upcoming` sittings → coordinator emails, per-coordinator in-app notifications, case flag, audit, dedup (`miss_escalated_at_utc`).
- `GET /api/v1/supervisor/crisis-queue` returns court-miss rows (`critical` / `Court miss`) for Coordinator/Director; field workers 403.
- `CourtSittingService` clears escalation on future reschedule; `CourtMissFlagService` recalculates case flag on resolve/reschedule.
- 9 integration tests in `CourtMissEscalationJobTests.cs`; API + test project build clean.
- Code review patches: full pre-save eligibility re-check; `Attended` exclusion, deactivated coordinator, and `Postponed` flag-clear tests (12 tests total).

### File List

- `apps/api/Domain/Entities/CourtSitting.cs`
- `apps/api/Domain/Entities/Case.cs`
- `apps/api/Infrastructure/Persistence/CourtSittingConfiguration.cs`
- `apps/api/Infrastructure/Persistence/CaseConfiguration.cs`
- `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs`
- `apps/api/Infrastructure/Notifications/NotificationService.cs`
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs`
- `apps/api/Infrastructure/Cases/CourtMissFlagService.cs`
- `apps/api/Infrastructure/Cases/CourtSittingService.cs`
- `apps/api/Infrastructure/Supervisor/CrisisQueueService.cs`
- `apps/api/Jobs/CourtMissEscalationBackgroundService.cs`
- `apps/api/Controllers/V1/SupervisorController.cs`
- `apps/api/Models/Supervisor/CrisisQueueDtos.cs`
- `apps/api/Migrations/20260619172242_AddCourtMissEscalation.cs`
- `apps/api/Migrations/20260619172242_AddCourtMissEscalation.Designer.cs`
- `apps/api/Migrations/AppDbContextModelSnapshot.cs`
- `apps/api/Program.cs`
- `tests/api.integration/CourtMissEscalationJobTests.cs`
- `tests/api.integration/CaseCreateTests.cs`
- `README.md`
- `packages/api-client/openapi-snapshot.json`
- `packages/api-client/src/generated/api.ts`
- `packages/api-client/dist/**`

## Change Log

- 2026-06-19: Story 5.4 created — court miss escalation job, case flag, minimal crisis-queue API (court-miss rows); UI deferred Epic 8.
- 2026-06-19: Validation — 10 fixes (AC 1b delivery order, per-coordinator notifications, past-due test helper, dedup re-check, deactivated coordinators, audit null assignee, Director/unassigned/email-failure tests, case flag re-set, api-client regen note, Epic 8.1 forward-compat).
- 2026-06-19: Implemented — court miss escalation job, case flag, crisis-queue API, integration tests, README, api-client regen.
- 2026-06-19: Code review — 4 patches applied (eligibility re-check, Attended/deactivated-coordinator/Postponed tests); status → done.
