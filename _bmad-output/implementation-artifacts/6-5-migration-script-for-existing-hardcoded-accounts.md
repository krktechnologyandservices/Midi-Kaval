---
baseline_commit: NO_VCS
---

# Story 6.5: Migration Script for Existing Hardcoded Accounts

Status: ready-for-dev

## Story

As a **developer**,
I want to create a one-time script that reads existing config-file accounts and creates users in the database,
So that the existing admin users are not lost during the transition.

*Scope: **API-only** — a dedicated one-time migration service (`AccountMigrationService`) that reads the `Seed:*` configuration sections and creates corresponding DB users. This is *not* startup seeding — it's an on-demand script. **No** UI, **no** API endpoint, **no** frontend changes. The config-file accounts (`Seed:Admin`, `Seed:FieldWorker`, `Seed:Vendor`) are currently bootstrapped via per-role seeders (`AdminUserSeeder`, `FieldWorkerUserSeeder`, `VendorUserSeeder`) that run at dev startup only. This story replaces those seeder-driven startup flows for the migration context with a single, idempotent, on-demand service.*

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
   **And** existing user records are not modified  

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

- [ ] Create `AccountMigrationService` with dependencies: `AppDbContext`, `IConfiguration`, `IPasswordHasher<User>`, `ILogger<AccountMigrationService>`
- [ ] Implement `RunAsync(CancellationToken)` — reads all `Seed:*` sections and processes them
- [ ] Organisation self-heal: ensure `Seed:OrganisationId` org exists (create "Primary Organisation" if not found)
- [ ] Vendor organisation self-heal: ensure `VendorOrganisationId` (`00000000-0000-0000-0000-000000000001`) exists (create "Vendor System" if not found)
- [ ] Migrate admin account: read `Seed:Admin:Email`, `Seed:Admin:Password` → create `User` with `Role = Director`
- [ ] Migrate field worker account: read `Seed:FieldWorker:Email`, `Seed:FieldWorker:Password`, `Seed:FieldWorker:Role` → create `User` with configured role (skip if role invalid/missing)
- [ ] Migrate vendor account: read `Seed:Vendor:Email`, `Seed:Vendor:Password` → create `User` with `Role = Vendor` under vendor organisation
- [ ] Idempotency guard: check `OrganisationId + Email` uniqueness before insert (skip if exists)
- [ ] Idempotency for field worker: if user exists, update role/password (same as existing `FieldWorkerUserSeeder` behavior)
- [ ] Email normalization: trim + lowercase before lookup and storage
- [ ] Log each account creation at `Information` level; log skipped accounts at `Warning` level with reason
- [ ] Return a summary result (e.g., `(int created, int skipped)`) for test assertions

### Task 2: Create integration tests (`tests/api.integration/Migration/AccountMigrationServiceTests.cs`)

- [ ] Test: migrates admin account with Director role
- [ ] Test: migrates field worker with configured role
- [ ] Test: migrates vendor account with Vendor role under vendor org
- [ ] Test: idempotent — second run skips existing accounts
- [ ] Test: self-heals missing organisation
- [ ] Test: missing credentials skip that account with warning
- [ ] Test: invalid/missing field worker role skips that account
- [ ] Test: field worker with existing user updates role/password (upsert)
- [ ] Test: email normalization to lowercase

### Task 3: DI Registration

- [ ] Register `AccountMigrationService` in DI (scoped or transient — not singleton, since it uses `AppDbContext`)
- [ ] Ensure it does NOT auto-run at startup (unlike the existing seeders which run in dev mode via `DatabaseInitializer`)

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

5. **No API endpoint.** This service is intended to be invoked:
   - Manually via a CLI command or debug console
   - Or as part of a deployment runbook step
   - Not via HTTP

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



### File List

- apps/api/Infrastructure/Migration/AccountMigrationService.cs (new)
- tests/api.integration/Migration/AccountMigrationServiceTests.cs (new)

## Change Log

- 2026-06-29: Story 6.5 created — one-time migration script for existing hardcoded config-file accounts to DB users. Consolidates patterns from existing AdminUserSeeder, FieldWorkerUserSeeder, and VendorUserSeeder into a single idempotent, on-demand service.
