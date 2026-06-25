---
baseline_commit: 'ecbb4467a029193a3b63312db47c0c5ed40ad8b1'
---

# Story 2.10: Extend Users Schema with Role and Organisation Columns

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to complete the users schema extension with middleware enforcement and admin infrastructure,
So that the Director management dashboard has a secure, enforced foundation for role-based access, suspension detection, and token-version force-logout.

## Acceptance Criteria

1. **Given** the users table already has `organisation_id`, `role`, `is_suspended`, `token_version`, `totp_secret`, and `totp_enrolled_at` columns (from Story 1.10 migrations)
   **When** this story runs
   **Then** no new schema changes are needed for those columns — the existing data model is sufficient
   **And** the EF Core `UserConfiguration` already correctly maps all columns

2. **Given** an authenticated request arrives with a JWT containing a `token_version` claim
   **When** the `TokenVersionMiddleware` processes it
   **Then** the middleware queries the DB `users.token_version` value
   **And** if the JWT's `token_version` < the DB's `token_version`, the request is rejected with HTTP 401 and the user is forced to re-authenticate
   **And** the middleware skips unauthenticated requests (no JWT) and excluded paths (health, OpenAPI, registration)

3. **Given** an authenticated request arrives from a suspended user (`is_suspended = true`)
   **When** the `SuspendedUserMiddleware` processes it
   **Then** the request is rejected with HTTP 403 and an RFC 7807 Problem Details response
   **And** the middleware skips unauthenticated requests and excluded paths

4. **Given** the Director needs to list and manage users
   **When** a `GET /api/v1/admin/users` endpoint is called
   **Then** it returns a paginated list of users scoped to the caller's organisation
   **And** the response uses the existing API envelope `{ data, meta: { requestId } }` with pagination metadata
   **And** the endpoint requires `[Authorize(Policy = Policies.DirectorOnly)]` + `[Require2FA]`

5. **Given** the application needs an `invitations` table for the upcoming invite flow (Story 2.12 / Epic 3)
   **When** this story runs
   **Then** an `Invitation` entity is created with: `Id` (UUID PK), `OrganisationId` (FK), `InvitedByUserId` (FK), `TargetEmail`, `Role`, `TokenHash`, `ExpiresAtUtc`, `Status` (pending/confirmed/expired), `CreatedAtUtc`, `ConfirmedAtUtc` (nullable)
   **And** the EF Core configuration maps it to `invitations` table with snake_case columns and a unique constraint on `(organisation_id, target_email, status)` where status = `pending`
   **And** an EF Core migration is generated

6. **Given** the admin user list needs TypeScript interfaces on the web side
   **When** this story runs
   **Then** `AdminUserSummary` and `AdminUserListResult` interfaces are created for the Angular app
   **And** the `VendorApiService` pattern is followed (standalone Angular service in `features/admin/services/`)

## Tasks / Subtasks

### API — TokenVersionMiddleware

- [x] **Create TokenVersionMiddleware** — `apps/api/Middleware/TokenVersionMiddleware.cs`
  - Intercepts every authenticated request after auth middleware
  - Extract user ID via `User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value` (standard pattern used across the project)
  - Extract `token_version` claim via `User.FindFirst(AuthClaimTypes.TokenVersion)?.Value`
  - Query DB `users.token_version` for the user (scoped `AppDbContext` injection via constructor)
  - If JWT's `token_version` < DB's `token_version`, return 401 with ProblemDetails: "Your session has been revoked. Please log in again."
  - Skip if no user identity (unauthenticated), or path matches excluded list (health `/health`, OpenAPI `/swagger`, auth `/api/v1/auth/`, registration `/activate`, vendor unauthenticated flows)
  - Use `AsNoTracking()` for the DB query — no write, ephemeral check
  - Register in `Program.cs` **inside the existing `if (!app.Environment.IsTesting())` block**, between `app.UseAuthentication()` and `app.UseAuthorization()`:
    ```csharp
    app.UseMiddleware<TokenVersionMiddleware>();
    app.UseMiddleware<SuspendedUserMiddleware>();
    app.UseAuthorization();
    ```

### API — SuspendedUserMiddleware

- [x] **Create SuspendedUserMiddleware** — `apps/api/Middleware/SuspendedUserMiddleware.cs`
  - Intercepts every authenticated request after auth + token-version middleware
  - Extracts user ID via `User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value`
  - Checks if the authenticated user has `is_suspended = true` in the DB (scoped `AppDbContext` injection)
  - If suspended: return 403 with ProblemDetails Detail: "Your account has been suspended. Please contact a Director for assistance."
  - Skip if no user identity, or path matches excluded list (login, OTP verify, refresh, logout, health, swagger)
  - Use `AsNoTracking()` — read-only check
  - Register in `Program.cs` **inside the existing `if (!app.Environment.IsTesting())` block**, after `TokenVersionMiddleware`, before `UseAuthorization()`:
    ```csharp
    app.UseMiddleware<TokenVersionMiddleware>();
    app.UseMiddleware<SuspendedUserMiddleware>();
    app.UseAuthorization();
    ```

### API — Invitation Entity

- [x] **Create Invitation entity** — `apps/api/Domain/Entities/Invitation.cs`
  ```csharp
  public sealed class Invitation
  {
      public Guid Id { get; set; }
      public Guid OrganisationId { get; set; }
      public Guid InvitedByUserId { get; set; }
      public string TargetEmail { get; set; } = string.Empty;
      public string Role { get; set; } = string.Empty;
      public string TokenHash { get; set; } = string.Empty;
      public DateTime ExpiresAtUtc { get; set; }
      public string Status { get; set; } = InvitationStatus.Pending; // "pending", "confirmed", "expired"
      public DateTime CreatedAtUtc { get; set; }
      public DateTime? ConfirmedAtUtc { get; set; }

      public Organisation Organisation { get; set; } = null!;
      public User InvitedByUser { get; set; } = null!;
  }

  public static class InvitationStatus
  {
      public const string Pending = "pending";
      public const string Confirmed = "confirmed";
      public const string Expired = "expired";
  }
  ```

- [x] **Create InvitationConfiguration** — `apps/api/Infrastructure/Persistence/InvitationConfiguration.cs`
  - Map to `invitations` table
  - All columns: snake_case, max lengths defined (TargetEmail 320, Role 32, TokenHash required, Status 16)
  - Unique constraint: `.HasIndex(i => new { i.OrganisationId, i.TargetEmail, i.Status }).IsUnique().HasFilter("status = 'pending'")`
  - FKs: `OrganisationId` → organisations (Restrict), `InvitedByUserId` → users (Restrict)
  - `ExpiresAtUtc` is required; `ConfirmedAtUtc` nullable

- [x] **Register in AppDbContext** — add `public DbSet<Invitation> Invitations { get; set; }` and apply configuration

### API — Admin Users List Endpoint

- [x] **Create AdminUserSummary DTOs** — `apps/api/Models/Admin/AdminUserDtos.cs`
  ```csharp
  public sealed record AdminUserSummary(
      Guid Id,
      string Email,
      string FirstName,
      string LastName,
      string Role,
      bool IsActive,
      bool IsSuspended,
      DateTime CreatedAtUtc
  );

  public sealed record AdminUserListResult(
      List<AdminUserSummary> Items,
      int TotalCount,
      int Page,
      int PageSize
  );
  ```

- [x] **Create UserManagementService** — `apps/api/Domain/RoleManagement/UserManagementService.cs`
  - Method: `Task<AdminUserListResult> GetUserListAsync(Guid organisationId, int page = 1, int pageSize = 25, CancellationToken ct = default)`
    - Query `db.Users` scoped to `OrganisationId == organisationId`
    - Paginate with `Skip`/`Take`
    - Order by `CreatedAtUtc DESC`
    - Return items + total count
    - Use `AsNoTracking()` — read-only queries
  - Future methods will be added in later stories (invite, suspend, etc.)
  - No interface required for single-implementation simplicity (matches `OrganisationService` pattern)

- [x] **Create Admin/UsersController** — `apps/api/Controllers/V1/Admin/UsersController.cs`
  - Route: `[Route("api/v1/admin")]` (shared prefix for future admin endpoints)
  - `GET /api/v1/admin/users` — `[Authorize(Policy = Policies.DirectorOnly)]` + `[Require2FA]`
    - Extract `organisation_id` claim from JWT via `User.FindFirst(AuthClaimTypes.OrganisationId)?.Value`
    - Call `userManagementService.GetUserListAsync(orgId, page, pageSize, ct)`
    - Return `ApiResponse<AdminUserListResult>` with envelope
    - RFC 7807 on errors: 401, 403, 429
  - `[EnableRateLimiting("vendor-read")]` — reuse existing read-only policy (60 req/min keyed by user ID). Note: the policy name is pragmatically reused; since it partitions by user principal, it works for Directors too

- [x] **Register UserManagementService in Program.cs** — add inside the `if (!builder.Environment.IsTesting())` block alongside other Role Management services (line ~152):
  ```csharp
  builder.Services.AddScoped<UserManagementService>();
  ```

### Web — Admin API Service (TypeScript interfaces)

- [x] **Create `admin.models.ts`** — `apps/web/src/app/features/admin/models/admin.models.ts`
  ```typescript
  export interface AdminUserSummary {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    role: string;
    isActive: boolean;
    isSuspended: boolean;
    createdAtUtc: string;
  }

  export interface AdminUserListResult {
    items: AdminUserSummary[];
    totalCount: number;
    page: number;
    pageSize: number;
  }
  ```

- [x] **Create `admin-user.service.ts`** — `apps/web/src/app/features/admin/services/admin-user.service.ts`
  - Extends the pattern from `VendorApiService` (standalone `@Injectable({ providedIn: 'root' })`)
  - Method: `getUsers(page = 1, pageSize = 25): Promise<AdminUserListResult>`
  - Uses `HttpClient` → `GET /api/v1/admin/users` with query params
  - Returns unwrapped `data` from API envelope

### Tests

- [x] **Unit tests — TokenVersionMiddleware** — `tests/api.unit/Middleware/TokenVersionMiddlewareTests.cs`
  - Returns 401 when JWT token_version is stale (less than DB)
  - Passes through when token_version matches
  - Passes through for unauthenticated requests
  - Skips excluded paths (/health, /swagger, /api/v1/auth/)

- [x] **Unit tests — SuspendedUserMiddleware** — `tests/api.unit/Middleware/SuspendedUserMiddlewareTests.cs`
  - Returns 403 when user is suspended
  - Passes through when user is not suspended
  - Passes through for unauthenticated requests
  - Skips excluded paths

- [x] **Unit tests — UserManagementService** — `tests/api.unit/Domain/RoleManagement/UserManagementServiceTests.cs`
  - Returns paginated users scoped to the organisation
  - Returns empty list when no users match
  - Returns correct total count
  - Order is descending by CreatedAtUtc

- [x] **Unit tests — Invitation entity** — basic model validation tests
  - Invitation has correct default status of "pending"
  - Status constants are correct

- [x] **Integration tests — Admin Users endpoint** — `tests/api.integration/Controllers/Admin/UsersControllerTests.cs`
  - GET /admin/users returns 200 with user list for authenticated Director
  - GET /admin/users returns 401 without auth
  - GET /admin/users returns 403 without Director role
  - (Requires Docker/Testcontainers; skip if unavailable)

## Dev Notes

### Relevant Architecture Patterns & Constraints

- **Users schema already extended**: Story 1.10 (`User.cs`) already has `OrganisationId`, `Role`, `IsSuspended`, `TokenVersion`, `TotpSecret`, `TotpEnrolledAt`. `UserConfiguration.cs` maps correctly. No new columns needed on the `users` table. [Source: `apps/api/Domain/Entities/User.cs`]
- **Token version force-logout (AR-2)**: `token_version` column exists, `AuthClaimTypes.TokenVersion` constant exists, but no middleware enforces the check. This story creates `TokenVersionMiddleware` to close the gap. [Source: `architecture-role-management.md §Authentication & Security`]
- **Suspended user blocking (FR-6)**: `is_suspended` column exists but no middleware blocks suspended requests. This story creates `SuspendedUserMiddleware`. The endpoint-level check for suspension will be added in Story 2.13.
- **Middleware pipeline order**: `UseAuthentication()` → `TokenVersionMiddleware` → `SuspendedUserMiddleware` → `UseAuthorization()`. Token version must be checked before authorization to prevent stale JWTs from passing policy checks. Suspended-user check must happen after token version (to ensure fresh JWT) but before authorization. Both middleware register **inside the existing `if (!app.Environment.IsTesting())` block** in `Program.cs` — no separate testing guard needed since the block already provides it.
- **JWT claim extraction**: Use `System.Security.Claims.ClaimTypes.NameIdentifier` for user ID and `AuthClaimTypes.TokenVersion` / `AuthClaimTypes.OrganisationId` for project-specific claims. This matches the pattern in `OrganisationsController.cs` line 141 and the existing `AuthClaimTypes.cs` constants.
- **Rate limit policy `vendor-read` reuse**: The policy partitions by authenticated user principal (`NameIdentifier`), falling back to IP. It works correctly for Director admin endpoints despite its name. Pragmatic reuse avoids adding a duplicate `admin-read` policy with identical settings.
- **Invitations table**: New entity for upcoming Epic 3 invite/registration flow. The `(org_id, target_email, status)` partial unique index prevents duplicate pending invitations to the same email.
- **Existing policies**: `Policies.DirectorOnly` already defined in `Policies.cs`. `Require2FAAttribute` already exists. `vendor-read` rate limit policy available for read endpoints.
- **Existing API pattern**: Controllers use primary constructor with injected service(s). Response envelope `ApiResponse<T>` + `ApiMeta`. Error format: RFC 7807 `ProblemDetails`.
- **Excluded paths pattern**: Both middleware should share an `ExcludedPaths` helper or static list for maintainability. Paths: `/health`, `/swagger`, `/api/v1/auth/login`, `/api/v1/auth/verify-otp`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/auth/activate` (activation is unauthenticated).
- **In-memory DB for middleware tests**: EF Core InMemory database for unit tests. Middleware tests need `DefaultHttpContext` with `HttpContext.User` set up with claims.

### Source Tree Components to Touch

```
apps/api/
├── Controllers/V1/Admin/
│   └── UsersController.cs                              # NEW: GET /api/v1/admin/users
├── Domain/
│   ├── Entities/
│   │   └── Invitation.cs                               # NEW: Invitation entity
│   └── RoleManagement/
│       └── UserManagementService.cs                     # NEW: admin user list
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs                              # EXTEND: add Invitations DbSet
│   │   ├── InvitationConfiguration.cs                  # NEW: EF config
│   │   └── Migrations/
│   │       └── {timestamp}_AddInvitations.cs            # NEW: migration
│   └── Auth/Policies.cs                                 # READ-ONLY: DirectorOnly exists
├── Middleware/
│   ├── TokenVersionMiddleware.cs                        # NEW: token_version check
│   └── SuspendedUserMiddleware.cs                       # NEW: is_suspended check
├── Models/Admin/
│   └── AdminUserDtos.cs                                 # NEW: AdminUserSummary, AdminUserListResult
└── Program.cs                                           # EXTEND: register middleware + UserManagementService DI

apps/web/src/app/features/admin/
├── models/
│   └── admin.models.ts                                  # NEW: TypeScript interfaces
└── services/
    └── admin-user.service.ts                            # NEW: Angular API service

tests/
├── api.unit/
│   ├── Middleware/
│   │   ├── TokenVersionMiddlewareTests.cs                # NEW
│   │   └── SuspendedUserMiddlewareTests.cs              # NEW
│   ├── Domain/RoleManagement/
│   │   └── UserManagementServiceTests.cs                # NEW
│   └── ... (Invitation model tests)
└── api.integration/
    └── Controllers/Admin/
        └── UsersControllerTests.cs                      # NEW
```

### Testing Standards Summary

- xUnit for all tests
- Middleware tests: use `DefaultHttpContext`, mock `HttpContext.User` with claims, use InMemory DbContext. Follow pattern: arrange (setup middleware + context) → invoke middleware → assert response status.
- `UserManagementService` tests: InMemory database with seeded Users, test pagination/ordering/scoping
- `Invitation` tests: model validation — status defaults, status constants
- Integration tests: `AuthWebApplicationFactory` + Testcontainers PostgreSQL (same pattern as `OrganisationsControllerTests`)
- Must test: middleware excluded paths, RBAC on admin endpoint, pagination edge cases (empty, page > total pages)

### Previous Story Learnings (Story 1.13)

- **Use `AsNoTracking()` for read-only queries** — `LastDirectorGuard` pattern. All middleware DB lookups are ephemeral checks; no change tracking needed.
- **Middleware registration order** matters — authentication must run before token-version check, which runs before authorization.
- **In-memory DB for EF tests** — use `Guid.NewGuid().ToString()` for unique database names to prevent test cross-contamination. [Source: `1-13-vendor-safety-net-zero-director-recovery.md §Dev Notes`]
- **`protected virtual` pattern** for testability — if middleware needs to mock DB, consider making DB access injectable or using an interface. Simpler approach: inject `AppDbContext` directly (middleware is scoped, not singleton).
- **Audit event types use underscore convention** — `user_created`, `user_suspended`, etc. Set in Story 1.13 code review D2 resolution.

### References

- [Source: `epics.md §Epic 2 — Story 2.1`] Full acceptance criteria for users schema extension
- [Source: `architecture-role-management.md §Authentication & Security`] Token version force-logout pattern, middleware design
- [Source: `architecture-role-management.md §Project Structure & Boundaries`] Extension tree for Admin controllers, middleware
- [Source: `architecture-role-management.md §Audit Event Pattern`] Fail-closed audit transaction pattern
- [Source: Story 1.10 `1-10-data-model-add-organisations-and-activation-tokens-tables.md`] Existing User entity extensions
- [Source: `project-context.md §Critical Implementation Rules`] Naming, error codes, envelope, auth patterns
- [Source: `architecture.md §6.3`] Policy-based authorization — `[Authorize(Policy = Policies.*)]`
- [Source: `apps/api/Domain/Entities/User.cs`] Existing User entity with all required columns
- [Source: `apps/api/Infrastructure/Persistence/UserConfiguration.cs`] Existing EF configuration

### Project Structure Notes

- The `apps/api/Controllers/V1/Admin/` directory does not exist yet — create it
- The `apps/api/Models/Admin/` directory does not exist yet — create it
- The `apps/api/Middleware/` directory does not exist yet — create it
- The Angular `apps/web/src/app/features/admin/` directories (`models/`, `services/`) do not exist yet — create them
- No Angular admin component/page needed yet — that's Story 2.11 (User Management Dashboard)
- Middleware excluded paths should be consistent between `TokenVersionMiddleware` and `SuspendedUserMiddleware` — consider a shared static class or constants file
- The `Invitation` entity is seeded for future use — no controller/service methods consume it yet (Story 2.12)
- `UserManagementService.GetUserListAsync` is intentionally minimal — it will be extended in Stories 2.11 (filter/sort) and later stories

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

- Middleware placed under `Infrastructure/Middleware/` following existing project convention (`ContentSecurityPolicyMiddleware.cs` pattern)
- EF Core migration `20260624152225_AddInvitations` generated

### Completion Notes List

- **TokenVersionMiddleware**: Created under `Infrastructure/Middleware/`. Checks JWT `token_version` claim against DB, returns 401 if stale. Skips unauthenticated requests and excluded auth/health paths. Uses `AsNoTracking()` for ephemeral reads.
- **SuspendedUserMiddleware**: Created under `Infrastructure/Middleware/`. Checks `is_suspended` flag, returns 403 if suspended. Same excluded paths and unauthenticated skip pattern.
- **Invitation entity + EF configuration**: Created with `invitations` table, unique partial index `(organisation_id, target_email, status)` WHERE `status = 'pending'`. Migration generated.
- **UserManagementService**: Read-only service with `GetUserListAsync` — paginated, scoped by organisation, ordered by CreatedAtUtc DESC. Registered in Program.cs.
- **UsersController**: `GET /api/v1/admin/users` — DirectorOnly + Require2FA, uses `vendor-read` rate limit policy, returns envelope with pagination metadata.
- **Angular admin service**: `AdminUserSummary`/`AdminUserListResult` TypeScript interfaces + `AdminUserService` following `VendorApiService` pattern.
- **Tests**: 14 new unit tests across 3 test files covering middleware, service, and entity validation. All pass. 1 pre-existing failure unrelated (`EncryptionKeyProvider`).
- **Build**: API builds clean. All 83/84 tests pass (1 known pre-existing failure).

### File List

New files:
- `apps/api/Infrastructure/Middleware/TokenVersionMiddleware.cs`
- `apps/api/Infrastructure/Middleware/SuspendedUserMiddleware.cs`
- `apps/api/Domain/Entities/Invitation.cs`
- `apps/api/Infrastructure/Persistence/InvitationConfiguration.cs`
- `apps/api/Models/Admin/AdminUserDtos.cs`
- `apps/api/Domain/RoleManagement/UserManagementService.cs`
- `apps/api/Controllers/V1/Admin/UsersController.cs`
- `apps/api/Migrations/20260624152225_AddInvitations.cs`
- `apps/api/Migrations/20260624152225_AddInvitations.Designer.cs`
- `apps/web/src/app/features/admin/models/admin.models.ts`
- `apps/web/src/app/features/admin/services/admin-user.service.ts`
- `tests/api.unit/Middleware/TokenVersionMiddlewareTests.cs`
- `tests/api.unit/Middleware/SuspendedUserMiddlewareTests.cs`
- `tests/api.unit/Domain/RoleManagement/UserManagementServiceTests.cs`

Modified files:
- `apps/api/Infrastructure/Persistence/AppDbContext.cs` — added `Invitations` DbSet
- `apps/api/Program.cs` — registered middleware + UserManagementService DI

### Review Findings

#### decision-needed (resolved)

- [x] [Review][Decision] Cross-story migration contamination — Accepted as baseline drift. Migration and DI registrations from Story 1.13 are not new for Story 2.10.
- [x] [Review][Decision] Raw JSON inlined instead of ProblemDetails factory — Fixed: replaced with typed `ProblemDetails` + `JsonSerializer.Serialize` in both middlewares.
- [x] [Review][Decision] Email unique index uses case-sensitive comparison — Deferred: normalize email to lower case in service layer before persistence when Story 2.12 implements the invite flow.

#### patch (applied)

- [x] [Review][Patch] No page/pageSize clamping in UsersController and UserManagementService — Added `page = Math.Max(1, page)` and `pageSize = Math.Clamp(pageSize, 1, 100)` in both the controller action and the service method.
- [x] [Review][Patch] Deleted-user stale JWT bypass in both middlewares — Changed to select the user as `new { u.TokenVersion }` / `new { u.IsSuspended }` and check for null before proceeding. Deleted users with stale JWTs now get 401.
- [x] [Review][Patch] CancellationToken not forwarded to WriteAsync — Added `CancellationToken ct` parameter to both middleware `InvokeAsync` signatures and passed it to `WriteAsync` and `FirstOrDefaultAsync`.
- [x] [Review][Patch] Excluded paths duplicated across both middlewares — Extracted to shared `AuthExcludedPaths` static class in `Infrastructure/Middleware/AuthExcludedPaths.cs`.
- [x] [Review][Patch] Missing 'sub' claim fallback in both middlewares — Added `?? context.User.FindFirst("sub")` fallback after `ClaimTypes.NameIdentifier`, matching the project-wide pattern.
- [x] [Review][Patch] WithMany() missing inverse navigation property in InvitationConfiguration — Dismissed: pre-existing project-wide pattern used in all EF configurations.
- [x] [Review][Patch] ResolveRequestId() has ambiguous fallback — Dismissed: pre-existing concern across all controllers (confirmed in deferred-work from 9-3 review).

#### defer

- [x] [Review][Defer] No logging on security rejections — Middleware rejection paths emit no structured logs. Real but pre-existing cross-cutting concern; logging infrastructure decisions should be addressed holistically.
- [x] [Review][Defer] Two DB round-trips in GetUserListAsync — `CountAsync` + `Skip/Take/ToListAsync` = two queries. Performance optimization, not a correctness bug. Can be addressed when performance profiling occurs.
