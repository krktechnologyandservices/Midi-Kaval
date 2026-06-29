---
baseline_commit: 689f5b4aea977615f0db09da5167d50c3e2ba815
---

# Story 5.6: User Notification — Suspension and Deletion Emails

Status: done

## Story

As a **user**,
I want to receive an email when my account is suspended or permanently deleted,
so that I'm informed of changes to my access and know what steps to take.

*Scope: **API + Hangfire job only** — backend-only. No in-app notifications, no push. Extends the existing `UserStatusEmailJob` (Hangfire fire-and-forget, already wired into `UserManagementService`) with rate-limiting (Redis) and audit event recording. No new EF tables, migrations, BackgroundServices, or JobRunners needed.*

## Acceptance Criteria

1. **Suspension email:** When a user account is suspended, the affected user receives an email that includes the reason (if provided by the Director) and instructions to contact another Director for appeal.
2. **Permanent deletion email:** When a user account is permanently deleted (anonymised), the affected user receives an email confirming the action and noting it is irreversible.
3. **Rate limiting:** Notification emails to the same user are rate-limited to a maximum of 3 emails of the same type per 24-hour period. Uses Redis counter with 24h TTL.
4. **Email delivery retry:** Email delivery failures are automatically retried by Hangfire's `[AutomaticRetry(Attempts = 3)]`.
5. **Send at action time (fire-and-forget):** The notification email is dispatched immediately via `BackgroundJob.Enqueue<UserStatusEmailJob>` when the suspension/deletion action is taken. **Already implemented** in `UserManagementService`.
6. **Audit integration:** The notification send attempt is recorded in the `audit_events` table with event type `user_notification_sent`, including the target email and notification type (suspension vs deletion).
7. **No Director opt-out:** These are sent directly to the affected user. No notification preference checks apply.
8. **Integration tests:** Verify suspension email, deletion email, rate-limit enforcement (3/24h), delivery failure + retry, and that notification audit events are recorded.

**Existing behavior (already complete, verify only):** `SuspendAsync()` and `DeleteAsync()` in `UserManagementService` already call `BackgroundJob.Enqueue<UserStatusEmailJob>` after committing the action. The job sends emails with correct subject/body for `suspended`, `deleted`, and `reactivated` action types.

**What this story adds:** Rate-limiting guard before send, audit event recording after successful send, richer email templates, and integration tests.

## Tasks / Subtasks

- [x] **Task 1: Create Redis rate-limiter for user notification emails** (AC: 3)
  - [x] Create `IUserNotificationRateLimiter` interface in `Infrastructure/Notifications/`
  - [x] Create `RedisUserNotificationRateLimiter` in `Infrastructure/Notifications/`
  - [x] Create `FakeUserNotificationRateLimiter` for tests (in-memory, configurable max, never blocks unless told to)
  - [x] Register in DI: `services.AddSingleton<IUserNotificationRateLimiter, RedisUserNotificationRateLimiter>()`

- [x] **Task 2: Add rate-limiting and audit to UserStatusEmailJob** (AC: 1, 2, 3, 4, 6)
  - [x] Modify `UserStatusEmailJob` constructor to inject `IUserNotificationRateLimiter` and `IAuditService`
  - [x] Before sending: call `_rateLimiter.CanSendAsync(userId, actionType, ct)` — if false, log warning and return (job completes without retry)
  - [x] After successful send: call `_rateLimiter.RecordSendAsync(userId, actionType, ct)`
  - [x] After successful send: record audit event via `_auditService.RecordAsync(...)` with:
  - [x] Add `Guid organisationId` to `ExecuteAsync` signature
  - [x] Update `UserManagementService.EnqueueStatusEmailJob` and `EnqueueDeletionEmailJob` to pass `organisationId` to the Hangfire job
  - [x] Add `AuditEventTypes.UserNotificationSent = "user.notification_sent"` constant

- [x] **Task 3: Enrich email templates** (AC: 1, 2)
  - [x] Extend `UserStatusEmailJob` switch cases with appeal instructions and irreversibility notice

- [x] **Task 4: Integration tests** (AC: 8)
  - [x] Create `UserStatusEmailJobTests.cs` in `tests/api.integration/`:
  - [x] **Test 1 — Suspension email sent**
  - [x] **Test 2 — Deletion email sent**
  - [x] **Test 3 — Rate limit blocks 4th email**
  - [x] **Test 4 — Retry on delivery failure**
  - [x] **Test 5 — Audit event recorded**
  - [x] **Test 6 — Different notification types have separate rate-limit counters**

- [x] **Task 5: Config**
  - [x] Registered `IUserNotificationRateLimiter` as singleton in DI
  - [x] `UserStatusEmailJob` has no parameterless constructor — constructor injection only (Hangfire resolves from DI container)

## Dev Notes

### Brownfield Reality

- **`UserStatusEmailJob` already exists** at `apps/api/Jobs/UserStatusEmailJob.cs` — a Hangfire job with `[AutomaticRetry(Attempts = 3)]`, inline HTML templates for `suspended`/`reactivated`/`deleted` action types
- **Already wired** into `UserManagementService.SuspendAsync()` (line 171 calling `EnqueueStatusEmailJob`), `DeleteAsync()` (line 330 calling `EnqueueDeletionEmailJob`), and `ReactivateAsync()` (line 240)
- `DeletionAsync` properly captures `snapshotEmail` before anonymising the user row (line 287-288) and passes it to `EnqueueDeletionEmailJob` (line 330)
- `FakeEmailSender` (in-memory, `FailNextSend` support) exists for tests
- Redis is available via `IConnectionMultiplexer` singleton (injected in several existing services: `OtpChallengeStore`, `RefreshTokenStore`, `AuthVerifiedStore`, `PasswordResetTokenStore`, `OrganisationService`)
- `IAuditService` is registered as scoped, `AuditService.RecordAsync(...)` accepts organisationId, actor/subject user IDs, target snapshot, and metadata dict — inject directly into `UserStatusEmailJob` for audit recording
- Hangfire is configured with `HangfireInMemory` storage (test-friendly) + PostgreSQL

### Key Patterns to Follow

1. **Rate-limiter interface + implementation pattern:** Mirror `OtpChallengeStore` pattern (inject `IConnectionMultiplexer`, use `IDatabase.StringGet`/`StringIncrement`/`KeyExpire`). Test fake can use an in-memory `ConcurrentDictionary`.
2. **Hangfire job DI:** `UserStatusEmailJob` already uses constructor injection (`IEmailSender`, `ILogger`). Add `IUserNotificationRateLimiter` and `IAuditService` — Hangfire resolves from the app's DI container.
3. **Rate-limited silent skip:** When `CanSendAsync` returns false, log a warning and return (don't throw). Hangfire will mark the job as succeeded and not retry.
4. **Audit at send time (not enqueue time):** Record the audit event only after `emailSender.SendAsync` succeeds. This ensures audit events reflect actual deliveries, not just attempts.
5. **No new EF entities, tables, or migrations.** The story requirement for retry tracking is already satisfied by Hangfire's `[AutomaticRetry(Attempts = 3)]`.

### What NOT to Build

- ❌ No `PendingUserNotification` entity or EF config
- ❌ No `pending_user_notifications` migration
- ❌ No `UserNotificationJobRunner` class
- ❌ No `UserNotificationBackgroundService`
- ❌ No `UserNotificationEmailTemplate` class — extend the inline switch in `UserStatusEmailJob` instead
- ❌ No new `UserNotificationType` enum — the `actionType` string (`"suspended"`/`"deleted"`) serves this purpose

### Critical Gotchas

- **Hangfire parameterless constructor:** If `UserStatusEmailJob` has a parameterless constructor (for Hangfire serialization), it must be removed — constructor injection is the only supported pattern once dependencies are added.
- **Scoped IAuditService in Hangfire:** Hangfire jobs run in a background scope. Verify that scoped services resolve correctly (Hangfire's `UseScopedSerializer` or job activation filter may be needed — check existing Hangfire config).
- **Redis key format:** `user_notification:{userId}:{notificationType}:{yyyy-MM-dd}`. The `Guid` userId should be formatted as `N` (no dashes) for compact keys.
- **Deletion audit:** After deletion, the user row is anonymised. The `organisationId` must be passed to the Hangfire job at enqueue time (before anonymisation) since the job can't look up the user afterwards. `EnqueueDeletionEmailJob` currently only passes `(originalEmail, originalName)` — it needs `organisationId` added.

### References

- [Source: `apps/api/Jobs/UserStatusEmailJob.cs` — existing Hangfire job to extend]
- [Source: `apps/api/Domain/RoleManagement/UserManagementService.cs` — lines 171, 240, 330 (already wired), lines 345-354 (enqueue methods)]
- [Source: `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — add `UserNotificationSent` constant]
- [Source: `apps/api/Infrastructure/Audit/AuditService.cs` — IAuditService.RecordAsync signature]
- [Source: `apps/api/Infrastructure/Email/IEmailSender.cs`]
- [Source: `apps/api/Infrastructure/Email/FakeEmailSender.cs`]
- [Source: `apps/api/Infrastructure/Auth/OtpChallengeStore.cs` — Redis DI pattern reference]
- [Source: `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs` — existing DI registrations, Redis setup]
- [Source: `_bmad-output/planning-artifacts/epics.md` — §Epic 5, Story 5.2 acceptance criteria]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — §Notifications, §Redis rate-limit counters]

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash (Cocreator)

### Completion Notes List

| Note |
|------|
| ✅ Created `IUserNotificationRateLimiter` interface + `RedisUserNotificationRateLimiter` (Redis INCR with 24h TTL) |
| ✅ Created `FakeUserNotificationRateLimiter` (in-memory, configurable `MaxPerDay`) |
| ✅ Added `AuditEventTypes.UserNotificationSent = "user.notification_sent"` |
| ✅ Modified `UserStatusEmailJob`: injects rate limiter + audit service, checks rate limit before send, records rate limit + audit event after send, added `organisationId` param |
| ✅ Modified `UserManagementService`: passes `organisationId` to Hangfire job in `EnqueueStatusEmailJob` and `EnqueueDeletionEmailJob` |
| ✅ Enriched email templates: suspension includes appeal instructions, deletion includes irreversibility notice |
| ✅ Registered `RedisUserNotificationRateLimiter` as singleton in DI |
| ✅ Updated `AuthWebApplicationFactory` to replace rate limiter with `FakeUserNotificationRateLimiter` in tests |
| ✅ Created 6 integration tests: suspension email, deletion email, rate-limit enforcement, delivery failure throws, audit event recorded, separate rate-limit counters per type |
| ✅ Fixed unit test overrides in `UserManagementServiceTests` for updated method signatures |
| ✅ All projects build with 0 errors (API, IntegrationTests, UnitTests) |

### File List

- `_bmad-output/implementation-artifacts/5-6-user-notification-suspension-and-deletion-emails.md` (new)
- `apps/api/Jobs/UserStatusEmailJob.cs` (modified — added rate limiter + audit injection, orgId param, enriched templates)
- `apps/api/Infrastructure/Notifications/IUserNotificationRateLimiter.cs` (new)
- `apps/api/Infrastructure/Notifications/RedisUserNotificationRateLimiter.cs` (new)
- `apps/api/Infrastructure/Notifications/FakeUserNotificationRateLimiter.cs` (new)
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` (modified — added `UserNotificationSent`)
- `apps/api/Domain/RoleManagement/UserManagementService.cs` (modified — pass `organisationId` to Hangfire job)
- `tests/api.integration/UserStatusEmailJobTests.cs` (new)
- `tests/api.integration/AuthWebApplicationFactory.cs` (modified — added `NotificationRateLimiter` + DI replacement)
- `tests/api.unit/Domain/RoleManagement/UserManagementServiceTests.cs` (modified — updated testable overrides for new signature)
- `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs` (modified — register `RedisUserNotificationRateLimiter`)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — updated status to in-progress, then review)

### Change Log

| Date | Change |
|------|--------|
| 2026-06-28 | Story created — user notification emails for suspension and deletion, extending existing UserStatusEmailJob |
| 2026-06-28 | Validation applied: corrected architecture (Hangfire, not new BackgroundService), removed unnecessary EF table/migration/JobRunner, added rate limiter + audit injection into existing job |
| 2026-06-29 | Implemented: `IUserNotificationRateLimiter` + Redis + fake, `AuditEventTypes.UserNotificationSent`, `UserStatusEmailJob` rate-limit + audit + orgId, `UserManagementService` orgId propagation, enriched templates, integration tests (6 scenarios), unit test fixes, DI registration |

## Review Findings

### Patch (fixable without human input)

- [x] [Review][Patch] HTTP CancellationToken captured in Hangfire lambda — jobs silently fail after request ends [UserManagementService.cs:345-354]
- [x] [Review][Patch] MetadataJson test assertion compares raw string against full JSON blob [UserStatusEmailJobTests.cs:129]
- [x] [Review][Patch] Deletion email uses Guid.Empty as userId — audit event has no user reference [UserManagementService.cs:351-355]
- [x] [Review][Patch] Rate-limit increment before audit — audit failure causes irreversible silent skip [UserStatusEmailJob.cs:68-83]
- [x] [Review][Patch] TOCTOU race in Redis rate limiter — check-then-act gap allows limit to be exceeded [RedisUserNotificationRateLimiter.cs:12-26]
- [x] [Review][Patch] Key expiration not atomic with INCR — zombie keys on crash between INCR and KeyExpire [RedisUserNotificationRateLimiter.cs:25-28]
- [x] [Review][Patch] Empty display name when both names are null — `" ".Trim()` produces `""` [UserManagementService.cs:136-137, 213-214]
- [x] [Review][Patch] Nested `<p>` tag in suspension email when reason provided — invalid HTML [UserStatusEmailJob.cs:35]
- [x] [Review][Patch] Missing test: suspension email with null/empty reason (no-reason branch)
- [x] [Review][Patch] Missing test: concurrent rate-limit enforcement (parallel invocations)
- [x] [Review][Patch] Missing test: partial-failure recovery (email sent, audit fails)
- [x] [Review][Patch] Missing test: HTML injection in reason/name field
- [x] [Review][Patch] Missing test: very long reason/name strings

### Deferred (pre-existing, not caused by this change)

- [x] [Review][Defer] HTML email body sent as `text/plain` — pre-existing SmtpEmailSender issue
- [x] [Review][Defer] Notification may be silently lost if `BackgroundJob.Enqueue` throws after CommitAsync — pre-existing
- [x] [Review][Defer] Rate limiter uses calendar-day bucket, not 24-hour sliding window — design trade-off, matches spec wording
- [x] [Review][Defer] Rate-limit skip is completely silent — Director receives no feedback — not required by AC
- [x] [Review][Defer] SubjectUserId in deletion audit changed from null to targetUserId — intentional, snapshot preserves original identity
