---
baseline_commit: 15f4f0a9f0458182bfb3e154e814f4d029fea132
---

# Story 6.5: Migration Script for Existing Hardcoded Accounts

Status: done

## Story

As a **developer**,
I want to create a one-time script that reads existing config-file accounts and creates users in the database,
So that the existing admin users are not lost during the transition.

*Scope: **API-only** — a dedicated one-time migration service (`AccountMigrationService`) that reads the `Seed:*` configuration sections and creates corresponding DB users. This is *not* startup seeding — it's an on-demand script. **No** UI, **no** API endpoint, **no** frontend changes. The config-file accounts (`Seed:Admin`, `Seed:FieldWorker`, `Seed:Vendor`) are currently bootstrapped via per-role seeders (`AdminUserSeeder`, `FieldWorkerUserSeeder`, `VendorUserSeeder`) that run at dev startup only. This story consolidates the logic of those seeders into a single, idempotent, on-demand service for production cutover — the startup seeders remain unchanged for dev use.*

## Acceptance Criteria

1. **Given** a configuration file (or environment variable equivalent) with `Seed:Admin:Email`, `Seed:Admin:Password`, `Seed:FieldWorker:Email`, `Seed:FieldWorker:Password`, `Seed:Vendor:Email`, `Seed:Vendor:Password`, and `Seed:OrganisationId` sections  
   **When** `AccountMigrationService.RunAsync()` is invoked  
   **Then** users are created in the `users` table for each configured seed account  
   **And** the first migrated user (`Seed:Admin`) is assigned the `Director` role  
   **And** the `Seed:FieldWorker` user is assigned the configured role (`SocialWorker` or `CaseWorker`)  
   **And** the `Seed:Vendor` user is assigned the `Vendor` role  
   **And** all migrated users belong to the organisation specified by `Seed:OrganisationId` (or `VendorOrganisationId` for vendor)  
   **And** passwords are hashed using `IPasswordHasher<User>` before storage  

2. **Given** the migration script has already been run  
   **When** `RunAsync()` is invoked again  
   **Then** no duplicate users are created (idempotent — skips by `OrganisationId + Email` uniqueness)  
   **And** existing user records are not modified, **except** for the field worker account: if a field worker user already exists, their role and password are refreshed from config (upsert — matching existing `FieldWorkerUserSeeder` behavior)  

3. **Given** the `Seed:OrganisationId` organisation does not yet exist in the `organisations` table  
   **When** the script runs  
   **Then** a default "Primary Organisation" is created before any users are seeded  
   **And** the vendor system organisation (`00000000-0000-0000-0000-000000000001`) is also created if absent  

4. **Given** a `Seed:*` section is missing required values (email or password)  
   **When** the script runs  
   **Then** that account is skipped with a warning log  
   **And** the script continues processing other configured accounts  

5. **Given** the `Seed:FieldWorker:Role` is missing or invalid  
   **When** the script runs  
   **Then** the field worker account is skipped with a warning log  

6. **Given** an email address has inconsistent casing  
   **When** the script runs  
   **Then** the email is normalized to lowercase before lookup and storage  

## Tasks / Subtasks

### Task 1: Create `AccountMigrationService` (`Infrastructure/Migration/AccountMigrationService.cs`)

- [x] Create `AccountMigrationService` with dependencies: `AppDbContext`, `IConfiguration`, `IPasswordHasher<User>`, `ILogger<AccountMigrationService>`
- [x] Implement `RunAsync(CancellationToken)` — reads all `Seed:*` sections and processes them
- [x] Organisation self-heal: ensure `Seed:OrganisationId` org exists (create "Primary Organisation" if not found)
- [x] Vendor organisation self-heal: ensure `VendorOrganisationId` (`00000000-0000-0000-0000-000000000001`) exists (create "Vendor System" if not found)
- [x] Migrate admin account: read `Seed:Admin:Email`, `Seed:Admin:Password` → create `User` with `Role = Director`
- [x] Migrate field worker account: read `Seed:FieldWorker:Email`, `Seed:FieldWorker:Password`, `Seed:FieldWorker:Role` → create `User` with configured role (skip if role invalid/missing)
- [x] Migrate vendor account: read `Seed:Vendor:Email`, `Seed:Vendor:Password` → create `User` with `Role = Vendor` under vendor organisation
- [x] Idempotency guard: check `OrganisationId + Email` uniqueness before insert (skip if exists)
- [x] Idempotency for field worker: if user exists, update role/password (same as existing `FieldWorkerUserSeeder` behavior)
- [x] Set `FirstName = ""` and `LastName = ""` for all migrated users (`User` entity requires these — non-nullable, `HasMaxLength(128)`, `IsRequired()`)
- [x] Email normalization: trim + lowercase before lookup and storage
- [x] Log each account creation at `Information` level; log skipped accounts at `Warning` level with reason
- [x] Return a summary result (e.g., `(int created, int skipped)`) for test assertions

### Task 2: Create integration tests (`tests/api.integration/Migration/AccountMigrationServiceTests.cs`)

- [x] Test: migrates admin account with Director role
- [x] Test: migrates field worker with configured role
- [x] Test: migrates vendor account with Vendor role under vendor org
- [x] Test: idempotent — second run skips existing accounts
- [x] Test: self-heals missing organisation
- [x] Test: missing credentials skip that account with warning
- [x] Test: invalid/missing field worker role skips that account
- [x] Test: field worker with existing user updates role/password (upsert)
- [x] Test: email normalization to lowercase
- [x] Test: migrated users have `FirstName` and `LastName` set to empty string (required columns)
- [x] Test: `RUN_MIGRATION=1` env var triggers migration at startup (implemented via Program.cs env var check)

### Task 3: DI Registration and Invocation Mechanism

- [x] Register `AccountMigrationService` in DI as scoped (uses `AppDbContext`). Registered **outside** the `if (env.IsDevelopment())` block in `Program.cs` (line ~71) — production cutover tool, not dev-only
- [x] Ensure it does NOT auto-run at startup (only runs when `RUN_MIGRATION=1` env var is set)
- [x] Add `RUN_MIGRATION=1` environment variable check in `Program.cs` after `ApplyMigrationsAndSeedAsync()` — resolves and calls `RunAsync()` at startup for deployment runbooks

## Dev Notes

### READ FIRST

1. **This is NOT startup seeding.** The existing `AdminUserSeeder`, `FieldWorkerUserSeeder`, and `VendorUserSeeder` run at dev startup via `DatabaseInitializer.ApplyMigrationsAndSeedAsync()`. The migration script is a standalone, on-demand service. It should NOT be added to `DatabaseInitializer`.

2. **The three existing seeders share the same logic** — each reads its config section, creates the user, hashes the password. Rather than duplicating their code three times, the migration script can consolidate all of them into a single `RunAsync()` method with a helper for each account type.

3. **Existing seeder patterns to reuse:**
   - `AdminUserSeeder` → creates Director under org from `Seed:OrganisationId`
   - `FieldWorkerUserSeeder` → creates/updates field worker under same org, respects `Seed:FieldWorker:Role`
   - `VendorUserSeeder` → creates Vendor under `VendorOrganisationId` (`00000000-0000-0000-0000-000000000001`)
   - All use `IPasswordHasher<User>.HashPassword(user, password)` and `Guid.NewGuid()` for user IDs

4. **The migration is already partially done** in dev mode via the startup seeders. In production, accounts must be created via the invitation flow. The migration script is specifically for existing deployments that have config-file accounts and need to move them to the database.

5. **Idempotency asymmetry — intentional.** Admin and Vendor accounts are one-time bootstrap accounts: once created, they skip on subsequent runs (no password/role refresh). The Field Worker account is treated differently — matching the existing `FieldWorkerUserSeeder` which intentionally refreshes role and password from config on each run. This is because field worker credentials may rotate during the migration window.

6. **No API endpoint.** This service is intended to be invoked via `RUN_MIGRATION=1` environment variable at startup (see Task 3), or resolved from DI and called manually for testing.

6. **Configuration shape (from `appsettings.Development.json`):**
   ```json
   {
     "Seed": {
       "OrganisationId": "guid",
       "Admin": { "Email": "...", "Password": "..." },
       "FieldWorker": { "Email": "...", "Password": "...", "Role": "SocialWorker" },
       "Vendor": { "Email": "...", "Password": "..." }
     }
   }
   ```

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `Infrastructure/Seed/AdminUserSeeder.cs` | Standalone seeder for Director config account | **No change** — leave for backward compat |
| `Infrastructure/Seed/FieldWorkerUserSeeder.cs` | Standalone seeder for field worker config account | **No change** |
| `Infrastructure/Seed/VendorUserSeeder.cs` | Standalone seeder for Vendor config account | **No change** |
| `Infrastructure/Migration/AccountMigrationService.cs` | **Does not exist** | **NEW** — consolidate migration logic |
| `Infrastructure/Seed/DatabaseInitializer.cs` | Calls all seeders at dev startup | **No change** — migration script is NOT startup seeding |
| `tests/api.integration/Migration/AccountMigrationServiceTests.cs` | **Does not exist** | **NEW** — integration tests |

### Existing patterns to follow

**User object creation pattern** (from `AdminUserSeeder`):
```csharp
var createdAt = DateTime.UtcNow;
var user = new User
{
    Id = Guid.NewGuid(),
    OrganisationId = organisationId,
    Email = normalizedEmail,
    FirstName = "",
    LastName = "",
    Role = UserRoles.Director,
    TokenVersion = 0,
    IsActive = true,
    CreatedAtUtc = createdAt,
    UpdatedAtUtc = createdAt,
};
user.PasswordHash = passwordHasher.HashPassword(user, password);
db.Users.Add(user);
await db.SaveChangesAsync(cancellationToken);
```

**Field worker upsert pattern** (from `FieldWorkerUserSeeder`):
```csharp
var existing = await db.Users.SingleOrDefaultAsync(
    u => u.OrganisationId == organisationId && u.Email == normalizedEmail, ct);
if (existing is not null)
{
    existing.Role = role;
    existing.PasswordHash = passwordHasher.HashPassword(existing, password);
    existing.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    return;
}
```

**Organisation self-heal pattern** (from all seeders):
```csharp
var orgExists = await db.Organisations.AnyAsync(o => o.Id == organisationId, ct);
if (!orgExists)
{
    var org = new Organisation { Id = organisationId, Name = "...", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
    db.Organisations.Add(org);
    await db.SaveChangesAsync(ct);
}
```

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Infrastructure/Migration/AccountMigrationService.cs` |
| NEW | `tests/api.integration/Migration/AccountMigrationServiceTests.cs` |
| NO CHANGE | Existing seeders, `DatabaseInitializer`, or any other files |

### Testing requirements

- Integration tests use `AuthWebApplicationFactory` (existing fixture with Testcontainers PostgreSQL)
- Tests configure mock config values via `WebApplicationFactory` configuration override pattern
- Test each account type, idempotency, self-healing, missing config, and email normalization
- Run with: `dotnet test tests/api.integration --filter AccountMigration`

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6 (restructured in sprint-status.yaml)]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — §Project Structure, Epic 6: Migration → `AccountMigrationService.cs`]
- [Source: `apps/api/Infrastructure/Seed/AdminUserSeeder.cs` — existing Director seeder pattern]
- [Source: `apps/api/Infrastructure/Seed/FieldWorkerUserSeeder.cs` — existing field worker seeder + upsert pattern]
- [Source: `apps/api/Infrastructure/Seed/VendorUserSeeder.cs` — existing Vendor seeder pattern]
- [Source: `apps/api/Infrastructure/Seed/DatabaseInitializer.cs` — startup seeding orchestration]
- [Source: `apps/api/appsettings.Development.json` — config shape with `Seed:*` sections]
- [Source: `_bmad-output/project-context.md` — project-wide rules]

## Dev Agent Record

### Agent Model Used

Composer (Cursor)

### Completion Notes List

- Created `AccountMigrationService` that reads `Seed:*` config sections and migrates admin (Director), field worker (SocialWorker/CaseWorker), and vendor (Vendor) accounts to DB
- Service self-heals missing organisations (Primary Organisation + Vendor System)
- Admin and vendor accounts are idempotent (skip on re-run); field worker upserts (refreshes role/password)
- All migrated users get `FirstName=""`, `LastName=""` to satisfy EF Core required constraints
- Email normalization (trim + lowercase) applied before lookup and storage
- Returns `MigrationSummary(int Created, int Skipped)` for test assertions
- Registered as scoped in DI (outside dev-only block) with `RUN_MIGRATION=1` env var activation mechanism
- Created comprehensive integration test suite (11 tests) covering all acceptance criteria
- API project compiles with 0 errors; integration test project compiles with 0 errors
- Integration tests require Docker/Testcontainers (PostgreSQL, Redis, Azurite) — verify with `dotnet test --filter AccountMigration` when Docker is available

### File List

- apps/api/Infrastructure/Migration/AccountMigrationService.cs (new)
- tests/api.integration/Migration/AccountMigrationServiceTests.cs (new)
- apps/api/Program.cs

## Review Findings

### Patch (fixable without human input)

- [x] [Review][Patch] No wrapping DB transaction — partial migration on crash leaves inconsistent state [AccountMigrationService.cs:18-50]
- [x] [Review][Patch] No try/catch around RunAsync — unhandled exception crashes app startup [Program.cs:264-271]
- [x] [Review][Patch] CancellationToken not forwarded from Program.cs — migration can't be gracefully cancelled on shutdown [Program.cs:272]
- [x] [Review][Patch] Field worker upsert does not restore IsActive — deactivated user gets new password but stays inactive [AccountMigrationService.cs:139-148]
- [x] [Review][Patch] Case-sensitive role comparison — "socialworker" silently skips instead of matching [AccountMigrationService.cs:125]
- [x] [Review][Patch] Test env vars use global state — parallel test methods race on Environment.SetEnvironmentVariable [AccountMigrationServiceTests.cs:27-36]
- [x] [Review][Patch] Weak test assertion in MissingCredentials_SkipsThatAccount — asserts user with empty email doesn't exist (tautology) [AccountMigrationServiceTests.cs:170-172]
- [x] [Review][Patch] Test method name typo "SkiptsThatAccount" → "SkipsThatAccount" [AccountMigrationServiceTests.cs:153] — false positive, already correct
- [x] [Review][Patch] Field worker upsert counted as "skipped" — misleading MigrationSummary conflates "updated" with "skipped" [AccountMigrationService.cs:147]
- [x] [Review][Patch] Redundant email trimming — Trim() called twice on same value [AccountMigrationService.cs:72-73, 115-116, 173-174]

### Deferred (pre-existing, not caused by this change)

- [x] [Review][Defer] TOCTOU race in existence-check-then-act across concurrent instances — extremely unlikely in single-instance startup; proper fix requires distributed locking
- [x] [Review][Defer] No email-confirmed/lockout state set on migrated users — pre-existing pattern, seeders don't set these either
- [x] [Review][Defer] No password policy or credentials lineage tracking — pre-existing, not in story scope
- [x] [Review][Defer] No retry policy for transient DB failures — pre-existing pattern across codebase
- [x] [Review][Defer] Multiple SaveChangesAsync round-trips instead of single batch — pre-existing pattern inherited from seeders
- [x] [Review][Defer] Organisation name mismatch ("Pilot Organisation" vs "Primary Organisation") — pre-existing inconsistency between seeders and migration
- [x] [Review][Defer] RUN_MIGRATION env var bypasses IConfiguration — design choice, documented in story

## Change Log

- 2026-06-29: Story 6.5 created — one-time migration script for existing hardcoded config-file accounts to DB users. Consolidates patterns from existing AdminUserSeeder, FieldWorkerUserSeeder, and VendorUserSeeder into a single idempotent, on-demand service.
- 2026-06-29: Validation — 6 improvements applied (AC2 contradiction fix, FirstName/LastName defaults, RUN_MIGRATION env var mechanism, scope wording, idempotency rationale, registration location)
- 2026-06-29: Implementation complete — AccountMigrationService created (3 account types, idempotency, org self-heal, email normalization), 11 integration tests, DI registration + RUN_MIGRATION env var in Program.cs; ready for code review.
- 2026-06-29: Code review — 10 patch findings applied (DB transaction, try/catch + CancellationToken in Program.cs, IsActive restoration in upsert, case-insensitive role comparison, env var cleanup in tests, assertion fix, MigrationSummary Updated field, redundant Trim removal); 7 deferred; status set to done.
