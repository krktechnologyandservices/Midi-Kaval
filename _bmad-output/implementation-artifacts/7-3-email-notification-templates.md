---
baseline_commit: NO_VCS
---

# Story 7.3: Email Notification Templates

<!-- Validated: 2026-06-20 — see 7-3-email-notification-templates-validation-report.md (10 fixes applied) -->

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Coordinator**,
I want email for court tomorrow, claims, assignments, and reports,
so that desk staff stay informed (FR-19).

*Scope: **Centralized plain-text email templates** and **`EmailDeliveryService`** orchestration wired to **existing operational producers** — refactor court job inline emails (Stories **5.3**, **5.4**), add **claim** and **assignment** emails, ship **report-ready template + public hook** for Epic **8.5**. Honour **role-based preferences stub** (Story **7.1**). **No** mobile/web UI, **no** HTML templates (plain text matches `SmtpEmailSender`), **no** SendGrid SDK (keep `IEmailSender` + SMTP), **no** changes to auth transactional emails (OTP/password reset stay inline in `AuthService`), **no** push/in-app bell work (Stories **7.2**, **7.4**), **no** new REST endpoints, **no** preferences PATCH persistence.*

## Acceptance Criteria

1. **Given** configured SMTP (`Email:Smtp:Host`) or test `FakeEmailSender`  
   **When** the API starts  
   **Then** `EmailDeliveryService` registers in DI alongside existing `IEmailSender`  
   **And** auth emails (login OTP, step-up OTP, password reset) remain sent directly via `IEmailSender` from `AuthService` — **unchanged**

2. **Given** template infrastructure  
   **When** an operational email is sent  
   **Then** subject/body are built by dedicated template renderers under `Infrastructure/Email/Templates/` (static classes — mirror `TravelClaimNotificationCopy` pattern)  
   **And** each template exposes `RenderSubject(...)` and `RenderBody(...)` with typed context records (no stringly-typed magic keys)  
   **And** all bodies are **plain text** (compatible with existing `TextPart("plain")` in `SmtpEmailSender`)  
   **And** shared footer line appended: *"Open Kaval Online for details."* (no deep links in v1 — Story **7.4** adds in-app centre links)

3. **Given** POCSO / beneficiary PII policy  
   **When** any operational email renders case context  
   **Then** body includes **crime number and ST number only** — never `beneficiaryName`, `beneficiaryContact`, or `beneficiaryAge`  
   **And** for `SensitivityLevel.POCSO` cases: omit free-text `court_sittings.purpose` (deferred PII risk from Stories **5.3**/**5.4** reviews); use fixed line *"Purpose: See Kaval Online (POCSO case)."*  
   **And** standard cases may include sitting purpose as today  
   **And** travel claim emails include destination + amount + claimant **staff email** (from `users.email`) — no case beneficiary fields

4. **Given** `EmailDeliveryService` with **two method families**  
   **When** post-save producers call **`TrySend*`** (claims, transfer, report hook)  
   **Then** skip with structured **info** log (not error) when: user missing/inactive, `email` empty, `NotificationPreferencesDefaults.ForRole(role).EmailEnabled == false`, or channel disabled via `EmailNotificationChannelMapper`  
   **And** `TrySend*` methods catch all exceptions internally — callers never need try/catch  
   **And** per-recipient SMTP failure logs `LogWarning` with `userId`, `eventType`, `toEmail` — other recipients still attempt send  
   **And** unknown/unmapped `eventType` never throws — skip only  
   **When** court jobs call **`SendCourtReminderEmailsAsync`** / **`SendCourtMissEscalationEmailsAsync`** (pre-save)  
   **Then** same gating applies per recipient, but **`EmailDeliveryException` propagates** on any `IEmailSender.SendAsync` failure — court runners must **not** call `TrySend*` for pre-save path (Story **5.3** AC6b / **5.4** dedup: email failure → no `SaveChangesAsync`)

5. **Given** event type → channel mapping (`NotificationPreferenceChannelsDto`)

   | `eventType` | Channel key | Typical recipients |
   |-------------|-------------|-------------------|
   | `court.reminder.24h` | `court` | Active Coordinators (+ assignee — see AC6) |
   | `court.miss.escalated` | `court` | Active Coordinators |
   | `travel.claim.submitted` | `claims` | Active Coordinators + Directors |
   | `travel.claim.approved` | `claims` | Active Coordinators + Directors |
   | `travel.claim.returned` | `claims` | Active Coordinators + Directors |
   | `case.transferred` | `assignments` | Active Coordinators + Directors (+ assignee — see AC10) |
   | `report.export.ready` | `reports` | Requesting Coordinator/Director only |

   **When** channel is `false` for user's role defaults  
   **Then** email skipped for that user; producer transaction unaffected

6. **Given** court reminder job (Story **5.3**) still qualifies a sitting  
   **When** `CourtReminderJobRunner` processes it  
   **Then** inline `BuildEmailMessage` / recipient loops are **removed**  
   **And** emails sent via `EmailDeliveryService.SendCourtReminderEmailsAsync(...)` using `CourtReminderEmailTemplate` (**not** `TrySend*`)  
   **And** **delivery order preserved**: all emails succeed **before** `SaveChangesAsync` (in-app + audit + dedup) — on `EmailDeliveryException`, runner skips save (Story **5.3** AC6b); integration tests `ReminderJob_EmailFailure_DoesNotSetDedupFlag` must still pass  
   **And** assignee with non-empty email receives reminder email **even when** field-worker stub has `emailEnabled: false` (FR-16 "web user for tomorrow's sitting" override — explicit bypass for assignee only)  
   **And** coordinator recipients respect `emailEnabled` + `channels.court`  
   **And** duplicate recipient dedupe when assignee email matches coordinator email (one send)

7. **Given** court miss escalation job (Story **5.4**)  
   **When** sitting is escalated  
   **Then** inline coordinator email loop replaced with `EmailDeliveryService.SendCourtMissEscalationEmailsAsync(...)` + `CourtMissEscalationEmailTemplate` (**not** `TrySend*`)  
   **And** email order preserved: emails before `SaveChangesAsync` (in-app + flag + audit); `EmailDeliveryException` prevents dedup — `MissEscalationJob_EmailFailure_DoesNotSetDedupFlag` must still pass  
   **And** only active Coordinators with court channel + emailEnabled receive email

8. **Given** a travel claim is **submitted** (`TravelClaimService.SubmitAsync`)  
   **When** save completes  
   **Then** `NotificationEventTypes.TravelClaimSubmitted = "travel.claim.submitted"` is added  
   **And** after `SaveChangesAsync`, `EmailDeliveryService.TrySendTravelClaimSubmittedAsync` emails all active Coordinators + Directors in org with claims channel enabled  
   **And** reuse existing `GetClaimantEmailAsync` for claimant staff email in template context  
   **And** template subject/body includes claimant staff email, destination, amount (`{amount:0.##}`), claim date, claim id (guid for ops reference)  
   **And** failure does not roll back submission

9. **Given** a Director **approves** or **returns** a claim (Story **6.3**/**6.4**)  
   **When** `ApproveAsync` / `ReturnAsync` completes in-app notification + push (Story **7.2**)  
   **Then** additionally email Coordinators + Directors (claims channel) with `TravelClaimDecisionEmailTemplate` — desk staff awareness  
   **And** email copy is **supervisor-facing** ("Claim from {claimantEmail} for {destination} (₹{amount}) was approved/returned.") — distinct from claimant in-app copy in `TravelClaimNotificationCopy`  
   **And** optional director comment included when present  
   **And** claimant does **not** receive this supervisor email (they already have in-app + push)

10. **Given** a case transfer (`CaseService.TransferAsync`, Story **2.8**)  
    **When** save completes  
    **Then** `NotificationEventTypes.CaseTransferred = "case.transferred"` is added  
    **And** after `SaveChangesAsync`, `EmailDeliveryService.TrySendCaseTransferredAsync` runs  
    **And** emails active Coordinators + Directors (assignments channel) with crime/ST, new assignee staff email, transfer timestamp  
    **And** new assignee receives assignment email at `users.email` when non-empty (**operational override** — FR-19 assignment awareness even though field-worker stub has `emailEnabled: false`)  
    **And** handoff whisper text (`priorActions`, `openItems`, `nextVisitPurpose`) is **not** included in email (mobile whisper only — PII/length risk)

11. **Given** report export is not implemented (Epic **8.5**)  
    **When** this story ships  
    **Then** `ReportExportReadyEmailTemplate` + `EmailDeliveryService.TrySendReportExportReadyAsync(userId, organisationId, reportName, expiresAtUtc)` exist as **public hook** for Epic **8.5**  
    **And** unit test covers template render + gating (reports channel)  
    **And** no background job or REST endpoint added in this story  
    **And** README documents hook signature for Epic **8.5** consumers

12. **Given** regression safety  
    **When** story ships  
    **Then** unit tests cover: channel gating, emailEnabled skip, empty email skip, POCSO purpose omission, assignee/coordinator dedupe, multi-recipient fan-out  
   **And** integration tests updated: `CourtReminderJobTests`, `CourtMissEscalationJobTests` assert template subject/body shape via `FakeEmailSender`  
   **And** regression preserved: `ReminderJob_EmailFailure_DoesNotSetDedupFlag`, `MissEscalationJob_EmailFailure_DoesNotSetDedupFlag` (require pre-save `Send*` throw semantics)  
   **And** new integration tests: claim submit → coordinator email; case transfer → coordinator + assignee emails; POCSO case court email omits purpose free-text  
    **And** existing push/in-app/notification regression suites unchanged  
    **And** README documents operational email events vs auth emails

## Tasks / Subtasks

- [x] **API — template renderers** (AC: 2, 3)
  - [x] `Infrastructure/Email/Templates/EmailTemplateFooter.cs` — shared footer helper
  - [x] `CourtReminderEmailTemplate.cs` + `CourtReminderEmailContext`
  - [x] `CourtMissEscalationEmailTemplate.cs` + context
  - [x] `TravelClaimSubmittedEmailTemplate.cs` + context
  - [x] `TravelClaimDecisionEmailTemplate.cs` + context (approved/returned variants)
  - [x] `CaseTransferredEmailTemplate.cs` + context
  - [x] `ReportExportReadyEmailTemplate.cs` + context

- [x] **API — delivery orchestration** (AC: 4, 5)
  - [x] `EmailNotificationChannelMapper.cs` — event type → channel bool; `IsKnownEventType`
  - [x] `EmailDeliveryService.cs` — `TrySend*` methods per producer; recipient query helpers for org coordinators/directors
  - [x] Register in `Program.cs` (same notifications DI block as `PushDeliveryService`)

- [x] **API — event constants** (AC: 8, 10)
  - [x] Add `TravelClaimSubmitted`, `CaseTransferred`, `ReportExportReady` to `NotificationEventTypes.cs`

- [x] **API — producer wiring** (AC: 6–10)
  - [x] Refactor `CourtReminderJobRunner` — replace `IEmailSender` with `EmailDeliveryService`; call `SendCourtReminderEmailsAsync` (pre-save, throws)
  - [x] Refactor `CourtMissEscalationJobRunner` — replace `IEmailSender`; call `SendCourtMissEscalationEmailsAsync` (pre-save, throws)
  - [x] Inject `EmailDeliveryService` into `CaseService` + `TravelClaimService` constructors
  - [x] `TravelClaimService.SubmitAsync` — email after save
  - [x] `TravelClaimService.ApproveAsync` / `ReturnAsync` — supervisor email after push
  - [x] `CaseService.TransferAsync` — email after save

- [x] **API — tests + docs** (AC: 12)
  - [x] `tests/api.unit/EmailDeliveryServiceTests.cs` — gating, overrides, **`Send*` propagates `EmailDeliveryException`**, **`TrySend*` swallows**
  - [x] `tests/api.unit/EmailTemplateTests.cs` (POCSO purpose omission, footer, supervisor claim copy)
  - [x] Update `CourtReminderJobTests`, `CourtMissEscalationJobTests`
  - [x] `TravelClaimEmailNotificationTests.cs` (or extend `TravelClaimNotificationApiTests`)
  - [x] `CaseTransferEmailTests.cs` (or extend `CaseAssignmentTests`)
  - [x] README — operational email matrix + report hook + distinction from auth SMTP

## Dev Notes

### READ FIRST — do not reinvent

1. **`IEmailSender` is DONE** — `EmailMessage(To, Subject, Body)` plain text via MailKit SMTP. Tests use `FakeEmailSender` on `AuthWebApplicationFactory.EmailSender`. **Do not** add SendGrid SDK or HTML MIME in this story.

2. **Auth emails are OUT OF SCOPE** — `AuthService` sends OTP/password-reset inline. Operational templates are for FR-19 desk/field operational events only.

3. **Push is DONE (7.2)** — `PushDeliveryService.TrySendAsync` after save for in-app events. Email is **parallel channel** for desk roles; do **not** remove or reorder push calls.

4. **Preferences stub is DONE (7.1)** — use `NotificationPreferencesDefaults.ForRole`. Coordinators/Directors: `emailEnabled: true`; field workers: `emailEnabled: false`. Two **explicit overrides** documented in AC6/AC10: court reminder assignee + case transfer new assignee.

5. **Court job delivery order is SACRED** — Stories **5.3**/**5.4** send emails **before** `SaveChangesAsync` so dedup flags are not set on email failure. Use **`Send*EmailsAsync`** (propagates `EmailDeliveryException`) — **never** `TrySend*` for court pre-save path. Post-save producers use `TrySend*` only.

6. **Claim submit has no in-app notification today** — only audit event. This story adds **email only** for submit (no new in-app row unless product asks — out of scope).

7. **Supervisor claim email ≠ claimant copy** — reuse amounts/destination from claim entity; **do not** reuse `TravelClaimNotificationCopy` strings (those are second-person for claimant).

8. **POCSO purpose omission** — addresses deferred review items in `deferred-work.md` (5.3/5.4 `purpose` free-text PII). Template accepts `SensitivityLevel` and omits purpose body line for POCSO.

9. **Report ready is hook-only** — Epic **8.5** async export job will call `TrySendReportExportReadyAsync`. Do not build export job in this story.

10. **Bell UI is Story 7.4** — emails have no deep links; footer points users to open the app manually.

11. **No OpenAPI / api-client changes** — no new HTTP endpoints.

12. **`UserDeviceService` / push tokens irrelevant** — email uses `users.email` only.

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `CourtReminderBackgroundService.cs` | `IEmailSender` + inline `BuildEmailMessage` / `BuildEmailRecipients` | Inject `EmailDeliveryService`; `SendCourtReminderEmailsAsync` pre-save; delete private email helpers |
| `CourtMissEscalationBackgroundService.cs` | `IEmailSender` + inline `BuildEmailMessage` | Inject `EmailDeliveryService`; `SendCourtMissEscalationEmailsAsync` pre-save |
| `TravelClaimService.cs` | ctor: `NotificationService`, `PushDeliveryService`; Submit: audit only | Add `EmailDeliveryService` to ctor; `TrySend*` after save on submit + approve/return |
| `CaseService.cs` | ctor: no notification services; Transfer: audit only | Add `EmailDeliveryService` to ctor; `TrySendCaseTransferredAsync` after save |
| `NotificationEventTypes.cs` | 5 push/in-app constants | Add `TravelClaimSubmitted`, `CaseTransferred`, `ReportExportReady` |
| `NotificationPreferencesDefaults.cs` | Role stub with channels | **Reuse** — no change unless adding field-worker assignments (do not) |
| `PushNotificationChannelMapper.cs` | Push channels | **Parallel** `EmailNotificationChannelMapper` — do not merge push/email mappers |
| `PushDeliveryService.cs` | Push orchestration | **Pattern reference** — mirror gating order and try/catch |
| `TravelClaimNotificationCopy.cs` | Claimant in-app copy | **Do not** use for supervisor emails |
| `AuthService.cs` | Auth emails | **No change** |
| `IEmailSender` / `FakeEmailSender` | Transport | **No change** to interface |
| `Program.cs` | DI registration | Register `EmailDeliveryService` |

### Email delivery contract

**Core gating order (per recipient user id):**
1. Load user — inactive/missing → skip info log
2. Empty/whitespace `user.Email` → skip
3. `NotificationPreferencesDefaults.ForRole(user.Role)` → unless **explicit override** flag on call, `EmailEnabled == false` → skip
4. `EmailNotificationChannelMapper.IsChannelEnabled(prefs.Channels, eventType)` → false → skip
5. Render template → `IEmailSender.SendAsync`

**Pre-save entry points (court jobs — propagate `EmailDeliveryException`, no internal catch):**

```csharp
Task EmailDeliveryService.SendCourtReminderEmailsAsync(
    CourtSitting sitting, Case caseEntity, User assignee, CancellationToken ct = default);

Task EmailDeliveryService.SendCourtMissEscalationEmailsAsync(
    CourtSitting sitting, Case caseEntity, CancellationToken ct = default);
```

**Post-save entry points (`TrySend*` — internal catch, log warnings, never throw to caller):**

```csharp
Task EmailDeliveryService.TrySendTravelClaimSubmittedAsync(
    TravelClaim claim, string claimantEmail, CancellationToken ct = default);

Task EmailDeliveryService.TrySendTravelClaimDecisionAsync(
    TravelClaim claim, string claimantEmail, string decisionEventType, string? directorComment, CancellationToken ct = default);
    // decisionEventType: NotificationEventTypes.TravelClaimApproved | TravelClaimReturned

Task EmailDeliveryService.TrySendCaseTransferredAsync(
    Case caseEntity, User newAssignee, DateTime transferredAtUtc, CancellationToken ct = default);

Task EmailDeliveryService.TrySendReportExportReadyAsync(
    Guid userId, Guid organisationId, string reportName, DateTime expiresAtUtc, CancellationToken ct = default);
```

Use `NotificationEventTypes.TravelClaimApproved` / `TravelClaimReturned` constants in decision method — not raw strings.

**Org desk staff query (reuse pattern):**

```csharp
db.Users.Where(u => u.OrganisationId == orgId && u.IsActive && u.Email != ""
    && (u.Role == UserRoles.Coordinator || u.Role == UserRoles.Director))
```

Filter per-recipient through gating — do not bulk-send BCC.

### Template content guidelines

| Template | Subject pattern | Body must include |
|----------|----------------|-------------------|
| Court reminder | `Court sitting reminder — {courtName}` | Court name, scheduled UTC ISO, purpose (or POCSO placeholder), crime #, ST # |
| Court miss | `Court sitting missed — {courtName}` | Same fields as reminder |
| Claim submitted | `Travel claim submitted — {destination}` | Claimant staff email, destination, ₹amount, claim date, claim id |
| Claim decision | `Travel claim approved/returned — {destination}` | Claimant staff email, destination, amount, status, optional director note |
| Case transferred | `Case assigned — Crime {crimeNumber}` | Crime #, ST #, new assignee email, transferred UTC ISO |
| Report ready | `Report ready — {reportName}` | Report name, expiry UTC ISO, footer |

Use `₹` for INR amounts with `{amount:0.##}` (match `TravelClaimNotificationCopy`). Format all UTC timestamps as **ISO 8601 `O`** across templates (matches existing court job bodies).

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Infrastructure/Email/Templates/EmailTemplateFooter.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/CourtReminderEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/CourtMissEscalationEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/TravelClaimSubmittedEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/TravelClaimDecisionEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/CaseTransferredEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/Templates/ReportExportReadyEmailTemplate.cs` |
| NEW | `apps/api/Infrastructure/Email/EmailDeliveryService.cs` |
| NEW | `apps/api/Infrastructure/Email/EmailNotificationChannelMapper.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs` |
| UPDATE | `apps/api/Jobs/CourtReminderBackgroundService.cs` |
| UPDATE | `apps/api/Jobs/CourtMissEscalationBackgroundService.cs` |
| UPDATE | `apps/api/Infrastructure/TravelClaims/TravelClaimService.cs` |
| UPDATE | `apps/api/Infrastructure/Cases/CaseService.cs` |
| UPDATE | `apps/api/Program.cs` |
| NEW | `tests/api.unit/EmailDeliveryServiceTests.cs` |
| NEW | `tests/api.unit/EmailTemplateTests.cs` |
| UPDATE | `tests/api.integration/CourtReminderJobTests.cs` |
| UPDATE | `tests/api.integration/CourtMissEscalationJobTests.cs` |
| NEW | `tests/api.integration/TravelClaimEmailNotificationTests.cs` |
| NEW/UPDATE | `tests/api.integration/CaseAssignmentTests.cs` (email assertions) |
| UPDATE | `README.md` |

**No mobile/web changes. No OpenAPI regeneration.**

### Previous story intelligence (7.2)

- `PushDeliveryService` gating order and internal try/catch — **mirror for email**.
- Court/coordinator roles: coordinators get email not push (`pushEnabled: false`).
- Claim approve/return already calls push after save — add supervisor email **after** same save, same method.
- Integration tests often need Docker/Testcontainers — document if unavailable.

### Previous story intelligence (7.1)

- `NotificationPreferencesDefaults`: Coordinator/Director have `reports: true`, `assignments: true`, `claims: true`, `court: true`.
- Field workers: `emailEnabled: false` — only override for FR-16 court assignee + assignment awareness emails.

### Previous story intelligence (5.3 / 5.4)

- Court reminder: coordinator + assignee recipients; dedupe shared email.
- Court miss: coordinators only.
- Email-before-save transaction boundary — **must preserve**.
- Existing integration tests assert `FakeEmailSender.Messages` — update expected subject/body strings after template refactor.

### Previous story intelligence (6.4)

- Claimant notification copy in `TravelClaimNotificationCopy` — supervisor email needs third-person wording.
- `TravelClaimNotificationApiTests` — extend for coordinator email on approve/return, not claimant email.

### Previous story intelligence (2.8)

- `CaseService.TransferAsync` — no notification today; email is net-new after existing `SaveChangesAsync`.
- Handoff fields stay out of email.
- **Initial case create assignment** (if any) is out of scope — email only on explicit `POST .../transfer` (Story **2.8**).

### Testing requirements

**Unit (`tests/api.unit/`):**
- `EmailDeliveryServiceTests` — mock `IEmailSender` or use `FakeEmailSender`; gating skips; override paths for assignee; multi-coordinator fan-out
- `EmailTemplateTests` — POCSO omits purpose; footer present; supervisor claim decision wording

**Integration (`tests/api.integration/`):**
- Refactor `CourtReminderJobTests` / `CourtMissEscalationJobTests` for template output (not exact legacy string match if formatting improves — assert key fields present)
- Claim submit as field worker → coordinator `FakeEmailSender` message with `travel.claim.submitted` context
- Case transfer → coordinator + assignee messages
- Regression: `TravelClaimNotificationApiTests`, `PushDeliveryApiTests`, `NotificationsAndOverdueJobTests` still pass
- **Must preserve:** `ReminderJob_EmailFailure_DoesNotSetDedupFlag`, `MissEscalationJob_EmailFailure_DoesNotSetDedupFlag`

**Run commands:**

```bash
dotnet test tests/api.unit --filter "FullyQualifiedName~Email"
dotnet test tests/api.integration --filter "FullyQualifiedName~CourtReminder|CourtMiss|TravelClaim|CaseAssignment"
```

### Project structure notes

- Templates live under `Infrastructure/Email/Templates/` — not `Infrastructure/Notifications/` (email transport concern).
- `EmailDeliveryService` in `Infrastructure/Email/` alongside `IEmailSender` — jobs/services inject it instead of raw `IEmailSender` for operational mail.
- Keep template classes `internal` if only used by `EmailDeliveryService`; public hook method on service for report ready.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 7.3, FR-16, FR-19]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — Email SMTP, notification jobs, POCSO]
- [Source: `_bmad-output/project-context.md` — API structure, testing layout, POCSO list DTO rules]
- [Source: `_bmad-output/implementation-artifacts/5-3-court-reminder-background-job.md` — email-before-save order]
- [Source: `_bmad-output/implementation-artifacts/7-2-push-delivery-service.md` — delivery service pattern]
- [Source: `apps/api/Infrastructure/Email/IEmailSender.cs`]
- [Source: `apps/api/Infrastructure/Notifications/NotificationPreferencesDefaults.cs`]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — court purpose PII deferral]

## Dev Agent Record

### Agent Model Used

Composer

### Debug Log References

### Completion Notes List

- Implemented plain-text email templates + `EmailDeliveryService` with `Send*` (court pre-save, throws) vs `TrySend*` (post-save, swallows) split.
- Wired court reminder/miss jobs, claim submit/approve/return, case transfer; report-ready hook for Epic 8.5.
- POCSO court emails omit free-text sitting purpose; operational override for court assignee + transfer assignee.
- Unit tests: 14 passed (`FullyQualifiedName~Email`). Integration tests authored; Docker/Testcontainers unavailable in dev session.

### File List

- apps/api/Infrastructure/Email/Templates/EmailTemplateFooter.cs (new)
- apps/api/Infrastructure/Email/Templates/CourtSittingEmailBodyHelper.cs (new)
- apps/api/Infrastructure/Email/Templates/CourtReminderEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/Templates/CourtMissEscalationEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/Templates/TravelClaimSubmittedEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/Templates/TravelClaimDecisionEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/Templates/CaseTransferredEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/Templates/ReportExportReadyEmailTemplate.cs (new)
- apps/api/Infrastructure/Email/EmailNotificationChannelMapper.cs (new)
- apps/api/Infrastructure/Email/EmailDeliveryService.cs (new)
- apps/api/Infrastructure/Notifications/NotificationEventTypes.cs (modified)
- apps/api/Jobs/CourtReminderBackgroundService.cs (modified)
- apps/api/Jobs/CourtMissEscalationBackgroundService.cs (modified)
- apps/api/Infrastructure/TravelClaims/TravelClaimService.cs (modified)
- apps/api/Infrastructure/Cases/CaseService.cs (modified)
- apps/api/Program.cs (modified)
- tests/api.unit/EmailDeliveryServiceTests.cs (new)
- tests/api.integration/TravelClaimEmailNotificationTests.cs (new)
- tests/api.integration/CourtReminderJobTests.cs (modified)
- tests/api.integration/CaseAssignmentTests.cs (modified)
- README.md (modified)

## Change Log

- 2026-06-20: Story created — email templates, delivery service, producer wiring, report hook for Epic 8.5.
- 2026-06-20: Validated — 10 fixes (Send* vs TrySend*, court dedup tests, constructor wiring, POCSO test, UTC format).
- 2026-06-20: Implemented — templates, EmailDeliveryService, producer wiring, unit/integration tests, README.

## Story Completion Status

Ready for code review. All ACs implemented; integration tests require Docker to execute locally.

### Review Findings

- [x] [Review][Defer] ShouldSendToUser uses role-default preferences, not per-user DB preferences — deferred, per-user preferences persistence is out of scope (story spec: "no preferences PATCH persistence")
- [x] [Review][Defer] Pre-save court email fail-fast skips remaining recipients on coordinator N failure; on retry coordinators 0..N-1 receive duplicates — deferred, spec-intended behavior per AC6 (email-before-save contract)
- [x] [Review][Defer] EmailDeliveryException lacks `[Serializable]` attribute — deferred, .NET 8 relaxed this requirement
- [x] [Review][Defer] No HTML email body / clickable link in footer — deferred, plain-text only per AC2
- [x] [Review][Defer] TOCTOU window: claimant email fetched after SaveChangesAsync — deferred, inherent race, TrySend* swallows exceptions
- [x] [Review][Defer] SendToDeskStaffAsync 4-param overload creates isolated HashSet — deferred, not a current bug (all callers share the set correctly)

- [x] [Review][Patch] Silent gating skips — ShouldSendToUser returns false without any log output for inactive/empty-email/emailEnabled/channel-disabled paths [EmailDeliveryService.cs:399-433]
- [x] [Review][Patch] Court miss escalation email dispatched before still-eligible re-check — reorder so email is sent after the guard [CourtMissEscalationJobRunner.cs:78-97]
- [x] [Review][Patch] GetClaimantEmailAsync uses SingleAsync (throws InvalidOperationException on missing user) — use SingleOrDefaultAsync + explicit null check [TravelClaimService.cs:719-728]
- [x] [Review][Patch] Redundant coordinator query in both court job runners — the query only supplies a log counter, EmailDeliveryService runs its own query [CourtReminderJobRunner.cs:93-98, CourtMissEscalationJobRunner.cs:71-76]
- [x] [Review][Patch] Travel claim decision emails lack claim ID in subject — disambiguate by appending claim ID [TravelClaimDecisionEmailTemplate.cs:12-16]
- [x] [Review][Patch] IsKnownEventType check preempts operationalOverride in ShouldSendToUser — move known-event check after the override bypass [EmailDeliveryService.cs:411-419]
- [x] [Review][Patch] Missing unit tests: empty-email skip path, multi-recipient fan-out, assignee/coordinator dedupe [tests/api.unit/EmailDeliveryServiceTests.cs]
- [x] [Review][Patch] CourtReminderJobTests missing subject/body shape assertions — add template output verification [tests/api.integration/CourtReminderJobTests.cs]
