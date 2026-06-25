---
baseline_commit: ecbb4467a029193a3b63312db47c0c5ed40ad8b1
---

# Story 1.13: Vendor Safety Net — Zero-Director Recovery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Vendor,
I want to be automatically notified when an organisation has zero active Directors and be able to re-issue an activation link,
So that organisations aren't permanently stranded.

## Acceptance Criteria

1. **Given** an organisation's last active Director is deleted or deactivated (suspended/role-changed away from Director)
   **When** the action is committed
   **Then** the system immediately detects this as a zero-Director state
   **And** the Vendor backstage shows a Safety Net Banner on that organisation's detail page
   **And** an email alert is sent to the Vendor: "Organisation {name} has no active Directors. Issue a new activation link to resume operations."
   **And** the Vendor dashboard (org list) shows a visual indicator for organisations in zero-Director state

2. **Given** a Vendor sees the Safety Net Banner on an organisation detail page
   **When** the Vendor enters a new Director email and clicks "Send Activation Link"
   **Then** a new activation token is generated for the existing organisation (not a new org)
   **And** the existing organisation record is preserved — existing users, invitations, and audit log remain intact
   **And** the activation link is emailed to the target address
   **And** the new Director can follow the standard activation flow (Story 1.12) to register
   **And** on registration, the organisation becomes active again (`is_active = true`)
   **And** the new Director sees the existing team roster — Coordinator and Field Worker accounts are unaffected

3. **Given** a Vendor navigates to an organisation detail page for an org in zero-Director state
   **When** the detail page loads
   **Then** a prominent banner is displayed at the top: "No active Directors. Send a new activation link to resume operations."
   **And** the banner is dismissible only after successfully sending a new activation link
   **And** the page shows the last known Director name and the date they were last active (to help the Vendor verify the situation before issuing the reset)

4. **Given** the event-driven detection of zero-Director state fails (e.g., the service restarts mid-operation)
   **When** the periodic monitoring check runs
   **Then** it detects organisations with `is_active = true` (already activated) that have zero active Directors
   **And** triggers the same recovery flow (Vendor email alert + dashboard indicator)
   **And** runs within [ASSUMPTION: 1 hour] as a Hangfire recurring job

5. **Given** a new activation link is sent for an existing organisation
   **When** the old activation (from initial bootstrap, Story 1.11) has already been consumed or expired
   **Then** the new link is independent — the old consumed/expired state does not affect the new link
   **And** the new link has its own 7-day expiry (reusing the same `ACTIVATION_TOKEN_TTL_HOURS` config)

6. **Given** the re-issue activation link endpoint is called
   **When** requests exceed the rate limit
   **Then** returns HTTP 429 with `Retry-After` header
   **And** the rate limit matches the existing vendor-create policy (10 req/min, 5 req/h per email)

## Tasks / Subtasks

### API — Last Director Detection Service

- [x] **Create/Implement LastDirectorGuard** — `apps/api/Domain/RoleManagement/LastDirectorGuard.cs`
  - Method: `Task<bool> IsLastActiveDirectorAsync(Guid organisationId, Guid userId, CancellationToken ct)`
    - Count active Directors in the organisation (role = Director, IsActive = true, IsSuspended = false)
    - If count is 1 and the target user matches, they are the last Director
  - Method: `Task<bool> HasAnyActiveDirectorAsync(Guid organisationId, CancellationToken ct)`
    - Returns true if at least one active Director exists in the org
  - Method: `Task<LastDirectorInfo?> GetLastKnownDirectorInfoAsync(Guid organisationId, CancellationToken ct)`
    - Returns `LastDirectorInfo` record with `Name` (string?) and `LastActiveAt` (DateTime?)
    - Queries audit events for the most recent Director-related activity (user_created with Director role, user_deleted) to determine the last known Director name and when they were last active
    - Returns null when org has never had a Director (initial bootstrap scenario — not zero-Director recovery)

### API — Zero-Director Event Trigger Service

- [x] **Create ZeroDirectorTriggerService** — `apps/api/Domain/RoleManagement/ZeroDirectorTriggerService.cs`
  - Contains the `LastDirectorInfo` record: `public sealed record LastDirectorInfo(string? Name, DateTime? LastActiveAt);`
  - A self-contained service that other endpoints call AFTER completing user suspension/deletion/role-change
  - Method: `Task NotifyUserRemovedAsync(Guid organisationId, Guid userId, CancellationToken ct)`:
    1. Load the removed user — if they were NOT a Director, return immediately (no-op)
    2. Check `LastDirectorGuard.HasAnyActiveDirectorAsync(organisationId)`
    3. If false (no active Directors remain), trigger recovery:
       - Set `Organisation.HasPendingRecovery = true`
       - Enqueue Hangfire fire-and-forget job: `ZeroDirectorAlertJob.Execute(orgId)`
    4. All in a single DB transaction
  - This service is designed to be called from future controllers (Story 2-13 suspension, 2-14 deletion) when those stories are implemented
  - For Story 1.13, only the monitoring fallback (AC-4) provides the safety net — the event-driven detection trigger (AC-1) will be wired in when the suspend/delete endpoints exist

- **Future wiring note:** When Story 2-13 (user suspension) and 2-14 (permanent deletion) are implemented, their controllers must call `ZeroDirectorTriggerService.NotifyUserRemovedAsync()` AFTER committing the action but BEFORE returning the response. This story creates the trigger infrastructure but AC-1 won't fire until those stories exist.

### API — ZeroDirectorAlertJob (Hangfire)

- [x] **Create ZeroDirectorAlertJob** — `apps/api/Jobs/ZeroDirectorAlertJob.cs`
  - On execution:
    1. Look up the organisation and verify it still has zero active Directors (re-check to avoid race conditions)
    2. If zero Directors confirmed:
       - Send email alert to all Vendor accounts: "Organisation {name} has no active Directors. Issue a new activation link to resume operations."
       - If the org has a known last Director, include their name and last active date in the email
    3. If a Director was re-added between trigger and execution, skip (no alert needed)
  - Email uses existing `IEmailSender` and email template system
  - The eventual recovery is handled via the Vendor backstage UI (see below)

### API — Hangfire Monitoring Job (Fallback)

- [x] **Create ZeroDirectorMonitorJob** — `apps/api/Jobs/ZeroDirectorMonitorJob.cs`
  - Recurring job (every hour): scans for organisations where `IsActive = true` AND they have zero active Directors
  - For each such org:
    - If no alert has been sent in the last hour (dedup via Redis or a `last_alert_sent_at` column), send the alert
    - Tracks last alert time to avoid flooding the Vendor with duplicate emails
  - Register in `Program.cs` as a Hangfire recurring job with cron expression for hourly execution
  - Protected by `isTesting()` guard — not registered in test environment

### API — Rate Limit Policy Registration

- [x] **Add `vendor-read` rate limit policy** — `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
  - Add a new policy: `options.AddPolicy("vendor-read", VendorReadRateLimitPartition)`
  - Uses a higher permit limit (e.g., 60 req/min per authenticated Vendor) since these are read-only list/detail calls
  - Does NOT share budget with the `vendor-create` policy (mutation endpoints)
  - Pattern follows existing `CreateAuthRateLimitPartition` helper but with a different permit limit
  - This policy is applied to GET endpoints in `OrganisationsController`

### API — Vendor Organisation List & Detail Endpoints

- [x] **Extend OrganisationsController** — `apps/api/Controllers/V1/Vendor/OrganisationsController.cs`
  - `GET /api/v1/vendor/organisations` — list all organisations
    - Returns `ApiResponse<List<VendorOrganisationSummary>>` with fields: `id`, `name`, `isActive`, `directorCount`, `hasPendingRecovery`, `createdAtUtc`
    - Requires `[Authorize(Policy = Policies.VendorOnly)]` + `[Require2FA]`
    - Rate limited via a read-friendly policy (`vendor-read` with higher permit limit, e.g. 60 req/min) — NOT the `vendor-create` policy since it should not compete with the POST creation budget
    - Orders by `created_at_utc DESC`
  - `GET /api/v1/vendor/organisations/{id}` — single org detail
    - Returns `ApiResponse<VendorOrganisationDetail>` with fields: `id`, `name`, `isActive`, `directorCount`, `hasPendingRecovery`, `lastKnownDirectorName`, `lastKnownDirectorActiveAt`, `createdAtUtc`
    - `lastKnownDirectorName` and `lastKnownDirectorActiveAt` are null unless org is in zero-Director state (then populated from audit trail)
    - Rate limited via `vendor-read` policy (same as list endpoint)
  - `POST /api/v1/vendor/organisations/{id}/reissue-activation` — re-issue activation link for existing org
    - Accepts `ReissueActivationRequest` with: `targetDirectorEmail` (string, required, valid email)
    - Validates the org exists
    - Validates that the org is in zero-Director state (re-check; if a Director was added between page load and request, return 409 Conflict)
    - Generates a new activation token via `TokenService.GenerateActivationToken()`
    - Stores the token linked to the existing org (same org ID, not a new org)
    - Sends the activation link via `ActivationEmailDeliveryJob` (reuse existing)
    - Does NOT set `Organisation.HasPendingRecovery = false` — that happens when the new Director activates (the RegistrationService already sets `IsActive = true` on activation)
    - Returns `ApiResponse<ReissueActivationResponse>` with: `status` ("sent" or "delivery_failed"), `targetDirectorEmail`
    - Rate limited via vendor-create policy (same as initial org creation)
    - Requires `[Authorize(Policy = Policies.VendorOnly)]` + `[Require2FA]`
  - **New DTOs** (in `apps/api/Models/Vendor/` — extend existing Vendor DTOs file):
    - `VendorOrganisationSummary` record: `Id`, `Name`, `IsActive`, `DirectorCount`, `HasPendingRecovery`, `CreatedAtUtc`
    - `VendorOrganisationDetail` record: adds `LastKnownDirectorName`, `LastKnownDirectorActiveAt`
    - `ReissueActivationRequest`: `TargetDirectorEmail` with `[Required, EmailAddress, StringLength(320)]`
    - `ReissueActivationResponse`: `Status`, `TargetDirectorEmail`

### API — OrganisationService Extensions

- [x] **Extend OrganisationService** — `apps/api/Domain/RoleManagement/OrganisationService.cs`
  - `ReissueActivationAsync(Guid organisationId, string targetDirectorEmail, CancellationToken ct)`:
    1. Load the organisation — throw `ValidationException` if not found
    2. Verify zero-Director state via `LastDirectorGuard.HasAnyActiveDirectorAsync()` — throw `ValidationException` with message "This organisation already has active Directors." if false
    3. Validate email format (reuse existing validation pattern)
    4. Check per-email rate limit (reuse existing `IsEmailRateLimitedAsync`)
    5. Generate new activation token via `TokenService.GenerateActivationToken()`
    6. Create `ActivationToken` row linked to existing `OrganisationId` (same fields as Story 1.11: `TokenHash`, `TargetEmail`, `ExpiresAtUtc = now + ACTIVATION_TOKEN_TTL_HOURS`, `DeliveryAttempts = 0`, `CreatedAtUtc = now`)
    7. Persist in a single transaction
    8. Send initial email (reuse existing email sending pattern)
    9. Return result: `(organisationId, name, status, activationTokenId)`
  - `GetOrganisationListAsync()` — returns list of orgs with director counts
  - `GetOrganisationDetailAsync(Guid id)` — returns single org detail with last known Director info

### API — Data Model Extension

- [x] **Extend Organisation entity** — `apps/api/Domain/Entities/Organisation.cs`
  - Add `HasPendingRecovery` (bool, default false) — set to true when zero-Director state detected, used by monitoring job and vendor UI to show the safety net state
  - Add EF configuration for the new column in `OrganisationConfiguration.cs`
  - Generate EF Core migration for: ALTER TABLE organisations ADD COLUMN has_pending_recovery BOOLEAN NOT NULL DEFAULT FALSE;

### API — RegistrationService Must Clear HasPendingRecovery

- [x] **Extend RegistrationService** — `apps/api/Domain/RoleManagement/RegistrationService.cs`
  - In `ExecuteActivationAsync`, after successfully creating the Director user and activating the org, add:
    - If the organisation's `HasPendingRecovery` was true, set it to false
    - This ensures that when a new Director activates via re-issued link (Story 1.13), the recovery state is cleared
  - This field is persisted via `db.SaveChangesAsync()` which is already called in the same transaction

### Web — Vendor Backstage: Organisation Roster

- [x] **Extend VendorComponent** — `apps/web/src/app/features/vendor/vendor.component.ts`
  - On init, fetch the org list via `VendorApiService.getOrganisations()`
  - Display a table/card list of organisations with columns: Name, Status (Active/Pending/Recovery), Directors count
  - For orgs in zero-Director state (`hasPendingRecovery = true`), show a visual indicator (amber badge: "Recovery Needed")
  - Each row is clickable → navigates to org detail view (inline or route)
  - After successfully creating a new org, refresh the list

- [x] **Extend VendorApiService** — `apps/web/src/app/features/vendor/vendor-api.service.ts`
  - Add TypeScript interfaces:
    ```typescript
    export interface VendorOrganisationSummary {
      id: string;
      name: string;
      isActive: boolean;
      directorCount: number;
      hasPendingRecovery: boolean;
      createdAtUtc: string;
    }
    export interface VendorOrganisationDetail extends VendorOrganisationSummary {
      lastKnownDirectorName: string | null;
      lastKnownDirectorActiveAt: string | null;
    }
    export interface ReissueActivationRequest {
      targetDirectorEmail: string;
    }
    export interface ReissueActivationResponse {
      status: string;
      targetDirectorEmail: string;
    }
    ```
  - Add `getOrganisations(): Promise<VendorOrganisationSummary[]>`
  - Add `getOrganisationDetail(id: string): Promise<VendorOrganisationDetail>`
  - Add `reissueActivation(organisationId: string, targetDirectorEmail: string): Promise<ReissueActivationResponse>`

### Web — Vendor Backstage: Organisation Detail & Safety Net Banner

- [x] **Create Org Detail panel** — within vendor component or as a sub-component
  - Shows org name, status, Director count
  - When `hasPendingRecovery = true`:
    - **Safety Net Banner**: prominent amber banner at top: "This organisation has no active Directors. Send a new activation link to resume operations."
    - Banner is dismissible only after a new link is sent successfully
    - Shows last known Director name and last active date (when available)
    - Shows a form: "New Director Email" input + "Send Activation Link" button
    - On success: banner replaced with success message "Activation link sent to {email}"
    - On error: show error in banner area
  - POSTs to `/api/v1/vendor/organisations/{id}/reissue-activation`

### Tests

- [x] **Unit tests** — `tests/api.unit/Domain/RoleManagement/LastDirectorGuardTests.cs`
  - IsLastActiveDirectorAsync returns true when user is the only active Director
  - IsLastActiveDirectorAsync returns false when there are other active Directors
  - HasAnyActiveDirectorAsync returns false when org has zero active Directors
  - HasAnyActiveDirectorAsync returns false when org has suspended Directors only
  - GetLastKnownDirectorInfoAsync returns null when org has never had a Director
  - All using InMemory database with `TestableRegistrationService`-like pattern

- [x] **Unit tests** — `tests/api.unit/Domain/RoleManagement/OrganisationServiceTests.cs`
  - Extend existing tests with ReissueActivation scenarios:
    - ReissueActivation succeeds for org in zero-Director state
    - ReissueActivation throws when org has active Directors
    - ReissueActivation throws when org not found

- [x] **Integration tests** — `tests/api.integration/Controllers/Vendor/OrganisationsControllerTests.cs`
  - GET organisations list returns results
  - GET organisation detail returns correct fields
  - GET organisation detail shows last known Director info for zero-Director org
  - POST reissue-activation succeeds for zero-Director org
  - POST reissue-activation returns 409 when org has Directors
  - POST reissue-activation returns 404 for non-existent org
  - POST reissue-activation is unauthenticated returns 401
  - POST reissue-activation without Vendor role returns 403

## Dev Notes

### Relevant Architecture Patterns & Constraints

- **Token generation pattern**: Three-step process from architecture: generate (SHA-256 hash) → sign (HMAC) → embed (URL). Reuse `TokenService` from Story 1.11. [Source: `architecture-role-management.md §Link/Token Generation Pattern`]
- **Audit events**: Every management action must write an audit event in the same DB transaction — fail-closed. The reissue-activation is a management action. [Source: `architecture-role-management.md §Audit Event Pattern`]
- **Existing vendor endpoints**: `OrganisationsController` already exists at `apps/api/Controllers/V1/Vendor/OrganisationsController.cs`. Extend it — do not create a new controller.
- **Existing OrganisationService**: Already has `CreateOrganisationAsync` with email rate limiting, token generation, and initial email delivery. Extend with `ReissueActivationAsync` and list/detail methods. `IsEmailRateLimitedAsync` is a private method on `OrganisationService` — correctly scoped for internal reuse by the reissue-activation flow; no external callers needed.
- **Existing vendor Angular component**: `apps/web/src/app/features/vendor/vendor.component.ts` is a standalone component. Extend it with org list/detail — do not create a new module.
- **LastDirectorGuard**: Does not exist yet. Create it as `apps/api/Domain/RoleManagement/LastDirectorGuard.cs`. All queries are read-only so use `AsNoTracking()` for performance. Note: the architecture document lists it in the extension tree.
- **Rate limiting**: GET endpoints (list/detail) use a new `vendor-read` policy with a higher permit limit (60 req/min). The reissue endpoint (mutation) uses the existing `vendor-create` policy (10 req/min per IP). Per-email rate limit: 5 req/h per email. [Source: `architecture.md §FR-1`, `epics.md Story 1.2`]
- **Error format**: RFC 7807 Problem Details. 400 validation, 401 unauthenticated, 403 forbidden, 404 not found, 409 conflict, 429 rate limit. [Source: `project-context.md §C# / ASP.NET Core (API)`]
- **API response envelope**: `{ data, meta: { requestId } }`. [Source: `project-context.md §ASP.NET Core API`]
- **Vendor backstage auth**: `[Authorize(Policy = Policies.VendorOnly)]` + `[Require2FA]` on all vendor management endpoints. [Source: `architecture-role-management.md §Authentication & Security`]

### Source Tree Components to Touch

```
apps/api/
├── Controllers/V1/Vendor/OrganisationsController.cs          # EXTEND: list, detail, reissue endpoints
├── Domain/
│   ├── Entities/Organisation.cs                              # EXTEND: add HasPendingRecovery
│   ├── RoleManagement/
│   │   ├── LastDirectorGuard.cs                              # NEW: last Director detection
│   │   └── OrganisationService.cs                            # EXTEND: ReissueActivation, list, detail
├── Infrastructure/
│   └── Persistence/
│       ├── OrganisationConfiguration.cs                      # EXTEND: HasPendingRecovery config
│       └── Migrations/                                       # NEW: AddHasPendingRecovery migration
├── Jobs/
│   ├── ZeroDirectorAlertJob.cs                               # NEW: fire-and-forget alert
│   └── ZeroDirectorMonitorJob.cs                             # NEW: recurring monitoring
├── Models/Vendor/                                            # EXTEND: new DTOs for list/detail/reissue
└── Program.cs                                                # EXTEND: register recurring job

apps/web/src/app/features/vendor/
├── vendor.component.ts                                       # EXTEND: org list + detail + safety net
├── vendor.component.html                                     # EXTEND: org roster table, detail panel
├── vendor.component.scss                                     # EXTEND: safety net banner styles
└── vendor-api.service.ts                                     # EXTEND: list, detail, reissue API calls

tests/
├── api.unit/Domain/RoleManagement/
│   ├── LastDirectorGuardTests.cs                             # NEW
│   └── OrganisationServiceTests.cs                           # EXTEND
└── api.integration/Controllers/Vendor/
    └── OrganisationsControllerTests.cs                        # EXTEND
```

### Testing Standards Summary

- xUnit for all tests
- Unit tests: InMemory database with test-specific overrides (see Story 1.12 `TestableRegistrationService` pattern)
- Integration tests: `AuthWebApplicationFactory` + Testcontainers PostgreSQL (same as existing `RegistrationControllerTests`)
- Must test: RBAC denial (401 without auth, 403 without Vendor role), duplicate reissue (409), audit writes, zero-Director edge cases
- Mock `IEmailSender` and `IConnectionMultiplexer` in unit tests where needed
- The recurring monitoring job can be tested via direct invocation in unit tests (not by running actual Hangfire scheduler)

### Project Structure Notes

- Aligns with existing monorepo structure — extends existing files rather than creating new modules
- `LastDirectorGuard` follows the pattern established in `architecture-role-management.md §Project Structure & Boundaries` but note: the architecture lists it in `Domain/RoleManagement/` directly (no interface prefix required if it's simple)
- Vendor backstage UI is a standalone component (no app shell), following the pattern from Story 1.11
- The safety net banner flow aligns with `EXPERIENCE.md §Flow 6 — Vendor safety net` and `EXPERIENCE.md §State Patterns — Safety Net Banner`

### References

- [Source: `epics.md` — Story 1.4 (1-13)] Full acceptance criteria
- [Source: `architecture-role-management.md §Authentication & Security`] Link signing, rate limiting, vendor auth patterns
- [Source: `architecture-role-management.md §Project Structure & Boundaries`] Extension tree for new files
- [Source: `EXPERIENCE.md §Flow 6 — Vendor safety net`] UX flow for Grace's recovery flow
- [Source: `EXPERIENCE.md §State Patterns`] Safety Net Banner UX treatment
- [Source: Story 1.12 `1-12-first-director-registration-flow.md`] Activation flow patterns, token consumption, testability patterns
- [Source: `project-context.md §Critical Implementation Rules`] Naming, error codes, envelope, auth patterns
- [Source: Story 1.11 OrganisationService] Existing CreateOrganisationAsync for pattern reference
- [Source: PRD §FR-3] Zero-Director recovery requirements

### Previous Story Learnings (Story 1.12)

- Testability: EF Core InMemory doesn't support transactions or raw SQL. Use `protected virtual` methods with test-specific overrides (`TestableRegistrationService` pattern) to bypass relational-provider requirements in unit tests. [Source: `1-12-first-director-registration-flow.md §Error 19`]
- Token validation consistency: always use `Encoding.UTF8.GetBytes(rawToken)` for hash/signature computation — not raw bytes. [Source: `1-12-first-director-registration-flow.md §Error 5`]
- Rate limiting: `HttpContext.Connection.Id` is unstable — use `"unknown"` as fallback instead. [Source: `1-12-first-director-registration-flow.md Patch P3`]
- Concurrent consumption: use raw SQL `UPDATE ... WHERE consumed_at_utc IS NULL` for atomicity. [Source: `1-12-first-director-registration-flow.md §Tasks`]
- Angular components use signals + Reactive Forms. Standalone components for unauthenticated pages.
- Integration tests use AuthWebApplicationFactory from `tests/api.integration/`.

### Git Intelligence

- Recent commits show Story 1.12 implemented RegistrationService, RegistrationController, activation component, and all associated tests
- The existing `OrganisationsController.cs` and `OrganisationService.cs` were created in Story 1.11
- No `LastDirectorGuard` exists in the codebase yet — it's a new file
- `Organisation.cs` entity does not yet have `HasPendingRecovery` — needs migration

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

- Build: API project builds with 0 errors
- Unit tests: 18 new tests pass (83/84 total — 1 pre-existing failure unrelated to story)
- Integration tests: Created (require Docker/Testcontainers)

### Completion Notes List

- ✅ Created LastDirectorGuard with IsLastActiveDirectorAsync, HasAnyActiveDirectorAsync, GetLastKnownDirectorInfoAsync
- ✅ Created ZeroDirectorTriggerService with NotifyUserRemovedAsync + LastDirectorInfo record
- ✅ Created ZeroDirectorAlertJob (fire-and-forget Hangfire) for Vendor email alerts
- ✅ Created ZeroDirectorMonitorJob (recurring Hangfire, hourly) as fallback detection
- ✅ Added HasPendingRecovery to Organisation entity + EF configuration
- ✅ Registered `vendor-read` rate limit policy (60 req/min)
- ✅ Extended OrganisationService with ReissueActivationAsync, GetOrganisationListAsync, GetOrganisationDetailAsync
- ✅ Extended RegistrationService to clear HasPendingRecovery on activation
- ✅ Extended OrganisationsController with GET list, GET detail, POST reissue-activation endpoints
- ✅ Extended Vendor Angular component with org roster table, detail panel, safety net banner
- ✅ Added TypeScript interfaces and API methods to VendorApiService
- ✅ 18 unit tests for LastDirectorGuard + OrganisationService
- ✅ Integration tests for OrganisationsController (require Docker)
- ⚠️ EF Core migration not generated (PostgreSQL provider required — uncomment after restoring)

### File List

**New Files:**
- `apps/api/Domain/RoleManagement/LastDirectorGuard.cs`
- `apps/api/Domain/RoleManagement/ZeroDirectorTriggerService.cs`
- `apps/api/Jobs/ZeroDirectorAlertJob.cs`
- `apps/api/Jobs/ZeroDirectorMonitorJob.cs`
- `apps/api/Models/Vendor/VendorOrganisationDtos.cs`
- `tests/api.unit/Domain/RoleManagement/LastDirectorGuardTests.cs`
- `tests/api.unit/Domain/RoleManagement/OrganisationServiceTests.cs`
- `tests/api.integration/Controllers/Vendor/OrganisationsControllerTests.cs`

**Modified Files:**
- `apps/api/Controllers/V1/Vendor/OrganisationsController.cs`
- `apps/api/Domain/Entities/Organisation.cs`
- `apps/api/Domain/RoleManagement/OrganisationService.cs`
- `apps/api/Domain/RoleManagement/RegistrationService.cs`
- `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
- `apps/api/Infrastructure/Persistence/OrganisationConfiguration.cs`
- `apps/api/Program.cs`
- `apps/web/src/app/features/vendor/vendor.component.ts`
- `apps/web/src/app/features/vendor/vendor.component.html`
- `apps/web/src/app/features/vendor/vendor.component.scss`
- `apps/web/src/app/features/vendor/vendor-api.service.ts`

## Review Findings

### Decision Needed

- [x] [Review][Decision] D1: TOCTOU race in ReissueActivationAsync — Resolved: wrap in serializable transaction. Add BeginTransactionAsync with System.Data.IsolationLevel.Serializable around the HasAnyActiveDirectorAsync check through token persistence.
- [x] [Review][Decision] D2: Dot-separated audit event types convention — Resolved: use underscores (e.g. "user_created", "organisation_zero_director_alert") for all audit event types project-wide.

### Patch

- [x] [Review][Patch] P1: Missing audit event on reissue-activation [OrganisationService.cs:250]
- [x] [Review][Patch] P2: Invalid email maps to 404 instead of 400 [OrganisationsController.cs:102-122]
- [x] [Review][Patch] P3: Exact-string matching on ex.Message for status code selection [OrganisationsController.cs:105]
- [x] [Review][Patch] P4: showList() fires loadOrganisations() without await [vendor.component.ts:1047]
- [x] [Review][Patch] P5: lastKnownDirectorActiveAt displayed as raw ISO string (no date pipe) [vendor.component.html:595-596]
- [x] [Review][Patch] P6: Missing type="button" on nav buttons [vendor.component.html:497-500]
- [x] [Review][Patch] P7: Reissue doesn't enqueue ActivationEmailDeliveryJob retry on failure [OrganisationService.cs:233-248]
- [x] [Review][Patch] P8: Status value "activation_sent" vs spec's "sent" [OrganisationService.cs:253]
- [x] [Review][Patch] P9: DeliveryAttempts starts at 1 instead of 0 [OrganisationService.cs:219]
- [x] [Review][Patch] P10: ZeroDirectorMonitorJob doesn't skip orgs already in recovery [ZeroDirectorMonitorJob.cs:56]
- [x] [Review][Patch] P11: LastDirectorGuard.IsLastActiveDirectorAsync second AnyAsync omits IsActive/IsSuspended [LastDirectorGuard.cs:22-26]
- [x] [Review][Patch] P12: Wrap ReissueActivationAsync in serializable transaction [OrganisationService.cs:186-250]
- [x] [Review][Patch] P13: Change audit event types from dot to underscore notation [LastDirectorGuard.cs:55, ZeroDirectorAlertJob.cs:48, ZeroDirectorMonitorJob.cs:48]

### Deferred

- [x] [Review][Defer] F1: No database migration included — acknowledged in story file, PostgreSQL provider needed
- [x] [Review][Defer] F2: Controller catches RateLimitExceededException from service layer — pre-existing pattern used elsewhere
- [x] [Review][Defer] F3: Dedup uses audit events instead of Redis/column — reasonable alternative, works correctly

## Change Log

- **2026-06-24**: Story 1.13 implemented. Added LastDirectorGuard, ZeroDirectorTriggerService, ZeroDirectorAlertJob, ZeroDirectorMonitorJob, HasPendingRecovery on Organisation, vendor-read rate limit, OrganisationService extensions, RegistrationService recovery clear, OrganisationsController list/detail/reissue, Vendor Angular UI with safety net banner, 18 unit tests, and integration tests.
- **2026-06-24**: Code review completed. 2 decision-needed, 11 patch, 3 deferred findings identified.
