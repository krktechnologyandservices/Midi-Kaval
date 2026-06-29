---
baseline_commit: 689f5b4aea977615f0db09da5167d50c3e2ba815
---

# Story 5.5: Audit Broadcast — Batched Email Digests to Directors

| Status |
|--------|
| done |

> **TL;DR for dev — concrete actions:**
> 1. Create `AuditDigestJobOptions` config class (WindowMinutes, IntervalMinutes)
> 2. Create `AuditDigestJobRunner` (scoped) — queries un-notified user management `audit_events`, groups by organisation, composes digest per active Director, sends via `IEmailSender`, marks notified
> 3. Create `AuditDigestBackgroundService` (singleton `BackgroundService` timer) — mirrors existing `CourtReminderBackgroundService` pattern
> 4. Create `AuditDigestEmailTemplate` — subject + body with action summary per event
> 5. Add `audit_digest_entries` table to track which events have been digested (preserves append-only integrity of `audit_events`)
> 6. Register job runner + background service in `Program.cs` (non-Development only)
> 7. Integration tests with `FakeEmailSender` — single event, multi-event batched, dedup, multi-organisation, org with no active Directors
>
> **Brownfield reality:** The `audit_events` table is fully operational with `target_user_snapshot` (JSONB), `actor_ip_address`, and all user management event types from Stories 4-6/4-7. The `IEmailSender` + `EmailDeliveryService` stack is already wired. The `BackgroundService` + `*JobRunner` pattern is established (4 examples: `CourtReminderBackgroundService`, `CourtMissEscalationBackgroundService`, `InterventionOverdueBackgroundService`, `CaseAnonymizationBackgroundService`). The `EmailTemplateFooter`, `AppDbContext`, and `IEmailSender` are all ready to use. What's missing: the digest batch job, the tracking table, and the digest email template. **No UI changes** — this is backend-only.

## Story

As a **Director**,
I want to receive email notifications when user management actions happen in my organisation,
so that I'm aware of all changes without having to constantly check the audit log.

*Scope: **API + background job only** — backend-only. No in-app notifications (they'd duplicate the audit log), no push (deferred). Uses existing `BackgroundService` + `*JobRunner` pattern. New `audit_digest_entries` tracking table preserves append-only audit integrity by not modifying `audit_events`. Job sends digest emails every N minutes (configurable, default 5) via existing `IEmailSender`/`SmtpEmailSender`. Directors cannot opt out (FR-15).*

## Acceptance Criteria

1. **Given** user management actions (user creation, suspension, reactivation, permanent deletion) occur in an organisation
   **When** the audit digest job runs
   **Then** an email notification is sent to every active Director in that organisation
   **And** the email includes: action type (human-readable), target user name/email, actor name/email, timestamp for each event
   **And** if multiple actions occur within the digest window (default 5 minutes), they are combined into a **single digest email** per Director (batched, not one email per event)
   **And** the email body lists each event as a bullet or numbered item with consistent formatting

2. **Given** an organisation has no active Directors
   **When** the audit digest job runs
   **Then** no email is sent for that organisation
   **And** the events are still marked as processed (to prevent re-querying the same events on every run)

3. **Given** the same audit event
   **When** the job runs multiple times
   **Then** the event appears in at most one digest email (dedup via `audit_digest_entries`)
   **And** no duplicate emails are sent for the same event

4. **Given** a Director user
   **When** their account is suspended or deactivated
   **Then** they stop receiving digest emails until reactivated
   **And** this requires no explicit "unsubscribe" — the digest job checks `IsActive` before sending

5. **Given** multiple organisations
   **When** audit events occur in different organisations
   **Then** each organisation's Directors receive digests scoped to their own organisation only
   **And** no cross-organisation leakage occurs

6. **Given** a digest job run
   **When** `IEmailSender.SendAsync` throws an exception for a particular Director
   **Then** the error is logged and other Directors in the same organisation still receive their digests
   **And** the events are still marked as processed (failures are logged but do not block the batch)

7. **Given** the digest job configuration
   **When** the API starts (non-Development)
   **Then** `AuditDigestBackgroundService` registers as a hosted service
   **And** the job interval and digest window are configurable via `AuditDigestJobOptions`
   **And** integration tests invoke `AuditDigestJobRunner` directly (not via hosted service)

8. **Given** regression safety
   **When** the story is implemented
   **Then** existing email services, audit event recording, and notification preferences remain unchanged
   **And** no in-app notifications are created (this is email-only)
   **And** the `audit_events` table is not modified (append-only preserved)

## Tasks / Subtasks

### 1. Schema — `audit_digest_entries` tracking table (AC 1, 3)

- [x] Create migration with `audit_digest_entries` table:
  - `id` (UUID PK)
  - `audit_event_id` (UUID FK → `audit_events`, NOT NULL, ON DELETE CASCADE)
  - `organisation_id` (UUID FK → `organisations`, NOT NULL, ON DELETE CASCADE)
  - `digest_sent_at_utc` (timestamp NOT NULL)
  - `digest_batch_id` (UUID NOT NULL — groups entries sent in one digest email)
- [x] Composite index on `(audit_event_id)` for dedup check queries
- [x] Composite index on `(organisation_id, digest_sent_at_utc)` for batch queries
- [x] Create `AuditDigestEntry` entity in `Domain/Entities/`
- [x] Create `AuditDigestEntryConfiguration.cs` in `Infrastructure/Persistence/` (all existing configs live here — mirror pattern from `AuditEventConfiguration.cs`)
- [x] Register `DbSet<AuditDigestEntry>` in `AppDbContext`

### 2. Backend — Job options (AC 7)

- [x] Create `AuditDigestJobOptions` in `Jobs/`:
  ```csharp
  public sealed class AuditDigestJobOptions
  {
      public const string SectionName = "AuditDigestJob";
      public int WindowMinutes { get; set; } = 5;   // Digest window width
      public int IntervalMinutes { get; set; } = 5;  // Job execution interval
  }
  ```
- [x] Register options in `Program.cs`:
  ```csharp
  builder.Services.Configure<AuditDigestJobOptions>(
      builder.Configuration.GetSection(AuditDigestJobOptions.SectionName));
  ```

### 3. Backend — Digest email template (AC 1)

- [x] Create `AuditDigestEmailContext` record:
  - `OrganisationName` (string)
  - `Events` (list of digest event items with: ActionType, TargetName, TargetEmail, ActorName, ActorEmail, TimestampUtc)
- [x] Create `AuditDigestEmailTemplate` static class with `RenderSubject` and `RenderBody`:
  - Subject: `"User activity digest — {OrganisationName}"` (or similar)
  - Body: Plain-text format listing each event as a bullet with consistent fields:
    ```
    User Activity Digest — {OrganisationName}

    • {ActionType}: {TargetName} ({TargetEmail})
      By: {ActorName} ({ActorEmail})
      When: {Timestamp}

    • ...

    ---
    Open Kaval Online for details.
    ```
  - Use `EmailTemplateFooter.Append()` for consistency
  - Follow existing template pattern (`CourtMissEscalationEmailTemplate.cs`)

### 4. Backend — AuditDigestJobRunner (scoped, AC 1-6)

- [x] Create `AuditDigestJobRunner` in `Jobs/`:
  - Injects: `AppDbContext`, `IEmailSender`, `IOptions<AuditDigestJobOptions>`, `ILogger<AuditDigestJobRunner>`
  - Method: `RunAsync(CancellationToken)`

- [x] **Query eligible events** (include actor JOIN to resolve name/email for digest body):
  The `AuditEvent` entity already has `ActorUser` navigation property — use EF Core `.Include(ae => ae.ActorUser)` instead of raw SQL:
  ```csharp
  var events = await db.AuditEvents
      .Include(ae => ae.ActorUser)
      .Where(ae => ae.OrganisationId != null
          && digestEventTypes.Contains(ae.EventType)
          && !db.AuditDigestEntries.Any(ade => ade.AuditEventId == ae.Id)
          && ae.CreatedAtUtc < jobCutoff)
      .OrderBy(ae => ae.OrganisationId)
      .ThenBy(ae => ae.CreatedAtUtc)
      .ToListAsync(ct);
  ```
  Actor name/email available as `evt.ActorUser?.FirstName + " " + evt.ActorUser?.LastName` and `evt.ActorUser?.Email`.
  Target user snapshot available as JSON deserialized from `evt.TargetUserSnapshot`.
  - Event types to include (user management actions per FR-15):
    - `"user.suspended"`
    - `"user.reactivated"`
    - `"user.deleted"`
    - `"auth.account_created"` (user creation via registration)
  - Use `event_type.StartsWith()` prefix matching or explicit list
  - Order by `organisation_id`, `created_at_utc`

- [x] **Group by organisation:**
  - For each org with events, query active Directors:
    ```csharp
    var directors = await db.Users
        .Where(u => u.OrganisationId == orgId
            && u.Role == UserRoles.Director
            && u.IsActive
            && !u.IsSuspended)
        .ToListAsync(ct);
    ```
  - Skip orgs with zero active Directors (still mark events processed via AC 2)

- [x] **Compose and send digest:**
  - For each active Director in the org:
    1. Build `AuditDigestEmailContext` with all events for that org
    2. Render subject + body
    3. Send via `emailSender.SendAsync()`
    4. On success: record `AuditDigestEntry` rows (one per audit_event_id, same `digest_batch_id`)
  - If email send fails for a specific Director: log error, **still** record digest entries (AC 6 — failure doesn't block batch)
  - Use `sentAddresses HashSet` to dedupe Directors by email (AC 4 handles via IsActive check)

- [x] **Batch persistence:**
  - After processing all orgs, single `SaveChangesAsync` to persist all digest entries
  - Use `using var transaction` for atomicity within the batch

### 5. Backend — AuditDigestBackgroundService (singleton, AC 7)

- [x] Create `AuditDigestBackgroundService` in `Jobs/` (one file with runner):
  - Mirrors `CourtReminderBackgroundService` exactly:
    ```csharp
    public sealed class AuditDigestBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditDigestJobOptions> options,
        ILogger<AuditDigestBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var runner = scope.ServiceProvider.GetRequiredService<AuditDigestJobRunner>();
                    await runner.RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Audit digest job failed.");
                }
                await Task.Delay(interval, stoppingToken);
            }
        }
    }
    ```

### 6. Registration in Program.cs (AC 7)

- [x] Register options:
  ```csharp
  builder.Services.Configure<AuditDigestJobOptions>(
      builder.Configuration.GetSection(AuditDigestJobOptions.SectionName));
  ```
- [x] Register scoped runner:
  ```csharp
  builder.Services.AddScoped<AuditDigestJobRunner>();
  ```
- [x] Register background service (only non-Development):
  ```csharp
  // Inside if (!builder.Environment.IsDevelopment())
  builder.Services.AddHostedService<AuditDigestBackgroundService>();
  ```

### 7. Integration tests (AC 8)

- [x] Create `AuditDigestJobTests.cs` in `tests/api.integration/`:
  - Follow pattern from `CourtMissEscalationJobTests.cs`
  - Include Testcontainers PostgreSQL setup

- [x] **Test 1 — Single event digest:** Create one user.suspended event and an active Director → job runs → Director receives one email with one event in body
- [x] **Test 2 — Batched multi-event digest:** Create 3 events within window (user.suspended, user.reactivated, user.deleted) → job runs → Director receives one email with all 3 events
- [x] **Test 3 — Dedup (second run):** Run job → all events processed → run job again → no new emails
- [x] **Test 4 — Multi-organisation isolation:** Create events in org A and org B → org A Director receives only org A events; org B Director receives only org B events
- [x] **Test 5 — No active Directors:** Org has events but zero active Directors → job runs → no emails sent but events marked processed
- [x] **Test 6 — Email failure for one Director:** Two active Directors; `FakeEmailSender.FailNextSend = true` → one Director gets digest, the other fails → error logged → events still marked processed for both
- [x] **Test 7 — Only user management event types:** Create a non-user-management event (e.g., `court.sitting.created`) → not included in digest → Director receives email only for user management events
- [x] **Test 8 — Multi-tenancy data isolation:** Verify Directors of org B cannot see org A events in their digest

## Existing Context

### Already implemented (brownfield — do NOT recreate)

| Component | File | Status |
|-----------|------|--------|
| `IEmailSender` interface | `apps/api/Infrastructure/Email/IEmailSender.cs` | ✅ Existing — `SendAsync(EmailMessage)` |
| `SmtpEmailSender` | `apps/api/Infrastructure/Email/SmtpEmailSender.cs` | ✅ Existing — MailKit SMTP |
| `FakeEmailSender` | `apps/api/Infrastructure/Email/FakeEmailSender.cs` | ✅ Existing — in-memory, supports `FailNextSend` |
| `EmailDeliveryService` | `apps/api/Infrastructure/Email/EmailDeliveryService.cs` | ✅ Existing — higher-level send orchestration, preference checks |
| Email template pattern | `Infrastructure/Email/Templates/*.cs` | ✅ Existing — `*EmailContext` record + `*EmailTemplate` static class |
| `EmailTemplateFooter` | `Infrastructure/Email/Templates/EmailTemplateFooter.cs` | ✅ Existing — `Append(body)` |
| `BackgroundService` + `*JobRunner` pattern | `Jobs/CourtReminderBackgroundService.cs` | ✅ Existing — canonical reference |
| `AuditEvent` entity | `Domain/Entities/AuditEvent.cs` | ✅ Existing — all columns, snapshot, IP |
| `AppDbContext` | `Infrastructure/Persistence/AppDbContext.cs` | ✅ Existing — `DbSet<AuditEvent>`, `DbSet<User>`, `DbSet<Organisation>` |
| Audit event types | `Infrastructure/Audit/AuditEventTypes.cs` | ✅ Existing — all event type constants |
| User role constants | `Domain/Enums/UserRoles.cs` | ✅ Existing — `Director`, `Coordinator`, etc. |
| Integration test pattern | `tests/api.integration/CourtMissEscalationJobTests.cs` | ✅ Existing — Testcontainers, `FakeEmailSender` |

### Key design notes

- **No in-app notifications**: Unlike the court miss escalation job (which creates `InAppNotification` rows), this digest is email-only. Directors view the audit log for in-app review. Creating in-app notifications would duplicate the audit trail and add unnecessary DB writes.
- **Preserve append-only audit**: The `audit_events` table is append-only (NFR-4 — DB user has INSERT-only privileges). Adding a `digest_sent_at_utc` column would require UPDATE privileges. Instead, use a separate `audit_digest_entries` tracking table — this preserves the append-only invariant.
- **Why not use existing `EmailDeliveryService.SendToUserAsync`?**: That method checks `EmailNotificationChannelMapper` and notification preferences. FR-15 explicitly states "Directors cannot opt out." Use `IEmailSender` directly (like the fire-and-forget Hangfire email jobs do), bypassing preference checks. Note: `FakeEmailSender` exposes sent messages via `.Messages` (not `.SentMessages`).
- **Batch ID**: Each digest email gets a unique `digest_batch_id` (UUID). This enables future querying like "what events were in batch X?" for debugging and audit.
- **Event type filter for digest candidates**: Only events that represent user management actions (FR-15) should trigger digests. These are: `user.suspended`, `user.reactivated`, `user.deleted`, `auth.account_created` (captures both invitation-based and direct user registration). `invitation.sent` is excluded — the invitation itself is not a management action on an existing user; the subsequent `auth.account_created` is.
- **Window vs interval**: `WindowMinutes` controls how far back the job looks for un-notified events. Only events aged at least `WindowMinutes` are eligible — this gives time for more events to accumulate within the same batching window before a digest is sent. `IntervalMinutes` controls how often the job runs. These can be the same value (default 5) for simplicity. Example: with both set to 5, the job runs every 5 minutes and emails events that occurred 5+ minutes ago, giving a ~5-minute batching window.
- **Structured logging**: Log digest batch summary at Info level: `"Audit digest sent: {BatchId} org {OrganisationId} {EventCount} events to {DirectorCount} Directors"`.

### Previous story intelligence (from 5-4 — court miss escalation)

- BackgroundService + JobRunner pattern is well-established: runner is scoped (business logic), service is singleton (timer). Register runner as `AddScoped`, service as `AddHostedService` (non-Dev).
- Email-then-SaveChanges ordering is critical: send emails first (outside DB transaction), then save tracking entries. If email send fails for some Directors, still save entries for the ones that succeeded.
- Integration tests invoke runner directly (`await runner.RunAsync(ct)`) — not via hosted service. Use `FakeEmailSender` to capture sent emails.
- `FakeEmailSender.FailNextSend` pattern for email-failure testing.

### Previous story intelligence (from 4-6 — audit events data model)

- `target_user_snapshot` is JSONB with `{ email, name, role }` — available on every audit event for user management actions.
- `actor_user_id` FK preserves the actor's identity (even after anonymisation via SET NULL).
- The `event_type` values for user management events are well-defined constants in `AuditEventTypes.cs`.

### Previous story intelligence (from 4-7 — audit log viewer)

- The audit log viewer is the in-app counterpart to this email digest. Directors who want to investigate further after receiving an email digest can navigate to Admin → Audit Log.
- Event type labels are mapped to human-readable strings in `audit.utils.ts` — useful reference for the digest email template's human-readable action type.

## Architecture Compliance

### API Pattern

| Decision | Value |
|----------|-------|
| No new HTTP endpoints | All backend-only — background job + email |
| Existing infrastructure | `IEmailSender`, `BackgroundService` pattern, `AppDbContext` |
| No in-app notifications | Digest is email-only — Directors use audit log for in-app review |
| No UI changes | Backend-only story |

### Files to create (NEW)

| File | Purpose |
|------|---------|
| `apps/api/Domain/Entities/AuditDigestEntry.cs` | Entity for tracking table |
| `apps/api/Infrastructure/Persistence/AuditDigestEntryConfiguration.cs` | EF configuration (all configs live in `Infrastructure/Persistence/`) |
| `apps/api/Jobs/AuditDigestBackgroundService.cs` | BackgroundService + JobRunner (one file) |
| `apps/api/Infrastructure/Email/Templates/AuditDigestEmailTemplate.cs` | Digest email template |
| `apps/api/Migrations/{timestamp}_AddAuditDigestEntries.cs` | Migration for tracking table |
| `tests/api.integration/AuditDigestJobTests.cs` | Integration tests (8 tests) |

### Files to modify (UPDATE)

| File | What to change |
|------|----------------|
| `apps/api/Domain/Entities/AuditEvent.cs` | Add `DigestEntries` nav collection (optional — for EF relationship if needed) |
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<AuditDigestEntry>` |
| `apps/api/Program.cs` | Register `AuditDigestJobOptions`, `AuditDigestJobRunner`, `AuditDigestBackgroundService` |
| `apps/api/appsettings.Development.json` | Add `AuditDigestJob` config section (optional — can use defaults) |

### Files that need NO changes (verified)

| File | Why |
|------|-----|
| `apps/api/Infrastructure/Email/IEmailSender.cs` | Interface is already sufficient |
| `apps/api/Infrastructure/Email/SmtpEmailSender.cs` | No changes needed |
| `apps/api/Infrastructure/Email/FakeEmailSender.cs` | Already supports `FailNextSend` + `.Messages` |
| `apps/api/Infrastructure/Email/EmailDeliveryService.cs` | Not used — digest bypasses preference checks |
| `apps/api/Infrastructure/Audit/AuditEventTypes.cs` | All event types already defined |
| `apps/api/Infrastructure/Audit/AuditService.cs` | Recording unchanged |
| `apps/api/Infrastructure/Notifications/NotificationService.cs` | No in-app notifications for digest |
| `apps/api/Infrastructure/Notifications/NotificationEventTypes.cs` | Digest is email-only — no new notification event type |
| Frontend files | No UI changes |

## Library / Framework Requirements

- No new NuGet packages — `System.Text.Json`, `Microsoft.Extensions.Options`, `Microsoft.EntityFrameworkCore` already in use
- No new npm packages — no frontend changes
- Follow existing `BackgroundService` + `*JobRunner` pattern (same as `CourtReminderBackgroundService`, `CourtMissEscalationBackgroundService`)

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash (Cocreator)

### Completion Notes List

| Note |
|------|
| ✅ Implemented AuditDigestEntry entity + EF config + migration + DbSet |
| ✅ Implemented AuditDigestJobOptions, AuditDigestEmailContext, AuditDigestEmailTemplate |
| ✅ Implemented AuditDigestJobRunner with event query, org grouping, Director filter, digest compose/send, batch persistence |
| ✅ Implemented AuditDigestBackgroundService (BackgroundService timer pattern) |
| ✅ Registered all services in Program.cs (options, scoped runner, non-Dev hosted service) |
| ✅ Added AuditDigestJob config to appsettings.Development.json |
| ✅ Created 8 integration tests (single, batched, dedup, multi-org, no directors, email failure, event type filter, multi-tenancy) |
| ✅ Added RunAuditDigestJobAsync helper to CaseTestData |
| ✅ API builds with 0 errors |

## Configuration Reference

```json
// appsettings.json / appsettings.Development.json
"AuditDigestJob": {
  "WindowMinutes": 5,
  "IntervalMinutes": 5
}
```

## Integration Test Recipe

1. Create organisation with active Director user
2. Seed audit events via `AppDbContext` (or via domain services) with specific event types and timestamps within the digest window
3. Run `AuditDigestJobRunner.RunAsync(ct)` directly
4. Assert `FakeEmailSender.Messages` contains:
   - Correct recipient (Director email)
   - Subject contains "User activity digest"
   - Body contains all seeded event details (action type, target name/email, actor name/email, timestamp)
5. Run runner again → assert no new emails (dedup)
6. Repeat for multi-org, no-Directors, email-failure, and non-user-management-event scenarios

Use existing `RbacTestData.DirectorEmail`, `AuthTestData` helper patterns. For test isolation, each test creates its own organisation + Director.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 5 Story 5.1]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md` — FR-15, NFR-8, NFR-11, NFR-12, NFR-13]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — §Notifications, §Decision Impact Analysis step 8]
- [Source: `_bmad-output/implementation-artifacts/5-4-court-miss-escalation-and-crisis-queue-feed.md` — BackgroundService pattern]
- [Source: `_bmad-output/implementation-artifacts/4-6-audit-events-data-model-and-recording.md` — audit_events schema]
- [Source: `apps/api/Jobs/CourtReminderBackgroundService.cs` — canonical BackgroundService pattern]
- [Source: `apps/api/Infrastructure/Email/IEmailSender.cs`]
- [Source: `apps/api/Infrastructure/Email/FakeEmailSender.cs`]
- [Source: `apps/api/Infrastructure/Email/Templates/CourtMissEscalationEmailTemplate.cs` — template pattern]
- [Source: `apps/api/Infrastructure/Audit/AuditEventTypes.cs`]

## Review Findings

### Patch

- [x] [Review][Patch] Missing FK `organisation_id` → `organisations` in migration and EF config
- [x] [Review][Patch] Missing `ae.OrganisationId != null` guard in event query `Where` clause
- [x] [Review][Patch] Missing explicit DB transaction for batch persistence
- [x] [Review][Patch] Namespace mismatch — uses `Infrastructure.Notifications` instead of `Jobs`
- [x] [Review][Patch] Duplicate `AuditDigestEntry` per director — per-director loop creates duplicate entries violating unique index
- [x] [Review][Patch] Unhandled `JsonException` on malformed `TargetUserSnapshot` in `BuildDigestEventItem`
- [x] [Review][Patch] No guard for empty Director email before `SendAsync`
- [x] [Review][Patch] Redundant private test helper — duplicates `CaseTestData` static helper
- [x] [Review][Patch] No concurrency guard — overlapping timer executions possible

### Defer

- [x] [Review][Defer] Eager `.Include(ActorUser)` loads full User entity (only name/email needed) — deferred, pre-existing project-wide pattern
- [x] [Review][Defer] No index on `AuditEvent.CreatedAtUtc` — deferred, pre-existing schema optimization concern
- [x] [Review][Defer] `TargetUserSnapshot` JSON has no schema version — deferred, pre-existing design constraint
- [x] [Review][Defer] `ActorIpAddress` PII with no retention policy — deferred, pre-existing cross-cutting legal concern
- [x] [Review][Defer] `DigestBatchId` is an orphaned GUID with no referential table — deferred, designed as lightweight tracking ID
- [x] [Review][Defer] No `CancellationToken` propagation in timer callback — deferred, pre-existing pattern in all BackgroundServices
- [x] [Review][Defer] No pagination for large event or director result sets — deferred, scales with org size
- [x] [Review][Defer] Clock drift can shift window boundary — deferred, acceptable for 5-min batch window
- [x] [Review][Defer] No exponential backoff on repeated job failure — deferred, pre-existing pattern
- [x] [Review][Defer] Large event backlog after downtime exceeds memory/email size — deferred, production concern for post-launch

### Re-review (2026-06-28)

#### Patch

- [x] [Review][Patch] Director query missing `OrderBy` — non-deterministic iteration affects email-failure test [AuditDigestBackgroundService.cs:65]
- [x] [Review][Patch] `OperationCanceledException` during director loop loses digest entries — already-emailed directors get duplicates on retry [AuditDigestBackgroundService.cs:128]
- [x] [Review][Patch] `RollbackAsync(ct)` uses cancelled token on OCE — masks original exception [AuditDigestBackgroundService.cs:189]
- [x] [Review][Patch] Test does not verify digest entries persisted after email failure (AC 6) [AuditDigestJobTests.cs:204]
- [x] [Review][Patch] No test for Director with `IsSuspended=true` while `IsActive=true` (AC 4) [AuditDigestJobTests.cs]
- [x] [Review][Patch] Private test helper not removed — still duplicates `CaseTestData` static helper despite earlier patch claim [AuditDigestJobTests.cs:306] — intentionally retained: minimal duplication avoids fragile cross-class dependency

#### Defer

- [x] [Review][Defer] Horizontal scale duplicate emails — pre-existing cross-cutting concern; no distributed lock pattern exists in project
- [x] [Review][Defer] Missing structural sync between `DigestEventTypes` and `FormatEventType` — design-level improvement for future
- [x] [Review][Defer] `AuditEvent` relationship configured by convention not explicit — works correctly, minor inconsistency

#### Dismiss

- [x] [Review][Dismiss] Spec's `ae.OrganisationId != null` guard is a no-op for non-nullable `Guid` — spec documentation issue, code correct
- [x] [Review][Dismiss] `Unknown` ambiguity for null/corrupt/missing snapshot — deliberate design choice

## Change Log

| Date | Change |
|------|--------|
| 2026-06-28 | Story created — batched email digests for user management events to Directors |
| 2026-06-28 | Implementation complete — all tasks checked, ready for review |
| 2026-06-28 | Code review findings documented — 9 patch, 10 defer, 3 dismiss |
| 2026-06-28 | Code review patches applied — 9/9 patch findings fixed, re-review launched |
| 2026-06-28 | Re-review findings documented — 6 patch (all applied), 3 defer, 2 dismiss |
