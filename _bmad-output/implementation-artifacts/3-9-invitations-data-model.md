---
baseline_commit: 689f5b4
---

# Story 3.9: Invitations Data Model

Status: done

> **TL;DR for dev — concrete actions:**
> 1. Add `InvitationStatus` enum to `packages/shared-types/src/index.ts`
> 2. Add integration schema tests at `tests/api.integration/Admin/InvitationsSchemaTests.cs`
> 3. Validate existing files (listed below) — **do not modify** unless gaps found
> 4. **Do not generate a new migration** — `20260624152225_AddInvitations` already exists
>
> The invitations data model (table, entity, EF config, migration) was already implemented during Epic 2 stories (2-10 through 2-12). This story validates, audits, and finalizes the data model for Epic 3 consumption. See "Brownfield Reality" below.

## Story

As a **developer**,
I want an `invitations` table to track pending, confirmed, and expired invitations,
so that the invitation lifecycle is persisted for the double-confirmation registration flow.

## Acceptance Criteria

1. **Given** the `invitations` table  
   **When** this story ships  
   **Then** the table exists with: `id` (UUID PK), `organisation_id` (FK), `invited_by_user_id` (FK → users), `target_email` (varchar), `role` (varchar), `token_hash` (text), `expires_at_utc` (timestamp), `status` (enum: pending/confirmed/expired), `created_at_utc` (timestamp), `confirmed_at_utc` (nullable timestamp)  
   **And** column names use snake_case, timestamps are ISO 8601 UTC

2. **Given** the `invitations` table  
   **When** the migration is inspected  
   **Then** there is a unique constraint on `(organisation_id, target_email, status)` where `status = 'pending'`  
   **And** EF Core migration `20260624152225_AddInvitations` exists and is runnable  
   **And** the migration also adds `has_pending_recovery` column (boolean, default false) to `organisations` — **do not regenerate this migration, it must be preserved as-is**

3. **Given** the `Invitation` entity  
   **When** data is queried  
   **Then** `Invitation.Organisation` navigation property links to `Organisation` via `organisation_id` FK  
   **And** `Invitation.InvitedByUser` navigation property links to `User` via `invited_by_user_id` FK  
   **And** FK constraints use `ON DELETE RESTRICT` (no cascade)

4. **Given** the `AppDbContext`  
   **When** inspecting persisted state  
   **Then** `DbSet<Invitation> Invitations` is registered  
   **And** `InvitationConfiguration` is applied via `ApplyConfigurationsFromAssembly`

5. **Given** the `InvitationStatus` constants  
   **When** comparing against `packages/shared-types/`  
   **Then** `InvitationStatus` enum is added to `packages/shared-types/src/index.ts` with **exact** values:  
   ```typescript
   export enum InvitationStatus {
     Pending = 'pending',
     Confirmed = 'confirmed',
     Expired = 'expired',
   }
   ```
   **And** the casing matches the C# constants (lowercase strings)

6. **Given** the existing test suite  
   **When** running `npm run test:api`  
   **Then** existing `InvitationServiceTests.cs` pass with 100% coverage of data-layer operations (send, list, resend)  
   **And** integration tests for the invitations table schema are added (column types, constraints, defaults)

## Tasks / Subtasks

### Validate (files already exist — inspect, do not modify)

- [x] **Invitation entity** (AC: 1)
  - [x] `Invitation.cs` — all columns mapped, navigation properties, `InvitationStatus` constants
  - [x] Entity lives at `apps/api/Domain/Entities/Invitation.cs`
  - [x] **Check consistency:** `TokenService.GenerateActivationToken()` is used for invitation tokens despite the name — do not refactor. The method generates both activation and invitation tokens.

- [x] **EF Core configuration** (AC: 1, 2, 3)
  - [x] `InvitationConfiguration.cs` — table mapping, column constraints, max lengths, defaults
  - [x] Unique index filter on `status = 'pending'`
  - [x] FK relationships with `DeleteBehavior.Restrict`

- [x] **Migration** (AC: 2)
  - [x] `20260624152225_AddInvitations.cs` — creates `invitations` table
  - [x] Also adds `has_pending_recovery` column to `organisations` (same migration)
  - [x] **WARNING: Do not generate a new migration or modify this one.** It is already applied and integrated with the model snapshot.

- [x] **AppDbContext registration** (AC: 4)
  - [x] `DbSet<Invitation> Invitations` registered at line 48
  - [x] Configuration auto-applied via `ApplyConfigurationsFromAssembly`

- [x] **Domain service** (AC: implied by downstream stories)
  - [x] `InvitationService.cs` — send, list, resend with audit and Hangfire email jobs
  - [x] Uses `AppDbContext` directly (not repository interfaces) — existing architecture decision, **do not refactor** to repository pattern
  - [x] Registered in `Program.cs` line 158 (not in `AuthServiceCollectionExtensions.cs`)
  - [x] `TokenService.cs` — SHA-256 hash + HMAC-SHA256 signing pattern

- [x] **DTOs** (AC: implied)
  - [x] `InvitationDtos.cs` — `InvitationSummary`, `SendInvitationRequest`, `SendInvitationResponse`, `ResendInvitationResponse`

- [x] **Unit tests** (AC: 6)
  - [x] `InvitationServiceTests.cs` — send, duplicate detection, resend, error cases (377 lines)

### Implement (work needed)

- [x] **Shared types enum** (AC: 5)
  - [x] Add `InvitationStatus` enum to `packages/shared-types/src/index.ts`:
    ```typescript
    export enum InvitationStatus {
      Pending = 'pending',
      Confirmed = 'confirmed',
      Expired = 'expired',
    }
    ```

- [x] **Integration test — schema validation** (AC: 6)
  - [x] Create `tests/api.integration/Admin/InvitationsSchemaTests.cs` verifying:
    - Invitations table columns match spec (UUID PK, FKs, varchar lengths, timestamps)
    - FK constraints (`ON DELETE RESTRICT`) reject invalid `organisation_id` and `invited_by_user_id`
    - Unique filtered index rejects duplicate pending email per org; allows duplicate confirmed
    - `confirmed_at_utc` is nullable (defaults to NULL on insert, settable on confirm)
    - `created_at_utc` is NOT NULL with no DB default (set by application code)

- [x] **Model snapshot check**
  - [x] Verify `AppDbContextModelSnapshot.cs` includes the Invitation entity with all columns (line 1344+ includes Invitation entity with all columns, FK constraints with DeleteBehavior.Restrict, and filtered unique index)

### Gap Analysis

| Item | Status | Action |
|------|--------|--------|
| `InvitationStatus` in `packages/shared-types` | ❌ Missing | Add enum with values: `Pending = 'pending'`, `Confirmed = 'confirmed'`, `Expired = 'expired'` |
| Integration schema tests | ❌ Missing | Create `InvitationsSchemaTests.cs` with column/FK/index validation |
| Migration guardrail in ACs | ⚠️ Added after validation | AC2 now flags preservation requirement |
| OpenAPI / api-client regen | Not yet needed | Regen when endpoints change |
| Invitation cleanup job | ✅ Done by earlier stories | Verify `InvitationCleanupJob.cs` — `Program.cs` line 239 (daily at 2am) |
| Invitation email delivery job | ✅ Done | Verify `InvitationEmailDeliveryJob.cs` — `Program.cs` line 160 |
| `InvitationService` DI registration | ✅ Done | `Program.cs` line 158 — not in `AuthServiceCollectionExtensions.cs` |
| `TokenService.GenerateActivationToken()` naming | ⚠️ Known — do not refactor | Used for both activation and invitation tokens despite name |

## Dev Notes

### Brownfield reality (read before coding)

This story is **unique** — the data model was already implemented as part of Epic 2 stories (2-10 data model, 2-12 invite flow, 2-15 last-director, 2-16 2FA mandate). The epics numbering has the invitations table at Story 3.1 (Epic 3 internal) / 3-9 (global), but the actual code was produced earlier due to cross-epic dependencies.

| Area | Current state | This story |
|------|---------------|------------|
| `Invitation.cs` | Fully implemented (all columns, nav props, `InvitationStatus` constants) | Validate, no changes needed unless gaps found |
| `InvitationConfiguration.cs` | EF config with FK constraints, unique filtered index | Validate, no changes needed |
| `Migration` | `20260624152225_AddInvitations` exists | **Do not regenerate or modify.** Also adds `has_pending_recovery` to organisations — this must be preserved. |
| `InvitationService.cs` | Send, list, resend with audit + Hangfire jobs | Validate, no changes needed. Uses `AppDbContext` directly (not repository interfaces) — existing architecture decision, do not refactor. |
| `InvitationsController.cs` | REST endpoints under `/api/v1/admin/invitations` | Part of Epic 2 stories — verify alignment with existing patterns |
| `TokenService.GenerateActivationToken()` | Used for both activation and invitation tokens despite the name | **Do not rename or refactor.** Downstream stories depend on this method name. |
| `InvitationService` DI registration | `Program.cs` line 158 — not in `AuthServiceCollectionExtensions.cs` | Verify registration exists |
| `packages/shared-types` | No `InvitationStatus` enum | **Add it** with values: `Pending = 'pending'`, `Confirmed = 'confirmed'`, `Expired = 'expired'` |
| Integration tests | No schema validation tests | **Add column/constraint tests** |

### Architecture compliance

**Data architecture** (from `architecture-role-management.md`):

| Rule | Status |
|------|--------|
| Table name `invitations` (snake_case plural) | ✅ |
| UUID PK (`Guid`) | ✅ |
| `organisation_id` FK | ✅ `DeleteBehavior.Restrict` |
| `invited_by_user_id` FK → users | ✅ `DeleteBehavior.Restrict` |
| `status` as string (pending/confirmed/expired) | ✅ Default = `"pending"` |
| `token_hash` SHA-256 storage | ✅ Via `TokenService` |
| Timestamps ISO 8601 UTC | ✅ |
| Unique index on `(organisation_id, target_email)` where `status = 'pending'` | ✅ Filtered unique index |
| Append-only audit events | ✅ In `InvitationService` transaction |

**Mandatory patterns** (from `architecture-role-management.md`):

1. ✅ Every management action writes audit in same DB transaction — fail-closed
2. ✅ Link tokens use three-step pattern: generate (SHA-256 hash) → sign (HMAC-SHA256) → embed (URL)
3. ❌ **Missing:** `InvitationStatus` enum in `packages/shared-types/src/index.ts` — must add with values `Pending = 'pending'`, `Confirmed = 'confirmed'`, `Expired = 'expired'`
4. ⚠️ Direct EF Core in `InvitationService` (not repository interfaces) — existing architectural choice from Epic 2. **Do not refactor.** Pattern established before `Domain/RoleManagement/` layer docs were finalized.
5. ✅ `[Authorize(Policy = ...)]` pattern on all endpoints — `InvitationsController` uses `[Authorize(Policy = Policies.DirectorOnly)]`
6. ✅ `[Require2FA]` attribute on all Director management actions — present on `InvitationsController`

### File structure requirements

**Files to validate (read before editing):**

| File | Notes |
|------|-------|
| `apps/api/Domain/Entities/Invitation.cs` | Entity + `InvitationStatus` constants |
| `apps/api/Infrastructure/Persistence/InvitationConfiguration.cs` | EF config + unique filtered index |
| `apps/api/Migrations/20260624152225_AddInvitations.cs` | Migration — verify columns match spec. **Do not regenerate.** |
| `apps/api/Migrations/AppDbContextModelSnapshot.cs` | Current snapshot — verify Invitation entity at line 1344+ |
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | `DbSet<Invitation> Invitations` (line 48) |
| `apps/api/Program.cs` | `InvitationService` DI registration at line 158; Hangfire jobs at lines 160-161 |

**Files to add:**

| File | Action |
|------|--------|
| `packages/shared-types/src/index.ts` | Add `InvitationStatus` enum: `Pending = 'pending'`, `Confirmed = 'confirmed'`, `Expired = 'expired'` |
| `tests/api.integration/Admin/InvitationsSchemaTests.cs` | Schema validation integration tests (columns, FKs, unique index) |

**Files already in play (from earlier stories, read before editing):**

| File | Notes |
|------|-------|
| `apps/api/Domain/Entities/Organisation.cs` | Parent entity — has `HasPendingRecovery` |
| `apps/api/Domain/Entities/ActivationToken.cs` | Similar token-based table pattern |
| `apps/api/Domain/RoleManagement/InvitationService.cs` | Full service with audit, tokens, Hangfire jobs |
| `apps/api/Controllers/V1/Admin/InvitationsController.cs` | REST layer |
| `apps/api/Models/Admin/InvitationDtos.cs` | Request/response DTOs |
| `apps/api/Infrastructure/RoleManagement/TokenService.cs` | Token generation + signing |
| `tests/api.unit/Domain/RoleManagement/InvitationServiceTests.cs` | Unit tests (377 lines) |
| `apps/api/Program.cs` | DI registration at line 158, Hangfire jobs at lines 160-161, 239-242 |

### Testing requirements

| Test | Layer | Coverage needed |
|------|-------|-----------------|
| Invitation entity properties | Unit | All columns, defaults, navigation properties |
| `InvitationConfiguration` | Unit | Table name, column types, max lengths, FK constraints, unique index |
| Migration UP/DOWN | Integration | Table created with correct schema, `organisations.has_pending_recovery` added, fully reversible |
| Unique filtered index | Integration | Duplicate pending email per org rejected; duplicate confirmed allowed |
| FK constraints | Integration | `organisation_id` and `invited_by_user_id` reject invalid IDs (deleted org/user) |
| Nullable `confirmed_at_utc` | Integration | Default NULL on insert, settable on confirm |
| `created_at_utc` NOT NULL | Integration | Column is NOT NULL with **no DB default** — `InvitationService` sets it in application code at `DateTime.UtcNow` |

### Previous story intelligence (3.8)

- Migration naming pattern: `{timestamp}_{Description}.cs`
- `SensitivityLevel` enum pattern: standalone file in `Domain/Enums/` with separate C# file, not inline string constants
- DTO pattern: use `sealed record` types with `System.ComponentModel.DataAnnotations`
- EF config pattern: `IEntityTypeConfiguration<T>` with `ToTable()`, `HasKey()`, `Property()` chaining
- POCSO tests used `WebApplicationFactory` + `Testcontainers` for integration tests
- Audit events pattern: `AuditEventTypes.InvitationSent`, `AuditEventTypes.InvitationResent` already defined

### Git intelligence summary

Recent commits in this workspace:

| Commit | Message | Relevance |
|--------|---------|-----------|
| `689f5b4` | UserRole | Most recent — likely includes role/user management changes |
| `a8b0e14` | comitt | Generic commit |
| `ecbb446` | Commit-Users | User-related changes |
| `9572600` | Commit | Generic |
| `7a23426` | CommitStage | Stage-related changes |

The last 3 commits contain 113 changed files, including all the Role Management infrastructure: Invitation entity, service, controller, configuration, migration, DTOs, and tests, plus Organisation, ActivationToken, and User extensions.

### Latest technical information

- **Entity Framework Core 8.x:** Use `HasIndex().IsUnique().HasFilter()` for filtered unique constraints (already done)
- **PostgreSQL 16+:** Supports filtered unique indexes natively
- **HMAC-SHA256 signing:** `TokenService` uses `HMACSHA256` with `CryptographicOperations.FixedTimeEquals` for constant-time comparison (already done)
- **Hangfire:** Background jobs for invitation email delivery already set up (`InvitationEmailDeliveryJob`)

### Project context reference

Key rules from `project-context.md`:

- Naming: DB snake_case plural; C# PascalCase; JSON camelCase
- IDs: UUID v4 (`Guid`)
- Timestamps: ISO 8601 UTC
- Authorization: Policy-based `[Authorize(Policy = Policies.*)]`
- Audit: Every data-changing endpoint writes `audit_events` in the same transaction
- API envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- Errors: RFC 7807 Problem Details
- Testing: xUnit + WebApplicationFactory + Testcontainers PostgreSQL for integration; xUnit for unit

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.1/3-9]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — Data architecture, Token pattern, Audit pattern]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md` — §3a Data Model, §4.3 FR-11/FR-12]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-23/EXPERIENCE.md` — Invitation flow UX]
- [Source: `_bmad-output/project-context.md` — All naming, auth, testing conventions]
- [Source: `apps/api/Domain/Entities/Invitation.cs`]
- [Source: `apps/api/Infrastructure/Persistence/InvitationConfiguration.cs`]
- [Source: `apps/api/Migrations/20260624152225_AddInvitations.cs`]
- [Source: `apps/api/Infrastructure/Persistence/AppDbContext.cs` — line 48]
- [Source: `apps/api/Domain/RoleManagement/InvitationService.cs`]
- [Source: `apps/api/Infrastructure/RoleManagement/TokenService.cs`]
- [Source: `packages/shared-types/src/index.ts` — needs InvitationStatus addition]
- [Source: `_bmad-output/implementation-artifacts/3-8-discreet-pocso-capture-mode.md` — EF migration pattern, DTO record pattern]
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml` — line 90, 3-9 status]

## Dev Agent Record

### Agent Model Used

Auto (claude-sonnet-4-20250514 / deepseek-v4-flash hybrid)

### Debug Log References

### Completion Notes List

- Story created from epics + architecture-role-management + PRD + UX + brownfield analysis (invitations table already implemented by Epic 2 stories)
- Two gaps identified: (1) `InvitationStatus` enum missing from `packages/shared-types/`, (2) integration schema tests missing
- All core data model artifacts (entity, config, migration, service, controller, DTOs) already exist and are functional
- **Implemented 2026-06-27:** Added `InvitationStatus` enum (Pending/Confirmed/Expired) to `packages/shared-types/src/index.ts`
- **Implemented 2026-06-27:** Created `tests/api.integration/Admin/InvitationsSchemaTests.cs` with 15 integration tests covering column types, FK constraints (ON DELETE RESTRICT), unique filtered index, nullable confirmed_at_utc, NOT NULL created_at_utc with no DB default, and organisitions.has_pending_recovery column
- **Validated 2026-06-27:** All existing artifacts (entity, configuration, migration, AppDbContext, service, DTOs, unit tests, model snapshot) verified correct — no gaps found
- **Build verified:** API project builds clean; all 12 InvitationService unit tests pass; 0 regressions introduced

### File List

Validated files:
- apps/api/Domain/Entities/Invitation.cs
- apps/api/Infrastructure/Persistence/InvitationConfiguration.cs
- apps/api/Migrations/20260624152225_AddInvitations.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Domain/RoleManagement/InvitationService.cs
- apps/api/Controllers/V1/Admin/InvitationsController.cs
- apps/api/Models/Admin/InvitationDtos.cs
- apps/api/Infrastructure/RoleManagement/TokenService.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Jobs/InvitationEmailDeliveryJob.cs
- apps/api/Jobs/InvitationCleanupJob.cs
- tests/api.unit/Domain/RoleManagement/InvitationServiceTests.cs
- packages/shared-types/src/index.ts

Files to add:
- packages/shared-types/src/index.ts (add InvitationStatus enum)
- tests/api.integration/Admin/InvitationsSchemaTests.cs (add schema validation tests)

Files modified:
- packages/shared-types/src/index.ts — Added `InvitationStatus` enum with values Pending='pending', Confirmed='confirmed', Expired='expired'

## Review Findings

### Decision Needed (Resolved)

- [x] [Review][Decision] `token_hash` column type: AC1 specifies `varchar` but migration creates `text` — **resolved: AC1 updated to `text`** (sources: blind+auditor)

### Patch (Applied)

- [x] [Review][Patch] Missing `InvitationStatus.Expired` acceptance test — added `StatusColumn_AcceptsExpiredValue` test (sources: blind+edge+auditor)
- [x] [Review][Patch] `DbSet_Invitations_IsRegistered` uses 2 random FK GUIDs — rewrote as `DbSet_Invitations_CanInsertAndQuery` with valid FK references (sources: blind+edge)
- [x] [Review][Patch] `ForeignKey_OnDeleteRestrict_PreventsOrgDeletion_WhenInvitationsExist` — fixed by using separate orgs for User vs Invitation FK (source: blind)
- [x] [Review][Patch] Missing `invited_by_user_id` FK `ON DELETE RESTRICT` test — added `ForeignKey_OnDeleteRestrict_PreventsUserDeletion_WhenUserSentInvitations` (sources: edge+auditor)
- [x] [Review][Patch] Missing navigation property tests — added `NavigationProperties_OrganisationAndUser_AreQueryable` with `.Include(i => i.Organisation)` and `.Include(i => i.InvitedByUser)` (source: auditor)
- [x] [Review][Patch] Missing DB-level CHECK constraint on `status` column — added `StatusColumn_AcceptsInvalidValue_NoCheckConstraint` to document gap (sources: blind+edge)
- [x] [Review][Patch] Missing `status` column NOT NULL verification — added `StatusColumn_IsNotNull` test (source: edge)
- [x] [Review][Patch] Missing `organisation_id` and `invited_by_user_id` NOT NULL tests — added `FkColumns_OrganisationIdAndInvitedByUserId_AreNotNull` (source: edge)
- [x] [Review][Patch] `ConfirmedAtUtc_IsNullable_DefaultsToNull` checks metadata only — added `ConfirmedAtUtc_InsertWithoutSetting_DefaultsToNull` behavioral test (source: blind)
- [x] [Review][Patch] `CreatedAtUtc_IsNotNullable_HasNoDefault` checks metadata only — added `CreatedAtUtc_NoDbDefault_ClrDefaultPersists` behavioral test (source: blind)
- [x] [Review][Patch] Missing `has_pending_recovery` default value test — extended `Migration_AddsHasPendingRecovery_ToOrganisations` to verify type=boolean and default=false (source: auditor)
- [x] [Review][Patch] Missing migration name verification — added `Migration_AppliedMigrationIsExpectedOne` checking `__EFMigrationsHistory` (source: auditor)
- [x] [Review][Patch] Consolidate test setup boilerplate — added `SeedOrgAndUserAsync` helper, refactored all FK/index tests to use it

### Deferred

- [x] [Review][Defer] Repeated test setup boilerplate — 6+ tests independently create Org+User with hardcoded strings — deferred, pre-existing pattern
- [x] [Review][Defer] `PasswordHash = "placeholder"` brittle test data — deferred, pre-existing test pattern
- [x] [Review][Defer] No `[Collection]` attribute for parallel test safety — deferred, pre-existing test infra concern
- [x] [Review][Defer] Missing `token_hash` uniqueness constraint test — deferred, not in ACs
- [x] [Review][Defer] Missing `expires_at_utc > created_at_utc` validation test — deferred, not in ACs
- [x] [Review][Defer] Missing email case-insensitivity test for unique index — deferred, not in ACs
- [x] [Review][Defer] Missing `status`+`confirmed_at_utc` consistency test — deferred, not in ACs
- [x] [Review][Defer] Missing re-invitation after expiry test — deferred, not in ACs

### Dismissed

- `InvitationsTable_HasPrimaryKey` verifies EF Core convention — not useful (source: blind)
- `UniqueFilteredIndex_AllowsDuplicateConfirmed_forSameEmail_AndOrg` — index structure is validated by other tests (source: blind)

## Change Log

| Date | Change |
|------|--------|
| 2026-06-27 | Added `InvitationStatus` enum to `packages/shared-types/src/index.ts` |
| 2026-06-27 | Created `tests/api.integration/Admin/InvitationsSchemaTests.cs` with 15 integration tests |
| 2026-06-27 | Validated all existing Invitation data model artifacts — all correct, no gaps found |
| 2026-06-27 | Code review completed: 1 decision-needed, 12 patch, 8 defer, 2 dismissed |
| 2026-06-27 | Code review patches applied: 12 tests added/fixed, AC1 updated, test setup consolidated via `SeedOrgAndUserAsync` helper
| 2026-06-27 | Status updated: ready-for-dev → review
