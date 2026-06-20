# Story Validation Report — 5.3 Court Reminder Background Job

**Story:** `5-3-court-reminder-background-job`  
**Validated:** 2026-06-19 (pass 1 + pass 2)  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (17 fixes applied)

---

## Summary

Story 5.3 scopes API-only court reminder delivery (24h window, in-app + email, dedup, audit) and mirrors Story 4.5 intervention overdue job patterns. Two validation passes addressed gaps that could cause **duplicate emails**, **coordinator-only partial delivery**, **non-retryable SMTP failures**, **wrong Program.cs registration**, or **audit attribution errors**.

| Check | Result |
|-------|--------|
| Epic 5.3 / FR-16 / SM-3 alignment | Pass |
| Mirror `InterventionOverdueBackgroundService` | Pass |
| Scope boundary (no UI, no 5.4 escalation) | Pass |
| Pass 1: active users, email dedupe, audit actor, failure retry | **Fixed** |
| Pass 2: skip entire sitting on bad assignee | **Fixed** |
| Pass 2: email-then-SaveChanges delivery order | **Fixed** |
| Pass 2: `Program.cs` `!IsTesting()` / `!IsDevelopment()` placement | **Fixed** |
| Pass 2: direct `db.AuditEvents.Add` (not CourtSittingService) | **Fixed** |
| Pass 2: join `cases` for crime/ST in email | **Fixed** |
| Pass 2: `IEmailSender` on runner DI | **Fixed** |
| Pass 2: notification list API test + schedule helper | **Fixed** |

---

## Pass 1 — Critical Issues (applied)

1. **Deactivated assignee/coordinators** — AC1 active-user guards; skip with log, no dedup on failure.
2. **Partial delivery / dedup lock** — AC6b retry-safe delivery failure handling.
3. **Coordinator = assignee duplicate emails** — AC2 recipient dedupe.

## Pass 1 — Enhancements (applied)

4. System job audit: `ActorUserId = null`, `SubjectUserId = assignee.
5. Eligibility query joins `users` + active coordinator filter.
6. Integration test: deactivated assignee exclusion.
7. Skip assignee email when empty.
8. `UsersSchemaTests` task wording (column-only migration).

---

## Pass 2 — Critical Issues (applied)

### 9. AC 6b “single SaveChanges + emails” not implementable

Emails are external to EF transactions.

**Fix:** AC6b + dedup table — send emails first; on success, one `SaveChanges` (in-app + audit + dedup). SaveChanges failure after emails leaves dedup unset (rare duplicate email acceptable).

### 10. Inactive/unassigned assignee — partial coordinator delivery ambiguous

FR-16 requires field-worker notification; coordinator-only reminders are inconsistent.

**Fix:** AC1 — skip entire sitting (no emails, no dedup) when `assigned_worker_id` null or assignee inactive.

### 11. Program.cs registration placement

Intervention job registers inside `!IsTesting()`; hosted service only when `!IsDevelopment()`.

**Fix:** AC7 + tasks — mirror exact `Program.cs` placement.

---

## Pass 2 — Enhancements (applied)

### 12. Job audit cannot use `CourtSittingService.AddAuditEvent`

Private method requires HTTP actor context.

**Fix:** Runner writes `db.AuditEvents.Add(...)` directly with `ActorUserId = null`.

### 13. Eligibility query must join `cases`

Email body needs `crimeNumber` / `stNumber`.

**Fix:** Dev notes query filters — join cases, `AssignedWorkerId != null`.

### 14. `IEmailSender` on `CourtReminderJobRunner`

First bulk-email background job; intervention runner has no email dependency.

**Fix:** Tasks — explicit runner constructor DI.

### 15. Runner + options + hosted service in one file

Matches `InterventionOverdueBackgroundService.cs` layout.

**Fix:** File structure + tasks.

### 16. Notification list API assertion in tests

Mirror `NotificationsAndOverdueJobTests` list step.

**Fix:** Tasks + integration test recipe step 7.

### 17. `SetCourtSittingScheduledAtUtcAsync` test helper

No clock-adjustment helper exists; use DB update pattern like interventions.

**Fix:** Tasks + recipe step 9.

---

## Verdict

Story is **implementation-ready**. No OpenAPI/client changes expected.

**Next:** `bmad-dev-story` on `5-3-court-reminder-background-job`, then `bmad-code-review` when in `review`.
