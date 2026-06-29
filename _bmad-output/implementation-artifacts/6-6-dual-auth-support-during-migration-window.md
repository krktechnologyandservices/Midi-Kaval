---
baseline_commit: 15f4f0a9f0458182bfb3e154e814f4d029fea132
---

# Story 6.6: Dual Auth Support During Migration Window

Status: done

## Story

As a **developer**,
I want both the old config-file authentication and the new database-driven authentication to work during the migration window,
So that rollback is possible if issues arise and users are not locked out during cutover.

*Scope: **API-only** — modify `AuthService.LoginAsync` to fall back to seed config sections when a user is not found in the database, controlled by a feature flag. **No** UI, **no** frontend changes, **no** new endpoints. The `AccountMigrationService` from Story 6.5 already handles bulk migration; this story adds the safety net so users whose accounts haven't been migrated yet can still log in.*

## Acceptance Criteria

1. **Given** the `DualAuth:Enabled` flag is set to `true`
   **And** a user's credentials exist in a `Seed:*` config section (`Seed:Admin`, `Seed:FieldWorker`, or `Seed:Vendor`)
   **But** the user does not yet exist in the `users` database table
   **When** the user attempts to log in with their email and password
   **Then** the login succeeds
   **And** the user account is automatically created in the `users` table (auto-migrated)
   **And** the user proceeds through the normal OTP/TOTP verification flow

2. **Given** the `DualAuth:Enabled` flag is set to `false` (or absent)
   **And** a user's credentials exist in a `Seed:*` config section
   **But** the user does not yet exist in the `users` database table
   **When** the user attempts to log in with their email and password
   **Then** the login fails with "Invalid email or password" (same as any other unknown user)

3. **Given** the `DualAuth:Enabled` flag is set to `true`
   **And** a user's credentials match **neither** the database **nor** any `Seed:*` config section
   **When** the user attempts to log in
   **Then** the login fails with "Invalid email or password" (no change from current behavior)

4. **Given** the `DualAuth:Enabled` flag is set to `true`
   **And** a seed config section (`Seed:Admin`, `Seed:FieldWorker`, `Seed:Vendor`) has a valid email
   **But** the corresponding password is missing, empty, or whitespace
   **When** the user attempts to log in with a matching email
   **Then** the login fails (misconfigured seed account cannot be used for auth)

5. **Given** the `DualAuth:Enabled` flag is set to `true`
   **And** no `Seed:*` config sections are configured (missing or empty)
   **When** any user attempts to log in
   **Then** the login falls through to the normal DB-only path (no crash, no warning logged for dual auth)

6. **Given** the `DualAuth:Enabled` flag is set to `true`
   **And** the `Seed:OrganisationId` config value is missing or invalid
   **When** a seed account user attempts to log in
   **Then** the login fails (cannot create user without a valid organisation)

7. **Given** a user was auto-migrated via dual auth
   **When** `DualAuth:Enabled` is later set to `false`
   **And** the same user attempts to log in
   **Then** the login succeeds via the normal DB path (auto-migrated users persist)

## Tasks / Subtasks

### Task 1: Create `DualAuthOptions` config class

- [x] Create `DualAuthOptions` in `Infrastructure/Auth/DualAuthOptions.cs`
- [x] Property: `bool Enabled` (default false)
- [x] Config section: `"DualAuth"`
- [x] Register in DI via `services.Configure<DualAuthOptions>(configuration.GetSection(DualAuthOptions.SectionName))` inside the `!builder.Environment.IsTesting()` block in `AuthServiceCollectionExtensions.cs`
- [x] Add `"DualAuth": { "Enabled": true }` to `appsettings.Development.json` so it's active during local dev

### Task 2: Modify `AuthService.LoginAsync` for dual auth fallback

- [x] Inject `IOptions<DualAuthOptions>` and `IConfiguration` into `AuthService` (if `IConfiguration` is not already injected — it probably is since `ResolveDefaultOrganisationId` uses it)
- [x] After the DB user lookup returns null (user not found), add a dual auth check block
- [x] Guard: only proceed if `_dualAuthOptions.Value.Enabled` is true
- [x] Parse `Seed:Admin`, `Seed:FieldWorker`, `Seed:Vendor` config sections:
  - Normalize email: `email.Trim().ToLowerInvariant()`
  - Validate password is non-empty
  - Validate `Seed:OrganisationId` is a valid GUID (required for admin and field worker accounts — they use the primary org)
  - Vendor account: lookup and creation uses `VendorOrganisationId` (`00000000-0000-0000-0000-000000000001`) — **not** `Seed:OrganisationId`. Use the same constant as `AccountMigrationService.VendorOrganisationId` or `VendorUserSeeder.VendorOrganisationId`.
  - For field worker, validate role is `SocialWorker` or `CaseWorker`
- [x] If email matches a seed section:
  - Verify the input password exactly matches the config password (plaintext comparison — the config stores plaintext, not hashed)
  - If match: auto-migrate the user to DB using `IPasswordHasher<User>.HashPassword()` (same logic as `AccountMigrationService`)
  - Create organisation if it doesn't exist (self-heal). For admin/field worker: use `Seed:OrganisationId` with name "Primary Organisation". For vendor: use `VendorOrganisationId` with name "Vendor System". Both follow the same pattern as `AdminUserSeeder` / `VendorUserSeeder`.
  - Set `FirstName=""`, `LastName=""`, `TokenVersion=0`, `IsActive=true`
  - Save to DB (`await db.SaveChangesAsync(ct)`)
  - Re-read the user from DB (without `AsNoTracking()`) and continue with normal login flow (OTP/TOTP challenge creation)
- [x] If no seed section matches or password doesn't match: return null (same "invalid credentials" response)
- [x] Log successful dual auth auto-migration at `Information` level including the email and role
- [x] Log failed attempts (email found in config but wrong password) at `Warning` level
- [x] Ensure all DB operations use the same `CancellationToken` (`ct`)

### Task 3: Add integration tests

- [x] Create `tests/api.integration/Auth/DualAuthLoginTests.cs` (or extend `AuthLoginTests.cs`)
- [x] Use `AuthWebApplicationFactory` with `IClassFixture<AuthWebApplicationFactory>` and `IAsyncLifetime`
- [x] Set `Seed__*` env vars in `InitializeAsync` and clean up in `DisposeAsync`
- [x] Test: **DualAuth_AdminConfigAccount_LogsInAndCreatesUser**
  - Set `DualAuth__Enabled=true` and `Seed__Admin__Email`/`Seed__Admin__Password`
  - Call `POST /api/v1/auth/login` with matching credentials
  - Assert `200 OK` with `challengeId` (OTP flow started)
  - Assert user now exists in DB with correct role (Director)
  - Assert `FirstName=""` and `LastName=""` as with all migrated users
  - Verify password is properly hashed: resolve user from DB and use `IPasswordHasher<User>.VerifyHashedPassword()` to confirm the input password verifies correctly (proves plaintext was not stored)
- [x] Test: **DualAuth_FlagDisabled_FallsThroughToNormalAuth**
  - Set `DualAuth__Enabled=false`
  - Call login with seed credentials
  - Assert `401 Unauthorized` (invalid credentials)
- [x] Test: **DualAuth_WrongPassword_ReturnsNull**
  - Set `DualAuth__Enabled=true`
  - Call login with correct email but wrong password
  - Assert `401 Unauthorized`
- [x] Test: **DualAuth_NonExistentUser_StillFails**
  - Set `DualAuth__Enabled=true`
  - Call login with completely unknown email
  - Assert `401 Unauthorized`
- [x] Test: **DualAuth_MigratedUser_LogsInNormallyAfterCutover**
  - Set `DualAuth__Enabled=true`, login once (auto-migrates)
  - Set `DualAuth__Enabled=false`
  - Call login again with same credentials
  - Assert `200 OK` (normal DB path works)

## Dev Notes

### READ FIRST

1. **This is NOT a replacement for the `AccountMigrationService`.** Story 6.5 created a bulk migration script that runs on startup via `RUN_MIGRATION=1`. This story adds a safety net so that **even if** the bulk migration hasn't run yet (or missed a user), individual users can still log in — and are auto-migrated on first login.

2. **Auto-migration on first login — intentional design.** The dual auth flow migrates the user to the database on successful password verification, then continues with the normal OTP/TOTP challenge. This is the simplest approach because:
   - The OTP/TOTP verification code path requires the user to exist in the DB (for `otpChallengeStore`, `IssueAuthTokensAsync`, audit logging, etc.)
   - After first login, the user exists in the DB, so disabling dual auth later doesn't affect existing users
   - No special handling needed in the TOTP path, the refresh path, or the logout path
   - The migration logic is identical to what `AccountMigrationService` already does (and was thoroughly tested in Story 6.5 code review)

3. **Plaintext password comparison.** The seed config stores passwords in plaintext (e.g., `"Seed:Admin:Password": "CHANGE_ME"`). The dual auth fallback compares the login password directly against the config value. When auto-migrating, the password is re-hashed using `IPasswordHasher<User>.HashPassword()` so the DB user has a proper hash. This means:
   - After the first dual-auth login, the config password is never used again
   - The DB user's password is properly hashed
   - Disabling dual auth after migration doesn't break subsequent logins

4. **Organisation self-heal.** If the `Seed:OrganisationId` organisation doesn't exist in the DB when dual auth auto-migrates a user, it must be created. Follow the same pattern as `AdminUserSeeder`:
   ```csharp
   var org = new Organisation { Id = organisationId, Name = "Primary Organisation", IsActive = true, CreatedAtUtc = now };
   db.Organisations.Add(org);
   await db.SaveChangesAsync(ct);
   ```

5. **Idempotency.** The `(OrganisationId, Email)` unique index on the `users` table prevents duplicate auto-migration. If two concurrent login requests race, the second will hit a `DbUpdateException` on the unique index. Catch only PostgreSQL error code `23505` (unique violation) — do not swallow other DB errors like FK violations or deadlocks. Use the existing `IsUniqueConstraintViolation()` pattern (or check `SqlException.Number` / `NpgsqlException.SqlState`). If a unique violation is caught, fall through and continue the normal login path (the user now exists).

6. **No change to the refresh or TOTP paths.** The user is migrated **before** the OTP/TOTP challenge. By the time `IssueAuthTokensAsync` or `VerifyTotpLoginAsync` runs, the user exists normally in the DB. No changes needed outside `LoginAsync`.

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `Infrastructure/Auth/AuthService.cs` | `LoginAsync` returns null when user not found in DB | Add dual auth fallback block after DB miss |
| `Infrastructure/Auth/AuthServiceCollectionExtensions.cs` | Registers `AuthService`, JWT, Redis, etc. | Add `services.Configure<DualAuthOptions>()` |
| `Infrastructure/Auth/DualAuthOptions.cs` | **Does not exist** | **NEW** — config class with `Enabled` flag |
| `tests/api.integration/Auth/DualAuthLoginTests.cs` | **Does not exist** | **NEW** — dual auth integration tests |
| `appsettings.Development.json` | Contains `Seed:*` sections, no DualAuth section | Add `"DualAuth": { "Enabled": true }` |

### Existing patterns to follow

**Password hashing for auto-migration:**
```csharp
var passwordHash = passwordHasher.HashPassword(user, password);
```

**Organisation self-heal (from AdminUserSeeder):**
```csharp
var orgExists = await db.Organisations.AnyAsync(o => o.Id == organisationId, cancellationToken);
if (!orgExists)
{
    var now = DateTime.UtcNow;
    var org = new Organisation { Id = organisationId, Name = "Primary Organisation", IsActive = true, CreatedAtUtc = now };
    db.Organisations.Add(org);
    await db.SaveChangesAsync(cancellationToken);
}
```

**Seed config email normalization:**
```csharp
var normalizedEmail = email.Trim().ToLowerInvariant();
```

### File structure

| Action | Path |
|--------|------|
| NEW | `apps/api/Infrastructure/Auth/DualAuthOptions.cs` |
| MODIFY | `apps/api/Infrastructure/Auth/AuthService.cs` — add dual auth fallback in `LoginAsync` |
| MODIFY | `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs` — register `DualAuthOptions` |
| MODIFY | `apps/api/appsettings.Development.json` — add `DualAuth:Enabled` |
| NEW | `tests/api.integration/Auth/DualAuthLoginTests.cs` |

### Testing requirements

- Integration tests use `AuthWebApplicationFactory` (existing fixture with Testcontainers PostgreSQL)
- Configure `Seed:*` and `DualAuth:*` via environment variables (`Seed__Admin__Email`, `DualAuth__Enabled`, etc.) in `InitializeAsync`
- Clean up all env vars in `DisposeAsync` to prevent cross-test contamination (follow pattern from Story 6.5 code review)
- Run with: `dotnet test tests/api.integration --filter DualAuth`

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 6, Story 6.2]
- [Source: `_bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md` — §Existing-User Migration, NFR-16]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — §Implementation sequence, Epic 6: Migration]
- [Source: `_bmad-output/implementation-artifacts/6-5-migration-script-for-existing-hardcoded-accounts.md` — previous story, migration service patterns]
- [Source: `apps/api/Infrastructure/Auth/AuthService.cs` — `LoginAsync` method to modify]
- [Source: `apps/api/Infrastructure/Seed/AdminUserSeeder.cs` — organisation self-heal pattern]
- [Source: `apps/api/appsettings.Development.json` — `Seed:*` config sections]

## Dev Agent Record

### Completion Notes List

- Story 6.6 created — dual auth support during migration window with `DualAuth:Enabled` feature flag
- Approach: modify `AuthService.LoginAsync` to fall back to seed config sections when user not found in DB
- Auto-migration on first dual-auth login: config user is migrated to DB before OTP/TOTP verification
- 5 integration tests covering: successful dual auth auto-migration, wrong password, unknown user, flag disabled fallthrough, and post-cutover login

### Implementation Notes

- Created `DualAuthOptions` config class with `Enabled` flag, registered in DI
- Added `AutoMigrateFromConfigAsync` private method in `AuthService` that checks `Seed:Admin`, `Seed:FieldWorker`, `Seed:Vendor` config sections
- Organisation self-heal with correct org IDs (primary org for admin/fieldworker, `VendorOrganisationId` for vendor)
- DbUpdateException handling for concurrent unique constraint races (idempotent auto-migration)
- Integration tests use three factory subclasses: `DualAuthEnabledWebApplicationFactory`, `DualAuthDisabledWebApplicationFactory`, and base `AuthWebApplicationFactory`

### File List

- apps/api/Infrastructure/Auth/DualAuthOptions.cs (new)
- apps/api/Infrastructure/Auth/AuthService.cs (modified)
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs (modified)
- apps/api/appsettings.Development.json (modified)
- tests/api.integration/Auth/DualAuthLoginTests.cs (new)

## Senior Developer Review (AI)

**Review outcome:** Changes Requested
**Review date:** 2026-06-29

### Action Items

#### Patch (fixable without human input)

- [x] [Review][Patch] Wrap org self-heal + user insert in DB transaction — concurrent requests can cause PK violation on org insert or leave orphan org after failed user insert [`AuthService.cs:760-815`]
- [x] [Review][Patch] Seed:OrganisationId shouldn't block Vendor dual auth — Vendor uses hardcoded VendorOrganisationId, doesn't need Seed:OrganisationId [`AuthService.cs:656-684`]
- [x] [Review][Patch] IsUniqueConstraintViolation uses fragile string matching instead of PostgresException.SqlState == "23505" [`AuthService.cs:818-823`]
- [x] [Review][Patch] Race handler re-read may return null under READ COMMITTED — winning transaction may not have committed yet; add retry with delay [`AuthService.cs:799-810`]
- [x] [Review][Patch] "auto-migrated" log fires even when re-read returns null — log says success but login fails [`AuthService.cs:813-815`]
- [x] [Review][Patch] Missing tests for AC4 (empty password) and AC6 (invalid org ID) — add test coverage [`DualAuthLoginTests.cs`]
- [x] [Review][Patch] AC5 violation: warning logged about missing Seed:OrganisationId even when no seed sections match — moved warning into Admin/FW methods only [`AuthService.cs:656-684`]
- [x] [Review][Patch] DualAuthOptions.Enabled not validated on startup — add ValidateOnStart [`AuthServiceCollectionExtensions.cs:44`]
- [x] [Review][Patch] Test cleanup uses email-only filter instead of (OrganisationId, Email) — fix to match unique constraint [`DualAuthLoginTests.cs:52-54`]

#### Defer (pre-existing or design-level concern)

- [x] [Review][Defer] Config credentials act as permanent backdoor after migration — operational concern; disable DualAuth:Enabled post-migration
- [x] [Review][Defer] Exception classes appended to AuthService.cs — pre-existing pattern
- [x] [Review][Defer] Inconsistent exception class constructors — pre-existing
- [x] [Review][Defer] FieldWorker role validation not centralized in UserRoles — maintainability enhancement
- [x] [Review][Defer] Forgot-password unavailable for migrated users with non-deliverable seed emails — pre-existing concern
- [x] [Review][Defer] Seed:FieldWorker:Role undocumented in spec — documentation gap

## Change Log

- 2026-06-29: Implemented DualAuth options, AuthService fallback, integration tests; status set to review.
- 2026-06-29: Code review — 9 patch findings applied (DB transaction, Seed:OrganisationId moved into Admin/FW only, PostgresException 23505, race handler retry, log fix, AC4/AC6 tests, ValidateOnStart, test cleanup fix); 6 deferred; status remains review.
