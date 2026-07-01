---
baseline_commit: 72eecec645a88ccfb21ec21fcd0329535684d4f1
---

# Story 25.3: Login Response Contract — requires2faSetup, setupUrl, orgRequires2fa, Bypass Code Login Verification, All-Role TOTP Login

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: done

## Story

As a **user of any role (Vendor, Coordinator, SocialWorker, CaseWorker, Accountant) with 2FA enrolled**,
I want **the login endpoint to detect my enrollment status and respond with the appropriate challenge (TOTP instead of email OTP), include setup URL hints when I'm unenrolled, and support backup/bypass code verification during login**,
so that **all roles can use TOTP-based login and the Angular client can intelligently redirect unenrolled users to the 2FA setup page.**

## Acceptance Criteria

1. **`LoginResponse` DTO** gains three optional fields (append-only, backward compatible):
   - `bool Requires2faSetup` (default false) — set to `true` when user is authenticated but NOT enrolled in 2FA
   - `string? SetupUrl` — role-aware setup URL when `requires2faSetup == true`: Vendor → `"/vendor/settings"`, other roles → `"/settings/2fa"`
   - `bool OrgRequires2fa` (default false) — set to `true` when the user's organisation has `require_2fa = true`

2. **`AuthService.LoginAsync`** extended to populate the new fields:
   - After password verification, before email OTP/TOTP challenge creation:
     - Look up `user.TotpEnrolledAt` and `user.Role`
     - If `user.TotpEnrolledAt is null` (not enrolled):
       - Set `requires2faSetup = true`
       - Set `setupUrl` based on role (Vendor → `"/vendor/settings"`, all others → `"/settings/2fa"`)
       - Query `await db.Organisations.Where(o => o.Id == user.OrganisationId).Select(o => o.Require2fa).FirstOrDefaultAsync(cancellationToken)` and set `orgRequires2fa` accordingly (uses direct DbSet query — do NOT use `db.Entry(user).Reference()` which would attach the detached entity as a side effect)
       - Continue with normal email OTP flow (do NOT skip OTP — setup URL is informational)
     - If `user.TotpEnrolledAt is not null` (already enrolled):
       - For ALL roles (not just Director): return TOTP challenge, skip email OTP
       - Set `requiresTotp = true`, `totpChallengeId = ...`, `userId = user.Id`, `tokenVersion = user.TokenVersion`
       - Set `requires2faSetup = false`, `setupUrl = null`

3. **Role-universal TOTP login** — the existing `if (user.Role == UserRoles.Director && user.TotpEnrolledAt is not null)` check (line 97 of `AuthService.cs`) must be expanded to ALL roles. Change to: `if (user.TotpEnrolledAt is not null)` — any enrolled user gets the TOTP challenge, regardless of role.

4. **`VerifyTotpLoginAsync`** updated to handle TOTP lockout:
   - After `user.TotpEnrolledAt is null` check, add `IsTotpLockedOutAsync` check (inject `IConnectionMultiplexer` or `IDatabase` into `AuthService` or call through `TwoFactorService`)
   - If locked out, return `null` (failed verification)
   - Follows the lockout mechanism implemented in Story 25-2

5. **Bypass code verification during login**: The `POST /auth/verify-backup-code` endpoint was created in Story 25-1 but is currently `[Authorize]`d (authenticated-only, resolves userId from JWT). This story must **modify** the existing endpoint to support unauthenticated login verification: remove the `[Authorize]` attribute, accept `userId` in the request body (via a new `VerifyBackupCodeRequest` DTO), and route to `BackupCodeService.VerifyAsync`. The existing `TryGetUserId()` pattern must be replaced with `request.UserId`. Same pattern as `POST /auth/verify-totp-login` (no auth required). Return 200 `{ success: true }` on valid code / 422 ProblemDetails on invalid or used code.

6. **Login endpoint contract documented**: The `LoginResponse` serializes through the existing `ApiEnvelopeFilter` as `{ data: { challengeId, ..., requires2faSetup, setupUrl, orgRequires2fa }, meta: { requestId } }`. The new fields are silently ignored by existing Angular/mobile clients (JSON deserialization ignores unknown fields).

7. **No DI changes needed** — `TwoFactorService` is already injected into `AuthService` (line 32 of `AuthService.cs`) and provides `IsTotpLockedOutAsync`. Do NOT inject `IConnectionMultiplexer` directly. The lockout check is a single call to `twoFactorService.IsTotpLockedOutAsync(user.Id, ct)`.

## Tasks / Subtasks

- [x] Add `bool Requires2faSetup`, `string? SetupUrl`, `bool OrgRequires2fa` to `LoginResponse` DTO (AC: 1)
- [x] Extend `AuthService.LoginAsync` to populate new fields (AC: 2)
  - [x] After password verify, before OTP challenge: check enrollment + org mandate
  - [x] Set `requires2faSetup`, `setupUrl`, `orgRequires2fa` for unenrolled users
  - [x] Return email OTP as normal (setup URL is informational, not blocking)
- [x] Expand TOTP login to ALL roles (AC: 3)
  - [x] Change `if (user.Role == UserRoles.Director && user.TotpEnrolledAt is not null)` to `if (user.TotpEnrolledAt is not null)`
  - [x] Ensure `VerifyTotpLoginAsync` works for all roles (no Director-specific logic)
- [x] Add TOTP lockout check to `VerifyTotpLoginAsync` (AC: 4)
  - [x] Call `twoFactorService.IsTotpLockedOutAsync(user.Id)` — reject if locked out
- [x] **Modify** existing `POST /auth/verify-backup-code` endpoint on `TwoFactorController` for unauthenticated login use (AC: 5)
  - [x] Remove `[Authorize]` from the method, remove `TryGetUserId()` call, accept `VerifyBackupCodeRequest` body instead
  - [x] Calls `BackupCodeService.VerifyAsync(request.UserId, request.Code, ct)` — `BackupCodeService` already injected
  - [x] Return 200 `{ success: true }` on valid, 422 ProblemDetails on invalid/used
  - [x] Keep `[EnableRateLimiting("auth-verify-backup-code")]`
- [x] Create `VerifyBackupCodeRequest` DTO (AC: 5)
  - [x] Properties: `Guid UserId`, `string Code`
  - [x] Namespace `MidiKaval.Api.Models.Auth`, follows existing DTO pattern
- [x] No DI changes needed — `TwoFactorService` is already injected into `AuthService` (AC: 7)
- [x] Run `dotnet build` to verify compilation
- [x] Run existing tests to verify no regressions
- [x] Verify that existing `POST /auth/totp-status` (enrollment check) is already `[Authorize]` and does NOT change

## Dev Notes

### Prerequisites (from previous stories — VERIFIED against actual codebase)
- Story 25-1: `TwoFactorService.GetEnrollmentStatusAsync`, `TwoFactorStatusResponse`, `GET /auth/2fa-status`, `POST /auth/verify-backup-code` endpoint **created** (currently `[Authorize]`d — this story modifies it)
- Story 25-2: TOTP lockout (`IsTotpLockedOutAsync`, `RecordFailedTotpAttemptAsync`) in `TwoFactorService`, `Organisation.Require2fa` column populated
- `TwoFactorService` already injected into `AuthService` constructor (verified at line 32)
- `BackupCodeService` already injected into `TwoFactorController` constructor (verified at line 18)
- `BackupCodeService.VerifyAsync` signature: `VerifyAsync(Guid userId, string code, CancellationToken ct)` — accepts body userId
- Existing `verify-backup-code` endpoint uses `VerifyTotpRequest` DTO (has `UserId` and `Code`). This story introduces a dedicated `VerifyBackupCodeRequest` DTO.

### Architecture Compliance

**This story implements:**
- FR-5.1 (Login response for unenrolled users: `requires2faSetup`, `setupUrl`) — AC 1, 2
- FR-5.2 (Org-level `require_2fa` flag in response: `orgRequires2fa`) — AC 1, 2
- FR-5.3 (Angular client uses `setupUrl` from response — contract only, UI in Story 25-4/25-5) — AC 6
- FR-6 (API Endpoints) — `POST /auth/verify-backup-code` made unauthenticated
- NFR-1.7 (TOTP lockout during login) — AC 4

**Does NOT implement (deferred):**
- FR-1 (Vendor Settings Page) — Story 25-5
- FR-4.4-4.6 (Backup code UI display, login fallback UI, warning banner) — Stories 25-4, 25-5
- Angular `TwoFactorSetupGuard` — Story 25-4 (refactored enrollment component consumes the contract)
- Bypass code verification frontend integration — Story 25-5
- Legacy migration of `"user.two_factor_reset"` → `"2fa_reset"` — deferred

### Source Tree Components to Touch

**Modified files:**
```
apps/api/Models/Auth/AuthDtos.cs                    # MODIFY: +3 fields to LoginResponse
apps/api/Infrastructure/Auth/AuthService.cs          # MODIFY: expand TOTP to all roles, populate new fields, lockout check in VerifyTotpLoginAsync
apps/api/Controllers/V1/Auth/TwoFactorController.cs  # MODIFY: + verify-backup-code endpoint, inject BackupCodeService
```

### Key Code Changes — `AuthService.LoginAsync`

**Current (line 97):**
```csharp
if (user.Role == UserRoles.Director && user.TotpEnrolledAt is not null)
```

**Changed to:**
```csharp
// All roles with 2FA enrolled get TOTP challenge — skip email OTP entirely
if (user.TotpEnrolledAt is not null)
{
    var totpChallenge = new OtpChallenge
    {
        UserId = user.Id,
        OrganisationId = user.OrganisationId,
        Email = user.Email,
        OtpHash = Guid.NewGuid().ToString("N"),
        TokenVersion = user.TokenVersion,
    };
    var totpChallengeId = await otpChallengeStore.CreateAsync(totpChallenge, cancellationToken);

    return new LoginResponse
    {
        ChallengeId = null,
        TotpChallengeId = totpChallengeId,
        ExpiresInSeconds = otpChallengeStore.ExpirySeconds,
        UserId = user.Id,
        TokenVersion = user.TokenVersion,
        RequiresTotp = true,
        Requires2faSetup = false,
        SetupUrl = null,
        OrgRequires2fa = false,
    };
}

// Not enrolled — populate setup hints and continue with email OTP
// Load organisation for require_2fa flag (direct DbSet query — avoids attaching detached entity)
var orgRequires2fa = await db.Organisations
    .Where(o => o.Id == user.OrganisationId)
    .Select(o => o.Require2fa)
    .FirstOrDefaultAsync(cancellationToken);

var setupUrl = user.Role == UserRoles.Vendor ? "/vendor/settings" : "/settings/2fa";

// ... existing email OTP flow (unchanged) ...

return new LoginResponse
{
    ChallengeId = challengeId,
    ExpiresInSeconds = otpChallengeStore.ExpirySeconds,
    Requires2faSetup = true,
    SetupUrl = setupUrl,
    OrgRequires2fa = orgRequires2fa,
    TotpChallengeId = null,  // explicit — not a TOTP challenge
    UserId = Guid.Empty,     // default, explicit for clarity
    TokenVersion = 0,        // default, explicit for clarity
    RequiresTotp = false,    // default, explicit for clarity
};
```

### Key Code Change — `AuthService.VerifyTotpLoginAsync`

**Add after the `user.TotpEnrolledAt is null` check (line 247-249):**

```csharp
// Check TOTP lockout
if (await twoFactorService.IsTotpLockedOutAsync(user.Id, cancellationToken))
{
    return null;
}
```

### Key Code Change — `TwoFactorController.verify-backup-code` (modify existing endpoint)

**Current state (from Story 25-1):** The endpoint exists with `[Authorize]`, resolves `userId` via `TryGetUserId()`, and uses `VerifyTotpRequest`. This story must:

1. Remove `[Authorize]` attribute
2. Change parameter type from `VerifyTotpRequest` to new `VerifyBackupCodeRequest` DTO
3. Replace `TryGetUserId()` with `request.UserId` from body
4. Keep `[EnableRateLimiting("auth-verify-backup-code")]` and rate limit policy

**Modified endpoint:**
```csharp
/// <summary>Verify a backup code during login (unauthenticated).</summary>
[HttpPost("verify-backup-code")]
[EnableRateLimiting("auth-verify-backup-code")]
[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> VerifyBackupCode([FromBody] VerifyBackupCodeRequest request, CancellationToken ct)
{
    var trimmedCode = request?.Code?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(trimmedCode))
    {
        return UnprocessableEntity(new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Invalid Code",
            Detail = "A verification code is required.",
        });
    }

    var verified = await backupCodeService.VerifyAsync(request!.UserId, trimmedCode, ct);
    if (!verified)
    {
        return UnprocessableEntity(new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Invalid Code",
            Detail = "The backup code is invalid or has already been used.",
        });
    }

    return Ok(new { success = true });
}
```

Note: The endpoint receives `userId` in the request body (via `VerifyBackupCodeRequest`) rather than from the JWT, because this is called during the unauthenticated login flow. The existing `TryGetUserId()` call must be removed. `BackupCodeService` is already injected into the `TwoFactorController` constructor (from Story 25-1).

### Existing Patterns & Conventions (MUST FOLLOW)

**DTO conventions:**
- Namespace: `MidiKaval.Api.Models.Auth`
- Properties: `{ get; set; }` with defaults (`false` for bool, `null` for string?)
- XML doc comments on each property (matching existing `LoginResponse` pattern)
- New fields:
  ```csharp
  /// <summary>If true, the user should be prompted to set up 2FA after login.</summary>
  public bool Requires2faSetup { get; set; }

  /// <summary>Role-aware URL where the user can set up 2FA (e.g., "/vendor/settings" for Vendors).</summary>
  public string? SetupUrl { get; set; }

  /// <summary>If true, the organisation requires 2FA enrollment. Enforced by Require2FAAttribute.</summary>
  public bool OrgRequires2fa { get; set; }
  ```

- New DTO:
  ```csharp
  /// <summary>Backup code verification during login (unauthenticated).</summary>
  public sealed class VerifyBackupCodeRequest
  {
      /// <summary>User id from the login response.</summary>
      public Guid UserId { get; set; }

      /// <summary>Backup code to verify.</summary>
      public string Code { get; set; } = string.Empty;
  }
  ```

**AuthService conventions:**
- Delegate TOTP lockout check to `TwoFactorService.IsTotpLockedOutAsync` (already injected)
- Do NOT inject `IConnectionMultiplexer` into `AuthService` — use the existing `TwoFactorService` wrapper
- Load org `Require2fa` via `db.Organisations.Where(o => o.Id == user.OrganisationId).Select(o => o.Require2fa).FirstOrDefaultAsync(cancellationToken)` — a direct DbSet query. Do NOT use `db.Entry(user).Reference().Query()` which would attach the `AsNoTracking` entity to the change tracker as a side effect.
- Keep the org query as a single projection (`Select` only the needed column)

**Role-aware setup URL mapping:**
- `UserRoles.Vendor` → `"/vendor/settings"`
- All other roles (Director, Coordinator, SocialWorker, CaseWorker, Accountant) → `"/settings/2fa"`

### Testing Standards

- Unit test: Login for unenrolled user → `requires2faSetup = true`, `setupUrl` correct per role, `orgRequires2fa` reflects org setting
- Unit test: Login for enrolled user → `requiresTotp = true`, all new fields false/null
- Unit test: Login for enrolled non-Director role → `requiresTotp = true` (verify all-role expansion)
- Unit test: Login for enrolled but TOTP-locked-out user → returns null from `VerifyTotpLoginAsync`
- Unit test: `POST /auth/verify-backup-code` accessible without auth token → 200 (not 401), uses body `{ userId, code }` (modifies existing `[Authorize]`d endpoint)
- Unit test: `POST /auth/verify-backup-code` with invalid user → 422 (not 401, since no auth token expected)
- Integration test: Existing `[Authorize]`d backup code scenarios still work — the endpoint becomes unauthenticated but `BackupCodeService.VerifyAsync` still validates codes correctly
- Integration test: Existing login flows for Director without 2FA still work (email OTP received)
- All existing tests must pass

## Project Structure Notes

- Minimal changes: 3 files modified, 0 new files
- DTO change is additive (backward compatible) — existing clients ignore new JSON properties
- No `[Authorize]` attribute needed on the `verify-backup-code` endpoint — **must be REMOVED** from the existing endpoint (currently has `[Authorize]` from Story 25-1). The `TwoFactorController` has no class-level `[Authorize]`, so removing the attribute keeps it unauthenticated, matching the existing `verify-totp-login` pattern.

## References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 3: "Login response contract"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "API & Communication Patterns" (login response contract table), "Implementation Sequence (Step 3)"
- **Current LoginResponse DTO:** `apps/api/Models/Auth/AuthDtos.cs` (lines 16-35)
- **Current AuthService.LoginAsync:** `apps/api/Infrastructure/Auth/AuthService.cs` (lines 50-167)
- **Current AuthService.VerifyTotpLoginAsync:** `apps/api/Infrastructure/Auth/AuthService.cs` (lines 212-270)
- **Current TwoFactorController:** `apps/api/Controllers/V1/Auth/TwoFactorController.cs`
- **UserRoles constants:** `apps/api/Domain/Entities/UserRoles.cs`
- **TwoFactorService (TOTP lockout):** `apps/api/Domain/RoleManagement/TwoFactorService.cs`
- **Story 25-1 (prerequisite):** `_bmad-output/implementation-artifacts/25-1-data-model-api-foundation.md`
- **Story 25-2 (prerequisite):** `_bmad-output/implementation-artifacts/25-2-admin-api.md`

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List
- Added `Requires2faSetup`, `SetupUrl`, `OrgRequires2fa` fields to `LoginResponse` DTO
- Expanded TOTP login from Director-only to ALL roles (`user.TotpEnrolledAt is not null`)
- `AuthService.LoginAsync` now populates setup hints for unenrolled users (org `Require2fa` query, role-aware setup URL)
- Added TOTP lockout check (`twoFactorService.IsTotpLockedOutAsync`) to `VerifyTotpLoginAsync`
- Modified `TwoFactorController.verify-backup-code`: removed `[Authorize]`, accepts `VerifyBackupCodeRequest` body, uses body userId instead of JWT
- Created `VerifyBackupCodeRequest` DTO in `Models/Auth/AuthDtos.cs`
- Build: 0 errors, 0 new warnings

### File List
- `apps/api/Models/Auth/AuthDtos.cs` — MODIFIED: +3 fields on LoginResponse, +VerifyBackupCodeRequest DTO
- `apps/api/Infrastructure/Auth/AuthService.cs` — MODIFIED: expanded TOTP to all roles, 2fa setup hints, lockout check
- `apps/api/Controllers/V1/Auth/TwoFactorController.cs` — MODIFIED: verify-backup-code endpoint unauthenticated

### Review Findings

#### decision-needed
- [x] [Review][Decision] Backup code verification succeeds but issues no tokens — RESOLVED: endpoint now calls `authService.CompleteBackupCodeLoginAsync` and returns `ApiResponse<VerifyOtpResponse>` with JWT + refresh token on success.

#### patch (resolved)
- [x] [Review][Patch] NullReferenceException risk on null request body — FIXED: added `if (request is null)` guard at `TwoFactorController.cs:150`.
- [x] [Review][Patch] TOTP lockout counter never incremented — FIXED: added `twoFactorService.RecordFailedTotpAttemptAsync(user.Id, ...)` call in `AuthService.VerifyTotpLoginAsync` failure path.
- [x] [Review][Patch] `OrgRequires2fa` hardcoded to `false` in enrolled branch — FIXED: moved org query before TOTP-enrolled branch; both paths use the same `orgRequires2fa` variable.
- [x] [Review][Patch] `VerifyBackupCodeRequest.Code` lacks validation attributes — FIXED: added `[Required, StringLength(64)]` to DTO property.

#### defer
- [x] [Review][Defer] TOTP lockout returns `null` without differentiation — `AuthService.cs:268-270` returns `null` for both lockout and invalid TOTP. Caller cannot distinguish. Deferred: not in story scope, would need client-side UX changes.
- [x] [Review][Defer] Backup code failures return 422 instead of 401 — Story spec explicitly defines 422 in AC 5. Deferred: spec-level design choice.
- [x] [Review][Defer] No audit events for failed backup code attempts — pre-existing gap from Story 25-1, not introduced by this story.
- [x] [Review][Defer] Attacker-supplied `UserId` on unauthenticated endpoint — `TwoFactorController.cs:162` accepts user-supplied userId. Rate limiting (2/hr) mitigates brute-force. Deliberate design per AC 5.
- [x] [Review][Defer] Race condition in `BackupCodeService.VerifyAsync` — pre-existing from Story 25-1, no optimistic concurrency on backup code consumption. Not introduced by this story.
- [x] [Review][Defer] `OrgRequires2fa` returns `false` when Organisation record is missing — pre-existing data integrity concern, not introduced by this story.
