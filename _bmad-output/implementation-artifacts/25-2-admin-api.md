# Story 25.2: Admin API — AdminTwoFactorController, Bypass Codes, TOTP Lockout, 2FA Audit Endpoints

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: ready-for-dev

## Story

As a **Director (and delegated Coordinator)**,
I want **admin API endpoints to reset 2FA, send reminders, generate bypass codes, toggle org-wide 2FA mandate, delegate reset authority to Coordinators, and query the 2FA audit log**,
so that **I can manage 2FA across my organisation without frontend-dependent workflows.**

## Acceptance Criteria

1. **AdminTwoFactorController** created at `Controllers/V1/Admin/AdminTwoFactorController.cs` with base route `api/v1/admin`, `[Authorize(Policy = Policies.DirectorOnly)]`, `[Require2FA]`. No class-level `[EnableRateLimiting]` — each endpoint sets its own policy (matching existing `UsersController` pattern). Injects `AdminTwoFactorService`, `IEmailSender`, `ILogger<AdminTwoFactorController>`. (Do NOT inject `LastDirectorGuard` — that check lives in the service layer.)

2. **AdminTwoFactorService** created at `Domain/RoleManagement/AdminTwoFactorService.cs` — orchestrates admin 2FA operations, injects `AppDbContext`, `TwoFactorService`, `BackupCodeService`, `IAuditService`, `ILogger<AdminTwoFactorService>`.

3. **Endpoints implemented:**

   | Method | Path | Role | Controller Method | Rate Limit |
   |--------|------|------|-------------------|------------|
   | `POST` | `/admin/users/{id:guid}/reset-2fa` | Director | `ResetTwoFactor` | `data-write` |
   | `POST` | `/admin/users/{id:guid}/send-2fa-reminder` | Director | `SendReminder` | `data-write` |
   | `POST` | `/admin/users/{id:guid}/generate-bypass-code` | Director | `GenerateBypassCode` | custom `"admin-bypass-code"` (2/hr per Director) |
   | `GET` | `/admin/audit/2fa` | Director | `GetAuditLog` | `data-read` |
   | `PUT` | `/admin/settings/require-2fa` | Director | `SetRequire2fa` | `data-write` |
   | `PUT` | `/admin/settings/delegate-2fa-reset` | Director | `SetDelegation` | `data-write` |

   *Note: `POST /admin/users/{id}/reset-2fa` already exists on `UsersController` (line 356). This story adds it to `AdminTwoFactorController` as well for cohesive routing. Both call through to `AdminTwoFactorService.ResetTwoFactorAsync` to consolidate logic. The existing `UsersController.ResetTwoFactor` should be refactored to delegate to `AdminTwoFactorService.ResetTwoFactorAsync` — do NOT duplicate the implementation.*

4. **Reset 2FA** (`ResetTwoFactorAsync`):
   - Guards: check `LastDirectorGuard.IsLastActiveDirectorAsync` — prevent resetting last active Director (422: "Cannot reset 2FA for the last active Director.")
   - Calls `TwoFactorService.ResetTwoFactorAsync(actorUserId, targetUserId, organisationId, ipAddress, ct)`
   - Calls `BackupCodeService.RevokeAllAsync(targetUserId, ct)`
   - Returns `ApiResponse<ResetTwoFactorResponse>` with message "Two-factor authentication has been reset."

5. **Send 2FA Reminder** (`SendReminderAsync`):
   - Resolves target user by `id` + `organisationId` — throws `KeyNotFoundException` if not found or not in org
   - Calls `IEmailSender.SendAsync` to send a 2FA enrollment reminder email
   - Email subject: "Action Required: Set Up Two-Factor Authentication"
   - Email body includes org name, setup URL (role-aware: e.g. `/vendor/settings` for Vendor, `/settings/2fa` for other roles), and note about org policy
   - Records audit event `2fa_reminder_sent` (add `TwoFactorReminderSent = "2fa_reminder_sent"` to `AuditEventTypes`)
   - Returns `ApiResponse` with message "Reminder sent."

6. **Bypass Code Generation** (`GenerateBypassCodeAsync`):
   - Rate limited per Director: 2 codes/hr. Store count in Redis key `bypass_count:{directorUserId}:{hourWindow}` with 1-hr TTL. Use `StringIncrementAsync` unconditionally (atomic), then check the returned post-increment value — if > 2, reject with 429 and decrement back. Set TTL on first create via `KeyExpireAsync` or `StringSetAsync` with `When.NotExists`. Do NOT use check-then-increment pattern (TOCTOU race).
   - Generates 12-char alphanumeric code (dashed groups, e.g. `A3K9-X7M2-P1Q8`)
   - Stores SHA-256 hash in Redis at `bypass_code:{userId}:{codeHash}` with 30-min TTL (`TimeSpan.FromMinutes(30)`)
   - Returns plaintext bypass code **exactly once** in response body: `{ bypassCode: "A3K9-X7M2-P1Q8", expiresInSeconds: 1800 }` (wrapped in `ApiResponse` envelope)
   - Records audit event `2fa_bypass_generated`
   - **Bypass code verification during login** is NOT in this story (handled by login flow in Story 25-3). This story only covers generation and storage.

7. **2FA Audit Log** (`GetAuditLogAsync`):
   - Endpoint: `GET /admin/audit/2fa?eventType=&userId=&from=&to=&page=1&pageSize=25`
   - Queries `db.AuditEvents` filtered by `OrganisationId` AND `EventType.StartsWith("2fa_")`
   - Supports optional `actorUserId`, `subjectUserId`, `from`/`to` date range, pagination (max pageSize 100)
   - Response: `ApiResponse<AuditListResultDto>` (reuse existing DTO from `AuditLogController`)
   - Returns paginated results ordered by `CreatedAtUtc DESC`

8. **Require 2FA Toggle** (`SetRequire2faAsync`):
   - Accepts body `{ require2fa: bool }`
   - Updates `Organisation.Require2fa` on the actor's organisation
   - Records audit event: `2fa_mandate_enabled` or `2fa_mandate_disabled` depending on new value
   - Returns updated state: `{ require2fa: true }`

9. **Delegate 2FA Reset** (`SetDelegationAsync`):
   - Accepts body `{ enabled: bool }`
   - Stores delegation flag in Redis at `delegate_2fa_reset:{organisationId}` (bool, no TTL — persistent until toggled)
   - When enabled, Coordinators can reset 2FA for SocialWorker/CaseWorker only (scope check in `AdminTwoFactorService.ResetTwoFactorAsync`)
   - Records audit event: `2fa_delegation_enabled` or `2fa_delegation_disabled`
   - Returns updated state: `{ enabled: true }`

10. **Coordinator-scoped reset** (delegation enforcement):
    - `AdminTwoFactorService.ResetTwoFactorAsync` accepts an `actorRole` string parameter (passed from controller via `User.FindFirstValue(ClaimTypes.Role)`):
      - If actor role is `Coordinator`, verify:
        - Delegation is enabled (Redis key `delegate_2fa_reset:{organisationId}` exists and is `true`)
        - Target user role is `SocialWorker` or `CaseWorker` only
        - If either check fails, throw `InvalidOperationException("Coordinators can only reset 2FA for Social Workers and Case Workers.")`
      - If actor role is `Director`, skip delegation checks (always allowed)
    - Coordinator auth: `[Authorize(Policy = Policies.CoordinatorOrAbove)]` does NOT apply to the full controller since Director has additional actions (bypass, mandate, delegation). Instead, the `ResetTwoFactor` method on `AdminTwoFactorController` uses `[Authorize(Policy = Policies.CoordinatorOrAbove)]` to allow both Director and Coordinator roles on that single endpoint.

11. **TOTP lockout** implemented in `TwoFactorService`:
    - Add `RecordFailedTotpAttemptAsync(Guid userId, string? actorIpAddress, CancellationToken ct)` — records `2fa_failed_totp` audit event, increments failure count in Redis at `totp_lockout:{userId}` with 15-min TTL
    - Add `IsTotpLockedOutAsync(Guid userId)` — checks if Redis key `totp_lockout:{userId}` has count >= 5; returns `true` if locked out
    - Modify `TwoFactorService.VerifyTotpCodeAsync` to:
      - Check `IsTotpLockedOutAsync` first — return `false` if locked out
      - If verification fails, call `RecordFailedTotpAttemptAsync` 
      - If verification succeeds, delete Redis lockout key (reset counter)
    - The lockout ONLY blocks TOTP verification — email OTP flow is unaffected

12. **New audit event types** added to `AuditEventTypes`:
    - `TwoFactorBypassGenerated = "2fa_bypass_generated"`
    - `TwoFactorBypassUsed = "2fa_bypass_used"` (event constant only — verification endpoint in Story 25-3)
    - `TwoFactorFailedTotp = "2fa_failed_totp"` (already added in Story 25-1, verify)    
    - `TwoFactorMandateEnabled = "2fa_mandate_enabled"`
    - `TwoFactorMandateDisabled = "2fa_mandate_disabled"`
    - `TwoFactorDelegationEnabled = "2fa_delegation_enabled"`
    - `TwoFactorDelegationDisabled = "2fa_delegation_disabled"`
    - `TwoFactorReminderSent = "2fa_reminder_sent"`
    Note: `TwoFactorReset` already exists (value `"user.two_factor_reset"`) and is recorded by `TwoFactorService.ResetTwoFactorAsync`. Do NOT add a new `"2fa_reset"` constant — it would be dead code. A future story should migrate the old event to align with the `2fa_` prefix convention.

13. **New DTOs** in `Models/Admin/`:
    - `ResetTwoFactorResponse(Guid userId, string Message)` — already exists, verify
    - `BypassCodeResponse(string BypassCode, int ExpiresInSeconds)`
    - `Require2faRequest(bool Require2fa)`
    - `DelegationRequest(bool Enabled)`

14. **DI registration** — `AdminTwoFactorService` registered as `AddScoped<AdminTwoFactorService>()` in `AuthServiceCollectionExtensions`.

15. Rate limit policy `"admin-bypass-code"` added: 2 requests per hour per Director (partitioned by `ClaimTypes.NameIdentifier`). Implement as `FixedWindowRateLimiterOptions` with `PermitLimit = 2`, `Window = TimeSpan.FromHours(1)`.

## Tasks / Subtasks

- [ ] Create `Domain/RoleManagement/AdminTwoFactorService.cs` (AC: 2)
  - [ ] `ResetTwoFactorAsync` — guard last director, delegate to `TwoFactorService.ResetTwoFactorAsync` + `BackupCodeService.RevokeAllAsync`
  - [ ] `SendReminderAsync` — resolve user, send email via `IEmailSender`, record audit
  - [ ] `GenerateBypassCodeAsync` — atomic `StringIncrementAsync` (no TOCTOU), generate crypto-random code, SHA-256 hash, store in Redis with 30-min TTL, return plaintext
  - [ ] `GetAuditLogAsync` — query `AuditEvents` filtered by org + `2fa_` prefix, support pagination
  - [ ] `SetRequire2faAsync` — update `Organisation.Require2fa`, record audit
  - [ ] `SetDelegationAsync` — store flag in Redis, record audit
  - [ ] Coordinator scope enforcement in `ResetTwoFactorAsync`
- [ ] Create `Controllers/V1/Admin/AdminTwoFactorController.cs` with all 6 endpoints (AC: 1, 3)
  - [ ] No class-level `[EnableRateLimiting]` — each endpoint sets its own (matching `UsersController` pattern)
  - [ ] `POST /admin/users/{id:guid}/reset-2fa` — `[Authorize(Policy = Policies.CoordinatorOrAbove)]`, `[EnableRateLimiting("data-write")]`
  - [ ] `POST /admin/users/{id:guid}/send-2fa-reminder` — Director only (inherited), `[EnableRateLimiting("data-write")]`
  - [ ] `POST /admin/users/{id:guid}/generate-bypass-code` — Director only, `[EnableRateLimiting("admin-bypass-code")]`
  - [ ] `GET /admin/audit/2fa` — Director only, `[EnableRateLimiting("data-read")]`
  - [ ] `PUT /admin/settings/require-2fa` — Director only, `[EnableRateLimiting("data-write")]`
  - [ ] `PUT /admin/settings/delegate-2fa-reset` — Director only, `[EnableRateLimiting("data-write")]`
- [ ] Refactor `UsersController.ResetTwoFactor` to delegate to `AdminTwoFactorService.ResetTwoFactorAsync` (AC: 3)
- [ ] Implement TOTP lockout in `TwoFactorService` (AC: 11)
  - [ ] `RecordFailedTotpAttemptAsync` — increment Redis counter + audit event
  - [ ] `IsTotpLockedOutAsync` — check Redis counter >= 5
  - [ ] Modify `VerifyTotpCodeAsync` — check lockout, record failures, reset on success
- [ ] Create `Models/Admin/BypassCodeResponse.cs` (AC: 13)
- [ ] Create `Models/Admin/Require2faRequest.cs` (AC: 13)
- [ ] Create `Models/Admin/DelegationRequest.cs` (AC: 13)
- [ ] Add audit event type constants to `AuditEventTypes.cs` (AC: 12)
- [ ] Register `AdminTwoFactorService` in DI (AC: 14)
- [ ] Add rate limit policy `"admin-bypass-code"` in `AuthServiceCollectionExtensions` (AC: 15)
- [ ] Run `dotnet build` to verify compilation
- [ ] Run existing tests to verify no regressions

## Dev Notes

### Prerequisites (from Story 25-1 — assumed complete)
- `BackupCode` entity + `backup_codes` table
- `BackupCodeService` with `RevokeAllAsync`, `VerifyAsync`, `GenerateAsync`
- `TwoFactorService.GetEnrollmentStatusAsync`
- `TwoFactorStatusResponse` DTO
- Audit event constants: `TwoFactorBackupUsed`, `TwoFactorFailedTotp`
- `Organisation.Require2fa` property
- `auth-verify-backup-code` rate limit policy

### Architecture Compliance

**This story implements:**
- FR-2.2 (Reset 2FA) — AC 3, 4
- FR-2.3 (Send Reminder) — AC 3, 5
- FR-2.5 (Bypass Codes) — AC 3, 6
- FR-2.6 (2FA Audit Log) — AC 3, 7
- FR-2.4 (Require 2FA Toggle) — AC 3, 8
- FR-2.7 (Delegate to Coordinators) — AC 3, 9, 10
- FR-6 (API Endpoints) — AC 3 (5 new endpoints + re-homed reset-2fa)
- NFR-1.2 (SHA-256 bypass codes in Redis) — AC 6
- NFR-1.6 (Rate limit 2/hr) — AC 15
- NFR-1.7 (TOTP lockout) — AC 11
- NFR-2 (11 audit event types) — AC 12
- NFR-3.3 (Rate limiting) — AC 3 (per-endpoint policies)

**Does NOT implement (deferred):**
- FR-1 (Vendor Settings Page) — Story 25-5
- FR-2.1 (2FA column in Staff Management UI) — Story 25-6
- FR-2.8 (Enrollment notification) — Story 25-8
- FR-3 (Onboarding Emails) — Story 25-8
- FR-4.4-4.6 (Backup code UI) — Stories 25-4, 25-5
- FR-5 (Login Response Contract) — Story 25-3
- Bypass code VERIFICATION during login — deferred to Story 25-3 (login flow)

### Source Tree Components to Touch

**New files:**
```
apps/api/Domain/RoleManagement/AdminTwoFactorService.cs
apps/api/Controllers/V1/Admin/AdminTwoFactorController.cs
apps/api/Models/Admin/BypassCodeResponse.cs
apps/api/Models/Admin/Require2faRequest.cs
apps/api/Models/Admin/DelegationRequest.cs
```

**Modified files:**
```
apps/api/Controllers/V1/Admin/UsersController.cs      # REFACTOR: delegate reset-2fa to AdminTwoFactorService
apps/api/Domain/RoleManagement/TwoFactorService.cs     # MODIFY: + TOTP lockout methods
apps/api/Infrastructure/AuthServiceCollectionExtensions.cs  # MODIFY: + DI + rate limit policy
apps/api/Infrastructure/Audit/AuditEventTypes.cs       # MODIFY: + 7 new event constants
```

### Existing Patterns & Conventions (MUST FOLLOW)

**Controller pattern (from `UsersController.cs`):**
- Namespace: `MidiKaval.Api.Controllers.V1.Admin`
- `[ApiController]`, `[Authorize(Policy = Policies.DirectorOnly)]`, `[Require2FA]`, `[Route("api/v1/admin")]`
- Helper methods: `TryResolveOrganisationId(out Guid?, out IActionResult?)`, `TryResolveActorUserId(out Guid?, out IActionResult?)`, `ResolveRequestId()`
- Error handling: `KeyNotFoundException` → 404, `InvalidOperationException` → 422 with ProblemDetails
- Response: `Ok(new ApiResponse<T>(data, new ApiMeta { RequestId = ResolveRequestId() }))`
- Use `HttpContext.Connection.RemoteIpAddress?.ToString()` for actor IP — **pass through to every service method** that accepts `actorIpAddress` (matching `UsersController.ResetTwoFactor` line 386)

**Service pattern (from `UserManagementService.cs`):**
- Namespace: `MidiKaval.Api.Domain.RoleManagement`
- Constructor injection with `AppDbContext`, `IAuditService`, `ILogger<T>`
- Methods: `virtual` for testability
- Scoped lifetime

**Redis store pattern (from `OtpChallengeStore.cs`):**
- Access `IConnectionMultiplexer` via DI, call `.GetDatabase()` to get `IDatabase`
- Key naming: `bypass_code:{userId}:{hash}`, `bypass_count:{directorUserId}:{yyyyMMddHH}`, `totp_lockout:{userId}`, `delegate_2fa_reset:{organisationId}`
- TTL via `db.StringSetAsync(key, value, expiry)`
- Atomic increment via `db.StringIncrementAsync`
- Key check via `db.KeyExistsAsync`

**AdminTwoFactorController endpoint detail:**

```
POST /admin/users/{id:guid}/reset-2fa
  [Authorize(Policy = Policies.CoordinatorOrAbove)]
  [EnableRateLimiting("data-write")]
  Body: none
  Response 200: ApiResponse<ResetTwoFactorResponse>
  Error 404: User not found
  Error 422: Cannot reset last Director OR Coordinator not delegated/target wrong role

POST /admin/users/{id:guid}/send-2fa-reminder
  [EnableRateLimiting("data-write")]
  Body: none
  Response 200: ApiResponse<{ message: string }>
  Error 404: User not found

POST /admin/users/{id:guid}/generate-bypass-code  
  [EnableRateLimiting("admin-bypass-code")]
  Body: none
  Response 200: ApiResponse<BypassCodeResponse>
  Error 429: Rate limited

GET /admin/audit/2fa?eventType=&userId=&from=&to=&page=1&pageSize=25
  [EnableRateLimiting("data-read")]
  Response 200: ApiResponse<AuditListResultDto> (reuse existing)

PUT /admin/settings/require-2fa
  Body: { require2fa: bool }
  Response 200: ApiResponse<{ require2fa: bool }>

PUT /admin/settings/delegate-2fa-reset
  Body: { enabled: bool }
  Response 200: ApiResponse<{ enabled: bool }>
```

**Bypass code generation details:**
- Use `RandomNumberGenerator.GetBytes(9)` → Base32 or base-62 encode → take 12 alphanumeric chars
- Format as `XXXX-XXXX-XXXX` (dashed groups)
- SHA-256 hash: `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)))`
- Store hash in Redis: `db.StringSetAsync($"bypass_code:{userId}:{hash}", "1", TimeSpan.FromMinutes(30))`
- Rate limit: `var count = db.StringIncrementAsync($"bypass_count:{directorId}:{hourWindow}")` — if result > 2, `db.StringDecrementAsync(key)` and reject (return 429). Set TTL on first create via `db.KeyExpireAsync(key, TimeSpan.FromHours(1), When.NotExists)`. Do NOT use check-then-increment (TOCTOU race).

**TOTP lockout implementation details:**
- Lockout key: `totp_lockout:{userId}`
- On failed TOTP: `db.StringIncrementAsync(key)`, set TTL on first create: `db.StringSetAsync(key, 1, TimeSpan.FromMinutes(15), When.NotExists)`
- On success: `db.KeyDeleteAsync(key)`
- Lockout check: `var count = (int)await db.StringGetAsync(key); return count >= 5;`
- Lockout applies ONLY to `TwoFactorService.VerifyTotpCodeAsync` — NOT to email OTP verification

**Email reminder sending:**
- Inject `IEmailSender` (singleton, already registered)
- Use `await emailSender.SendAsync(to, subject, body)` — follows existing pattern from `AuthService.cs`
- Resolve user role to determine setup URL: Vendor → `/vendor/settings`, other → `/settings/2fa`

**Audit event recording:**
- Constants in `AuditEventTypes.cs` with `2fa_` prefix
- Use `await auditService.RecordAsync(eventType, organisationId, actorUserId, subjectUserId, targetUserSnapshot, actorIpAddress, cancellationToken)` — follows `TwoFactorService` pattern
- For toggle events (mandate, delegation), pass `metadata` dict with the new state

**Legacy event naming note:**
- The existing `TwoFactorService.ResetTwoFactorAsync` fires `"user.two_factor_reset"` (old convention, not `2fa_` prefixed)
- Do NOT add a new `"2fa_reset"` constant for this story — the old constant is used by the existing code path
- A future story should migrate `"user.two_factor_reset"` → `"2fa_reset"` with a DB migration script
- All new events added in this story DO use the `2fa_` prefix (per architecture specification)

### Testing Standards

- Unit tests for `AdminTwoFactorService`:
  - `ResetTwoFactorAsync` — verifies last Director guard, calls `TwoFactorService` + `BackupCodeService.RevokeAllAsync`
  - `SendReminderAsync` — verifies email sent to correct address, audit event recorded
  - `GenerateBypassCodeAsync` — verifies rate limit, correct format, Redis storage
  - `GetAuditLogAsync` — verifies `2fa_` prefix filter, pagination
  - `SetRequire2faAsync` — verifies org column updated, correct audit event
  - `SetDelegationAsync` — verifies Redis flag set
  - Coordinator scope — verifies delegation check, role restriction
- Unit tests for `TwoFactorService` TOTP lockout:
  - Verify lockout blocks after 5 failures
  - Verify success resets counter
  - Verify email OTP unaffected
- Integration test: `POST /admin/users/{id}/reset-2fa` returns 422 for last Director
- All existing tests must pass

## Project Structure Notes

- New `AdminTwoFactorService` sits in `Domain/RoleManagement/` alongside existing `TwoFactorService` — consistent layering
- New `AdminTwoFactorController` in `Controllers/V1/Admin/` alongside `UsersController`, `InvitationsController` — consistent location
- DTOs in `Models/Admin/` — follows existing DTO pattern (`ResetTwoFactorResponse`, `SuspendUserRequest`, etc.)
- Refactoring `UsersController.ResetTwoFactor` to delegate to `AdminTwoFactorService` preserves backward compatibility — callers see no change

## References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 2: "Admin API"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "API & Communication Patterns", "File Changes — API", "Implementation Sequence (Step 2)", "Frontend Architecture", "Naming Patterns"
- **Existing controller pattern:** `apps/api/Controllers/V1/Admin/UsersController.cs` — admin controller conventions, TryResolve helpers, ProblemDetails responses
- **Existing admin controller:** `apps/api/Controllers/V1/Admin/InvitationsController.cs` — simple Director-only controller pattern
- **Existing audit controller:** `apps/api/Controllers/V1/AuditLogController.cs` — audit filtering by eventType prefix, date range, pagination
- **Existing service pattern:** `apps/api/Domain/RoleManagement/TwoFactorService.cs` — audit event recording, service layering
- **Existing store pattern:** `apps/api/Infrastructure/Auth/OtpChallengeStore.cs` — Redis key management with TTL
- **DI registration pattern:** `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
- **Audit event types:** `apps/api/Infrastructure/Audit/AuditEventTypes.cs`
- **LastDirectorGuard:** `apps/api/Domain/RoleManagement/LastDirectorGuard.cs`
- **Auth claim types:** `apps/api/Infrastructure/Auth/AuthClaimTypes.cs`
- **Story 25-1 (prerequisite):** `_bmad-output/implementation-artifacts/25-1-data-model-api-foundation.md`

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

### File List
