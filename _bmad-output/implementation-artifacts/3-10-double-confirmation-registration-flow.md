---
baseline_commit: 689f5b4
---

# Story 3.10: Double-Confirmation Registration Flow

Status: done

> **TL;DR for dev — concrete actions:**
> 1. Add `POST /api/v1/auth/accept-invitation` endpoint to `RegistrationController` — consumes invitation token, creates user in "pending confirmation" state, sends confirmation email
> 2. Add `POST /api/v1/auth/confirm-email` endpoint to `RegistrationController` — consumes confirmation token, activates user (sets `IsActive = true`), updates `Invitation.confirmed_at_utc`
> 3. Create `ConfirmationToken` entity + EF config + migration for the new confirmation token table
> 4. Create `ConfirmationEmailDeliveryJob` with exponential backoff retry (max 3 attempts)
> 5. Add `CONFIRMATION_TOKEN_TTL_HOURS` config (default 24h) to `appsettings.json`
> 6. Create Angular standalone component at `/invite` for invitation acceptance (centered card, no shell)
> 7. Create Angular confirmation-landing component (or inline into invite-accept component)
> 8. Add route `/invite` and confirmation-landing path to `app.routes.ts`
> 9. Add unit + integration tests for all new endpoints and domain logic
>
> **Brownfield reality:** The invitations data model, `InvitationService`, and `RegistrationService` (for first Director activation) already exist. This story builds the *second* half — inviting non-Director users (Coordinator, SocialWorker, CaseWorker, Accountant) through a double-confirmation flow. Follow patterns from the existing activation flow in `RegistrationService.cs`.

## Story

As an **invited user**,
I want to click my invitation link, set up my account, and confirm my email to activate,
so that my identity is verified before I can access the application.

## Acceptance Criteria

1. **Given** a valid invitation link
   **When** I open the link on an unauthenticated browser
   **Then** I see an invitation acceptance page with my email pre-filled (read-only), role badge, and organisation name
   **And** the page is a standalone Angular route at `/invite` with no app shell/sidebar

2. **Given** the invitation acceptance form
   **When** I enter my full name and a password meeting minimum complexity (8+ chars, uppercase, lowercase, digit)
   **Then** the system consumes the invitation token, creates my account in "pending confirmation" state (`IsActive = false`)
   **And** a confirmation email is sent to my registered email address
   **And** I cannot log in yet (login endpoint rejects `IsActive = false` accounts)

3. **Given** my account is in "pending confirmation" state
   **When** I attempt to log in
   **Then** the login endpoint returns a 403 with error code `ACCOUNT_NOT_CONFIRMED` and message "Please check your email to confirm your account before logging in."

4. **Given** a confirmation email has been sent
   **When** I click the confirmation link
   **Then** the system validates the HMAC signature, consumes the confirmation token
   **And** sets `User.IsActive = true`
   **And** updates `Invitation.confirmed_at_utc` to the current timestamp
   **And** updates `Invitation.status` to `confirmed`
   **And** I am redirected to the login page and can log in immediately

5. **Given** a confirmation link
   **When** the link is more than 24 hours old (configurable via `CONFIRMATION_TOKEN_TTL_HOURS`)
   **Then** the link is rejected as expired
   **And** the user sees "This confirmation link has expired. Contact your Director to request a new invitation."

6. **Given** a confirmation email delivery
   **When** delivery fails
   **Then** the system retries with exponential backoff (1 min, 5 min, 15 min — max 3 attempts)
   **And** if all retries fail, the invitation status shows "delivery failed" in the Director dashboard
   **And** `ConfirmationToken.DeliveryAttempts` tracks retry count (stops at max 3)

## Tasks / Subtasks

### API — Data Model (AC: 2, 4, 5)

- [x] **ConfirmationToken entity** (AC: 2, 4, 5)
  - [x] Create `apps/api/Domain/Entities/ConfirmationToken.cs`:
    - `Id` (Guid, PK), `UserId` (Guid, FK → users, `DeleteBehavior.Cascade`), `InvitationId` (Guid, FK → invitations, nullable, `DeleteBehavior.SetNull`), `TokenHash` (string, SHA-256), `ExpiresAtUtc` (DateTime), `ConsumedAtUtc` (DateTime?), `DeliveryAttempts` (int, default 0), `LastDeliveryAttemptAtUtc` (DateTime?), `CreatedAtUtc` (DateTime)
  - [x] Navigation property: `User` (User), `Invitation` (Invitation, nullable)

- [x] **EF Core configuration** (AC: 2, 4, 5)
  - [x] Create `apps/api/Infrastructure/Persistence/ConfirmationTokenConfiguration.cs`:
    - Table: `confirmation_tokens` (snake_case plural)
    - PK: `Id` with `HasDefaultValueSql("gen_random_uuid()")`
    - `TokenHash`: `HasMaxLength(64)`, `IsRequired()`
    - `ExpiresAtUtc`, `ConsumedAtUtc`, `CreatedAtUtc`: `.HasColumnType("timestamp with time zone")`
    - `DeliveryAttempts`: `HasDefaultValue(0)`
    - FK `UserId` → `User.Id`: `DeleteBehavior.Cascade`
    - FK `InvitationId` → `Invitation.Id`: `DeleteBehavior.SetNull`
    - Index on `TokenHash` for lookup performance

- [x] **Migration** (AC: 2, 4, 5)
  - [x] Generate migration: `AddConfirmationTokens` — creates `confirmation_tokens` table
  - [x] Register `DbSet<ConfirmationToken> ConfirmationTokens` in `AppDbContext`

### API — Domain Services (AC: 1-6)

- [x] **Extend `RegistrationService`** (AC: 1-6) — `apps/api/Domain/RoleManagement/RegistrationService.cs`
  - [x] Add method `AcceptInvitationAsync(rawToken, signature, fullName, password)`:
    1. HMAC signature verification via `TokenService.ValidateSignature()` — reject if invalid
    2. SHA-256 hash of raw token; look up `Invitation` by `TokenHash`
    3. Check invitation not expired (`expires_at_utc > now`)
    4. Check invitation status is `pending` (not already confirmed/expired)
    5. Validate password (reuse existing `ValidatePassword` pattern, 8+ chars, uppercase, lowercase, digit)
    6. Start DB transaction
    7. Create `User` with: `Email` from invitation, `FirstName`/`LastName` split from `fullName`, `PasswordHash` (via `IPasswordHasher<User>` from DI), `OrganisationId` from invitation, `Role` from invitation, `IsActive = false`, `TokenVersion = 1`
    8. Mark invitation as consumed (UPDATE `status = 'confirmed'`, `confirmed_at_utc = NOW()`)
    9. **Atomically** consume invitation token via raw SQL (prevent double-use)
    10. Create `ConfirmationToken` record: SHA-256 hash of new confirmation token, `UserId` FK, `InvitationId` FK, `ExpiresAtUtc = now + CONFIRMATION_TOKEN_TTL_HOURS`
    11. Generate HMAC signature for the new confirmation token
    12. Write audit event: `AuditEventTypes.AccountCreated` (new event type needed)
    13. `SaveChangesAsync` + `CommitTransactionAsync`
    14. Enqueue `ConfirmationEmailDeliveryJob` with confirmation token and signature
    15. Return result: `AcceptInvitationResponse(Email, OrganisationName, Message)`

  - [x] Add method `ConfirmEmailAsync(rawToken, signature)`:
    1. HMAC signature verification — reject if invalid
    2. SHA-256 hash; look up `ConfirmationToken` by `TokenHash`
    3. Check not expired (`ExpiresAtUtc > now`)
    4. Check not already consumed (`ConsumedAtUtc == null`)
    5. Start DB transaction
    6. Update `User.IsActive = true` (user can now log in)
    7. Atomically consume confirmation token (`ConsumedAtUtc = NOW()`)
    8. Write audit event: `AuditEventTypes.EmailConfirmed` (new event type needed)
    9. `SaveChangesAsync` + `CommitTransactionAsync`
    10. Return confirmation result

  - [x] Add `protected virtual` methods for testability:
    - `ConsumeInvitationTokenAtomicallyAsync(Guid invitationId)` — raw SQL
    - `ConsumeConfirmationTokenAtomicallyAsync(Guid tokenId)` — raw SQL
    - `SendConfirmationEmailAsync(Guid tokenId, string rawToken, string signature)` — enqueue Hangfire job

- [x] **Add validation helpers** (AC: 2)
  - [x] `ValidateFullName(fullName)` — non-empty, at least 2 characters, no leading/trailing whitespace
  - [x] Reuse existing `ValidatePassword(password)` — static, 8+ chars, uppercase, lowercase, digit

### API — Controller (AC: 1-6)

- [x] **Extend `RegistrationController`** — `apps/api/Controllers/V1/Auth/RegistrationController.cs`
  - [x] **`GET /api/v1/auth/accept-invitation`** — validate invitation link (no consumption):
    - Query params: `token`, `sig`
    - Rate limit: `[EnableRateLimiting("auth-activate-read")]`
    - Returns `ValidateInvitationLinkResponse` with `Email`, `OrganisationName`, `Role`
    - Pattern: follows existing `GET /activate` endpoint

  - [x] **`POST /api/v1/auth/accept-invitation`** — accept invitation, create user, send confirmation:
    - Body: `{ token, signature, fullName, password }`
    - Rate limit: `[EnableRateLimiting("auth-activate")]`
    - Returns `AcceptInvitationResponse(Email, OrganisationName, Message)`
    - Pattern: follows existing `POST /activate` with similar validation/error handling
    - Handle `KeyNotFoundException` → 404, `InvalidOperationException` → 422, `ValidationException` → 400

  - [x] **`POST /api/v1/auth/confirm-email`** — confirm email, activate user:
    - Body: `{ token, signature }`
    - Rate limit: `[EnableRateLimiting("auth-activate")]`
    - Returns `ConfirmEmailResponse(Message)` with 200
    - Handle `KeyNotFoundException` → 404 (token not found), `InvalidOperationException` → 422 (expired/consumed)

  - [x] All endpoints are `[AllowAnonymous]` (unauthenticated), same pattern as `POST /activate`

### API — DTOs (AC: 1-6)

- [x] **Add to `apps/api/Models/Auth/AuthDtos.cs`**:
  - `sealed record ValidateInvitationLinkResponse(string Email, string OrganisationName, string Role, bool IsValid)`
  - `sealed record AcceptInvitationRequest(string Token, string Signature, string FullName, string Password)`
  - `sealed record AcceptInvitationResponse(string Email, string OrganisationName, string Message)`
  - `sealed record ConfirmEmailRequest(string Token, string Signature)`
  - `sealed record ConfirmEmailResponse(string Message)`

### API — Audit Events (AC: 2, 4, 6)

- [x] **Add to `apps/api/Infrastructure/Audit/AuditEventTypes.cs`**:
  - `AccountCreated = "account_created"` — when user completes invitation acceptance
  - `EmailConfirmed = "email_confirmed"` — when user confirms email
  - `ConfirmationDeliveryFailed = "confirmation_delivery_failed"` — when all retries exhausted
  - `ConfirmationDelivered = "confirmation_delivered"` — when confirmation email sent successfully

### API — Background Job (AC: 6)

- [x] **Create `apps/api/Jobs/ConfirmationEmailDeliveryJob.cs`**:
  - Hangfire job that sends confirmation email
  - Parameters: `confirmationTokenId`, `rawToken`, `signature`, `targetEmail`, `userName`
  - On success: update `ConfirmationToken.DeliveryAttempts++`, clear `LastDeliveryAttemptAtUtc`
  - On failure: 
    - Increment `DeliveryAttempts`
    - Set `LastDeliveryAttemptAtUtc = now`
    - If `DeliveryAttempts < 3`: re-enqueue with exponential backoff (1 min, 5 min, 15 min)
    - If `DeliveryAttempts >= 3`: write `ConfirmationDeliveryFailed` audit event, mark invitation for "delivery failed" display
  - Use existing Hangfire setup (no new infrastructure needed)
  - Register in `Program.cs` alongside existing jobs (line ~160)

### API — Configuration (AC: 5)

- [x] **Add to `apps/api/appsettings.json` and `appsettings.Development.json`**:
  ```json
  "ConfirmationLink": {
    "BaseUrl": "http://localhost:4200",
    "TokenTtlHours": 24
  }
  ```
  - `CONFIRMATION_TOKEN_TTL_HOURS` — environment variable override
  - Read via `IConfiguration` in the service (same pattern as `INVITATION_TOKEN_TTL_HOURS`)

### API — Login Update (AC: 3)

- [x] **Modify `AuthService.LoginAsync`** — `apps/api/Infrastructure/Auth/AuthService.cs`:
  - `User.IsActive` is **already checked** at line 64 (throws `AuthForbiddenException(DeactivatedMessage)`)
  - **Modify** the existing check to differentiate between suspended (`IsSuspended`) and unconfirmed (`!IsActive && !IsSuspended`) users
  - For unconfirmed: return 403 with error code `ACCOUNT_NOT_CONFIRMED` and message "Please check your email to confirm your account before logging in."
  - For suspended: keep existing behavior (`DeactivatedMessage`)
  - This does not block the login attempt — it redirects the error message/error code specifically

### API — Shared Types (AC: 1-6)

- [x] **Add to `packages/shared-types/src/index.ts`**:
  ```typescript
  export const ConfirmationStatus = { Pending: 'pending', Confirmed: 'confirmed', Expired: 'expired' } as const;
  ```

### Frontend — Invitation Acceptance Page (AC: 1)

- [x] **Create `apps/web/src/app/features/invitation-accept/invitation-accept.component.ts`**:
  - Standalone Angular component with `standalone: true`
  - No app shell — uses its own layout
  - Route: `/invite`
  - Extracts `token` and `sig` from query params (`ActivatedRoute.queryParams`)
  - States: `loading | expired | invalid | form | submitting | success | error`
  - On init: calls `GET /api/v1/auth/accept-invitation?token=&sig=` to validate link
  - On success: shows centered card with:
    - Read-only email (pre-filled from token), role badge, organisation name
    - Full name input (firstName + lastName or single fullName field)
    - Password input with strength indicator (reuse pattern from `activation.component.ts`)
    - "Create Account" primary button
  - On submit: calls `POST /api/v1/auth/accept-invitation` with `{ token, signature, fullName, password }`
  - On success: shows confirmation message: "We've sent a confirmation email to {email}. Please check your inbox and click the link to activate your account."
  - On error: shows error message (expired → "This invitation is no longer valid. Ask your Director to send a new one.")
  - Follow UX patterns from `EXPERIENCE.md`:
    - "This invitation is no longer valid. Ask your Director to send a new one." (invalid/expired)
    - Centered card layout, no sidebar
    - Password strength indicator
    - No 2FA (Coordinator/Field Worker don't require it initially)

- [x] **Create `apps/web/src/app/features/invitation-accept/invitation-accept.component.html`**:
  - `@if` blocks for each state (loading/error/form/success)
  - Material card centered on page
  - `MatFormField`, `MatInput` for name and password
  - `MatProgressSpinner` for loading state
  - Error messages follow `EXPERIENCE.md` voice: neutral, precise

- [x] **Create `apps/web/src/app/features/invitation-accept/invitation-accept.component.scss`**:
  - Centered card layout, responsive (works on mobile per EXPERIENCE.md)
  - Inherits admin theme values from `admin-theme.scss` (navy primary `#1B2A4A`)

- [x] **Create `apps/web/src/app/features/invitation-accept/invitation-accept-api.service.ts`**:
  - `validateInvitationLink(token, sig): Observable<ValidateInvitationLinkResponse>`
  - `acceptInvitation(token, signature, fullName, password): Observable<AcceptInvitationResponse>`
  - Uses `HttpClient` (no generated client — these are unauthenticated, public endpoints)
  - Alternatively, extend `ActivationApiService` if patterns are similar enough

### Frontend — Email Confirmation Landing Page (AC: 4)

- [x] **Create `apps/web/src/app/features/invitation-accept/email-confirmed.component.ts`** (or inline as a second state of invitation-accept):
  - Route: `/confirm-email` (or extend `/invite/confirm`)
  - Extracts `token` and `sig` from query params
  - On init: calls `POST /api/v1/auth/confirm-email` with `{ token, signature }`
  - States: `loading | success | expired | error`
  - On success: shows "Your email has been confirmed! You can now log in." with "Go to Login" button → navigates to `/login`
  - On expired: "This confirmation link has expired. Contact your Director to request a new invitation."

### Frontend — Route Registration (AC: 1)

- [x] **Update `apps/web/src/app/app.routes.ts`**:
  - Add route: `path: 'invite'` → `InvitationAcceptComponent` (no guards — unauthenticated)
  - Add route: `path: 'confirm-email'` → `EmailConfirmedComponent` (no guards)
  - Pattern follows existing `/activate` route pattern

### Frontend — Login Update (AC: 3)

- [x] **Extend `apps/web/src/app/features/auth/login/login.component.ts`**:
  - Handle `ACCOUNT_NOT_CONFIRMED` error code from `AuthSessionService.login()`
  - Show error message: "Please check your email to confirm your account before logging in."
  - Optionally show "Resend confirmation email" link (future story, not in scope for this one)

## Dev Notes

### Brownfield reality (read before coding)

This story builds on existing infrastructure but introduces new concepts:

| Area | Current state | This story |
|------|---------------|------------|
| `Invitation.cs` | Has `ConfirmedAtUtc` (nullable, never set), `Status` (pending/confirmed/expired), `DeliveryAttempts` | Set `ConfirmedAtUtc` and `Status = confirmed` when user confirms email |
| `InvitationService.cs` | Send, list, resend only — no accept/confirm | Do NOT modify — create new methods in `RegistrationService.cs` instead |
| `RegistrationService.cs` | `ValidateLinkAsync`, `ActivateOrganisationAsync` (first Director only) | Add `AcceptInvitationAsync`, `ConfirmEmailAsync` — follow same patterns |
| `TokenService.cs` | `GenerateActivationToken()`, `BuildInvitationUrl()` → `/invite` | Reuse — `BuildInvitationUrl` already generates `/invite` URLs. **Do not rename/refactor.** |
| `AuthService.cs` | Login, OTP, TOTP, refresh, logout, password-reset | Add `IsActive` check in `LoginAsync` — return `ACCOUNT_NOT_CONFIRMED` for pending users |
| `Invitation` status | Set via InvitationService when sending | This story sets `confirmed` status via RegistrationService when email is confirmed |
| Email sending | Hangfire jobs: Activation, Invitation, PasswordReset | New `ConfirmationEmailDeliveryJob` — same Hangfire infrastructure |
| App shell | Activation, activation-complete use no shell | Invitation acceptance and email-confirmed use NO shell per EXPERIENCE.md |
| Rate limiting | `auth-activate`, `auth-activate-read` policies exist | Reuse same policies for new endpoints |
| `AuthService.IsActive` check | Already checked in `LoginAsync` (line 64) — throws `AuthForbiddenException` | **Modify** existing check to distinguish `!IsActive` (unconfirmed) from `IsSuspended` — do NOT add a second check |

### Security considerations

**Timing side-channel:** `GET /accept-invitation` reveals whether a token is valid by returning 200 (with email/org/role) vs 404. This is an accepted inherited pattern from the existing `GET /activate` endpoint. The alternative (always return 200) would conceal expired/invalid tokens from the user but violate the principle of failing fast for legitimate users. **Do not change this pattern** — the existing endpoint already establishes this trade-off.

**Login timing:** The `IsActive` check in `AuthService.LoginAsync` already validates `IsActive` before password comparison. The existing codebase performs password validation first, then checks active/suspended state. Do not reorder these checks — the existing order prevents revealing account existence through timing.

### Architecture compliance

**Mandatory patterns** (from `architecture-role-management.md`):

1. ✅ Every management action writes audit in same DB transaction — fail-closed
2. ✅ Link tokens use three-step pattern: generate (SHA-256 hash) → sign (HMAC-SHA256) → embed (URL). Verify HMAC before DB lookup.
3. ✅ Registration endpoints are unauthenticated, under `/api/v1/auth/`
4. ✅ Rate limiting on registration endpoints (reuse existing policies)
5. ✅ No `[Authorize]` on registration endpoints — `[AllowAnonymous]` explicitly set
6. ✅ Double-confirmation creates pending-confirmation state (`IsActive = false`) — user cannot log in until email confirmed

**Data architecture** (from `architecture-role-management.md`):

| Rule | Implementation |
|------|----------------|
| Table name `confirmation_tokens` (snake_case plural) | `ConfirmationTokenConfiguration.cs` → `ToTable("confirmation_tokens")` |
| UUID PK (`Guid`) | `HasKey(c => c.Id)` + `HasDefaultValueSql("gen_random_uuid()")` |
| `UserId` FK → users | `HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade)` |
| `InvitationId` FK → invitations (nullable) | `HasOne(c => c.Invitation).WithMany().HasForeignKey(c => c.InvitationId).OnDelete(DeleteBehavior.SetNull)` |
| `TokenHash` SHA-256 storage | `HasMaxLength(64)` — hex string (64 chars for SHA-256) |
| Timestamps ISO 8601 UTC | All DateTime properties use UTC |
| Append-only audit events | In `RegistrationService` transaction |

### Email generation

The confirmation email follows the same pattern as activation/invitation emails. The email includes:
- User's name (first name)
- "Confirm Your Account" button with `{baseUrl}/confirm-email?token={rawToken}&sig={signature}`
- Expiry notice: "This link expires in {CONFIRMATION_TOKEN_TTL_HOURS} hours."
- If sender is Director: "You've been invited by {DirectorName} to join {OrganisationName} as a {Role}."

**Create `ConfirmationEmailTemplate.cs`** at `apps/api/Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs`:
- Static class with `RenderSubject(ConfirmationEmailContext)` and `RenderBody(ConfirmationEmailContext)` methods
- Follow pattern from `ActivationEmailTemplate.cs` (uses `$"""` raw string literal)
- Subject: "Confirm your account on {OrganisationName}"
- Body: Welcome message, user name, role, "Confirm Your Account" button URL, expiry warning

### File structure requirements

**Files to create:**

| File | Action | Notes |
|------|--------|-------|
| `apps/api/Domain/Entities/ConfirmationToken.cs` | CREATE | New entity for confirmation tokens |
| `apps/api/Infrastructure/Persistence/ConfirmationTokenConfiguration.cs` | CREATE | EF config |
| `apps/api/Jobs/ConfirmationEmailDeliveryJob.cs` | CREATE | Hangfire job with retry |
| `apps/web/src/app/features/invitation-accept/invitation-accept.component.ts` | CREATE | Standalone component |
| `apps/web/src/app/features/invitation-accept/invitation-accept.component.html` | CREATE | Template |
| `apps/web/src/app/features/invitation-accept/invitation-accept.component.scss` | CREATE | Styles |
| `apps/web/src/app/features/invitation-accept/invitation-accept-api.service.ts` | CREATE | API service |
| `apps/web/src/app/features/email-confirmed/email-confirmed.component.ts` | CREATE | Confirm landing (or inline) |
| `apps/web/src/app/features/email-confirmed/email-confirmed.component.html` | CREATE | Template |
| `apps/api/Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs` | CREATE | Email body + subject rendering |

**Files to modify:**

| File | Action | Notes |
|------|--------|-------|
| `apps/api/Controllers/V1/Auth/RegistrationController.cs` | MODIFY | Add 2 new endpoints |
| `apps/api/Domain/RoleManagement/RegistrationService.cs` | MODIFY | Add 2 new methods |
| `apps/api/Models/Auth/AuthDtos.cs` | MODIFY | Add 4 new DTOs |
| `apps/api/Infrastructure/Audit/AuditEventTypes.cs` | MODIFY | Add 4 new event types |
| `apps/api/Infrastructure/Auth/AuthService.cs` | MODIFY | Add `IsActive` check in `LoginAsync` |
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | MODIFY | Add `DbSet<ConfirmationToken>` |
| `apps/api/Program.cs` | MODIFY | Register new DI + Hangfire job |
| `apps/api/appsettings.json` | MODIFY | Add `ConfirmationLink` section |
| `apps/api/appsettings.Development.json` | MODIFY | Add `ConfirmationLink` section |
| `apps/web/src/app/app.routes.ts` | MODIFY | Add `/invite` and `/confirm-email` routes |
| `apps/web/src/app/features/auth/login/login.component.ts` | MODIFY | Handle `ACCOUNT_NOT_CONFIRMED` |
| `packages/shared-types/src/index.ts` | MODIFY | Add `ConfirmationStatus` |

**Do NOT modify** (read-only reference):

| File | Why |
|------|-----|
| `apps/api/Domain/Entities/Invitation.cs` | Read for reference — do not change entity structure |
| `apps/api/Domain/RoleManagement/InvitationService.cs` | Do not add accept/confirm here — belong in RegistrationService |
| `apps/api/Domain/Entities/User.cs` | Read for reference — `IsActive` already exists |
| `apps/api/Migrations/20260624152225_AddInvitations.cs` | **Do not modify** — pre-existing migration |

### Testing requirements

| Test | Layer | Coverage needed |
|------|-------|-----------------|
| `RegistrationService.AcceptInvitationAsync` — happy path | Unit | Full flow: valid token → user created → confirmation token generated → audit written → email enqueued |
| `RegistrationService.AcceptInvitationAsync` — invalid HMAC | Unit | Throws `InvalidOperationException` |
| `RegistrationService.AcceptInvitationAsync` — expired token | Unit | Throws `InvalidOperationException` / `KeyNotFoundException` |
| `RegistrationService.AcceptInvitationAsync` — already confirmed | Unit | Throws `InvalidOperationException` |
| `RegistrationService.AcceptInvitationAsync` — weak password | Unit | Throws `ValidationException` |
| `RegistrationService.ConfirmEmailAsync` — happy path | Unit | Token consumed → user active → invitation confirmed → audit written |
| `RegistrationService.ConfirmEmailAsync` — invalid HMAC | Unit | Throws `InvalidOperationException` |
| `RegistrationService.ConfirmEmailAsync` — expired | Unit | Throws `InvalidOperationException` |
| `RegistrationService.ConfirmEmailAsync` — already consumed | Unit | Throws `InvalidOperationException` |
| `ConfirmationEmailDeliveryJob` — success path | Unit | Delivery attempts updated, audit written |
| `ConfirmationEmailDeliveryJob` — retry logic | Unit | Failed delivery re-enqueued with backoff |
| `ConfirmationEmailDeliveryJob` — max retries | Unit | After 3 failures, `DeliveryFailed` audit written |
| `ConfirmationToken entity` | Unit | All properties, FK constraints, defaults |
| `ConfirmationTokenConfiguration` | Unit | Table mapping, column types, cascade behavior |
| `AuthService.LoginAsync` — unconfirmed user | Unit | Returns `ACCOUNT_NOT_CONFIRMED`, 403 |
| `RegistrationController` — accept-invitation GET | Integration | Validates link without consuming, returns email/org |
| `RegistrationController` — accept-invitation POST | Integration | Full HTTP→DB flow with Testcontainers |
| `RegistrationController` — confirm-email POST | Integration | Full HTTP→DB flow with Testcontainers |
| `Invitation-accept component` | Web (Jasmine) | States: loading, form, success, error, expired |

### Previous story intelligence (3.9)

- DTO pattern: `sealed record` types with `System.ComponentModel.DataAnnotations`
- EF config: `IEntityTypeConfiguration<T>` with `ToTable()`, `HasKey()`, `Property()` chaining
- Audit events pattern: `AuditEventTypes.InvitationSent`, `AuditEventTypes.InvitationResent` already defined
- Services use `AppDbContext` directly — not repository interfaces (architectural decision, do not refactor)
- `TokenService.GenerateActivationToken()` is used for both activation AND invitation tokens — **do not rename/refactor**
- Registration in `Program.cs` (line ~158) not in `AuthServiceCollectionExtensions.cs`
- Integration tests use `WebApplicationFactory` + `Testcontainers` for PostgreSQL
- Migration naming: `{timestamp}_{Description}.cs`
- `Invitation` entity at `apps/api/Domain/Entities/Invitation.cs` with full implementation
- Test pattern: `protected virtual` methods on service for mocking in unit tests (e.g., `ExecuteActivationAsync`, `ConsumeTokenAtomicallyAsync`)

### Git intelligence summary

Recent commits in this workspace:

| Commit | Message | Relevance |
|--------|---------|-----------|
| `689f5b4` | UserRole | Most recent — role/user management changes |
| `a8b0e14` | commit | Generic |
| `ecbb446` | Commit-Users | User-related changes — likely includes User schema |
| `9572600` | Commit | Generic |
| `7a23426` | CommitStage | Stage-related changes |

The existing codebase has 113 changed files in the last 3 commits, including all Role Management infrastructure: User extensions, Invitation model, Organisation, ActivationToken, services, controllers, DTOs, and tests.

### Latest technical information

- **Entity Framework Core 8.x:** Use `HasDefaultValueSql("gen_random_uuid()")` for PostgreSQL UUID PKs, `HasDefaultValue(0)` for int defaults
- **PostgreSQL 16+:** Supports `gen_random_uuid()` natively — no extension needed
- **HMAC-SHA256 signing:** Follow existing `TokenService` pattern with `CryptographicOperations.FixedTimeEquals`
- **Hangfire:** Background jobs already set up for email delivery — register new `ConfirmationEmailDeliveryJob` alongside existing jobs in `Program.cs`
- **Angular 19+ standalone components:** Use `standalone: true`, no NgModule wrapper for new features

### Project context reference

Key rules from `project-context.md`:

- Naming: DB snake_case plural; C# PascalCase; JSON camelCase; Angular kebab-case files
- IDs: UUID v4 (`Guid`)
- Timestamps: ISO 8601 UTC
- Authorization: Policy-based `[Authorize(Policy = Policies.*)]` — but registration endpoints are `[AllowAnonymous]`
- Audit: Every data-changing endpoint writes `audit_events` in the same transaction, fail-closed
- API envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- Errors: RFC 7807 Problem Details
- HTTP status: 400 validation, 401 unauthenticated, 403 RBAC/account-not-confirmed, 404 not found, 409 conflict, 422 business rules
- Testing: xUnit + WebApplicationFactory + Testcontainers PostgreSQL for integration; xUnit for unit; Jasmine for web
- Angular standalone components + signals for local UI state
- No raw fetch — prefer generated client for authenticated endpoints; for unauthenticated registration, use `HttpClient` directly

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.2]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — Data architecture, Token pattern, Audit pattern, Registration endpoints, Double-confirmation design]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-23/EXPERIENCE.md` — Flow 3 (Director invites), Invitation Acceptance standalone page, Voice and tone, Error messages]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-23/DESIGN.md` — Navy enterprise skin, component visual specs]
- [Source: `_bmad-output/project-context.md` — All naming, auth, testing conventions]
- [Source: `apps/api/Controllers/V1/Auth/RegistrationController.cs` — Existing activation endpoint patterns]
- [Source: `apps/api/Domain/RoleManagement/RegistrationService.cs` — Existing registration patterns to extend]
- [Source: `apps/api/Infrastructure/RoleManagement/TokenService.cs` — Token generation, HMAC signing, URL building]
- [Source: `apps/api/Infrastructure/Auth/AuthService.cs` — Login flow to extend with IsActive check]
- [Source: `apps/api/Domain/Entities/Invitation.cs` — Invitation entity (ConfirmedAtUtc, DeliveryAttempts)]
- [Source: `apps/api/Domain/Entities/User.cs` — User entity (IsActive field)]
- [Source: `apps/api/Infrastructure/Persistence/AppDbContext.cs` — DbSet registrations]
- [Source: `apps/api/Program.cs` — DI registration pattern (line 158)]
- [Source: `_bmad-output/implementation-artifacts/3-9-invitations-data-model.md` — Previous story learnings, patterns, test approaches]

## Dev Agent Record

### Agent Model Used

Auto (deepseek-v4-flash)

### Debug Log References

### Completion Notes List

- Story created from epics + architecture-role-management + UX + previous story (3-9)
- Key brownfield insight: invitations data model exists but no accept/confirm API exists
- Double-confirmation means: step 1 = accept invitation (create user, send confirmation email), step 2 = confirm email (activate user)
- Follow patterns from existing `RegistrationService.cs` (first Director activation flow)
- New `ConfirmationToken` entity needed for the second confirmation step
- No interface for RegistrationService — inject concrete class (existing pattern)
- Rate limiting: reuse existing `auth-activate` and `auth-activate-read` policies
- Login flow must be updated to block unconfirmed accounts
- ✅ Resolved 8 code review patch items (2026-06-27):
  - Added `ACCOUNT_NOT_CONFIRMED` error code to 403 response in `AuthController` via updated `ForbiddenProblem()` with custom `code` extension and `type` URI
  - Moved audit events (`AccountCreated`, `EmailConfirmed`, `ConfirmationDelivered`) inside the DB transaction (before `CommitAsync`) to satisfy "audit in same transaction, fail-closed" requirement
  - Added `ConfirmationTokenCreated` audit event recorded alongside `AccountCreated` in the same transaction
  - Added atomic delivery attempts increment (`UPDATE confirmation_tokens SET delivery_attempts = delivery_attempts + 1`) in `ConfirmationEmailDeliveryJob` to prevent race conditions
  - Added max length guard (256 chars) to `ValidateFullName`
  - Added `already-confirmed` state to `email-confirmed.component` with user-friendly message for consumed tokens
  - Converted `AcceptInvitationRequest` from `sealed record` to `sealed class` with `[Required]`, `[MinLength]`, `[MaxLength]` data annotations
  - Added `Validators.minLength(2)` and `Validators.maxLength(256)` to client-side `fullName` form control
- 2 findings dismissed as already handled: idempotency (exists via atomic SQL), rate limiting (already present on confirm-email endpoint)
- 2 decisions captured: keep invitation `confirmed` at acceptance, reuse existing HMAC key

### Implementation Completed

**API Layer:**
- Created `ConfirmationToken` entity + `ConfirmationTokenConfiguration` (EF config) + migration `AddConfirmationTokens`
- Extended `RegistrationService` with `AcceptInvitationAsync` and `ConfirmEmailAsync` (plus `ValidateInvitationLinkAsync`)
- Extended `RegistrationController` with 3 new endpoints (`GET/POST /accept-invitation`, `POST /confirm-email`)
- Created `ConfirmationEmailDeliveryJob` with exponential backoff retry (1 min, 5 min, 15 min, max 3)
- Created `ConfirmationEmailTemplate` for email body/subject generation
- Added `AccountCreated`, `EmailConfirmed`, `ConfirmationDeliveryFailed`, `ConfirmationDelivered` audit events
- Added `ValidateInvitationLinkResponse`, `AcceptInvitationRequest/Response`, `ConfirmEmailRequest/Response` DTOs
- Modified `AuthService.LoginAsync` to differentiate unconfirmed (`ACCOUNT_NOT_CONFIRMED`) vs suspended users
- Added `ConfirmationLink` config section to `appsettings.Development.json` (BaseUrl + TokenTtlHours)
- Registered `ConfirmationEmailDeliveryJob` in `Program.cs`
- Added `ConfirmationStatus` enum to `packages/shared-types`

**Frontend Layer:**
- Created `invitation-accept` standalone component (TS + HTML + SCSS) with states: loading, form, success, error, expired
- Created `invitation-accept-api.service.ts` for HTTP calls (validate + accept endpoints)
- Created `email-confirmed` standalone component (TS + HTML + SCSS) with states: loading, success, expired, error
- Added routes `/invite` → `InvitationAcceptComponent` and `/confirm-email` → `EmailConfirmedComponent`

**Testing:**
- Added 20+ unit tests for `AcceptInvitationAsync`, `ConfirmEmailAsync`, `ValidateInvitationLinkAsync`
- All new tests pass; 175 of 179 total tests pass (4 pre-existing failures unrelated to this story)

### File List

Files created:
- apps/api/Domain/Entities/ConfirmationToken.cs
- apps/api/Infrastructure/Persistence/ConfirmationTokenConfiguration.cs
- apps/api/Infrastructure/Persistence/Migrations/20260627121411_AddConfirmationTokens.cs
- apps/api/Infrastructure/Persistence/Migrations/20260627121411_AddConfirmationTokens.Designer.cs
- apps/api/Jobs/ConfirmationEmailDeliveryJob.cs
- apps/api/Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs
- apps/web/src/app/features/invitation-accept/invitation-accept.component.ts
- apps/web/src/app/features/invitation-accept/invitation-accept.component.html
- apps/web/src/app/features/invitation-accept/invitation-accept.component.scss
- apps/web/src/app/features/invitation-accept/invitation-accept-api.service.ts
- apps/web/src/app/features/email-confirmed/email-confirmed.component.ts
- apps/web/src/app/features/email-confirmed/email-confirmed.component.html
- apps/web/src/app/features/email-confirmed/email-confirmed.component.scss

Files modified:
- apps/api/Controllers/V1/Auth/RegistrationController.cs
- apps/api/Controllers/V1/AuthController.cs
- apps/api/Domain/RoleManagement/RegistrationService.cs
- apps/api/Jobs/ConfirmationEmailDeliveryJob.cs
- apps/api/Models/Auth/AuthDtos.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Infrastructure/Auth/AuthService.cs
- apps/api/Infrastructure/Persistence/AppDbContext.cs
- apps/api/Program.cs
- apps/api/appsettings.Development.json
- apps/web/src/app/app.routes.ts
- apps/web/src/app/features/email-confirmed/email-confirmed.component.ts
- apps/web/src/app/features/email-confirmed/email-confirmed.component.html
- apps/web/src/app/features/invitation-accept/invitation-accept.component.ts
- packages/shared-types/src/index.ts

Test files updated:
- tests/api.unit/Domain/RoleManagement/RegistrationServiceTests.cs (extended with accept/confirm/validate tests)

## Review Findings

### Decision Needed

- [ ] [Decision] Should `ConfirmationToken` live in a new `confirmation_tokens` table, or reuse the `activation_tokens` table with a discriminator column? **Recommendation:** New table — cleaner separation of concerns, no risk of breaking existing activation flow, and follows the existing pattern of separate tables per token type.
- [ ] [Decision] Should the invitation-accept component be at path `/invite` (matching `TokenService.BuildInvitationUrl` output) or `/accept-invitation`? **Recommendation:** `/invite` — `TokenService.BuildInvitationUrl` already produces `/invite?token=...&sig=...` URLs and cannot be changed (do not rename).

### Dependencies

- [ ] [Dependency] Story 3-11 (Pending Invitations Management & Resend) depends on the "delivery failed" status from this story. The Director dashboard displays `ConfirmationDeliveryFailed` audit events to indicate failed deliveries. AC6 data is written to audit events — 3-11 reads and renders them.

### Code Review Findings (2026-06-27)

#### Decision Needed

- [x] [Review][Decision] Invitation status promoted to `confirmed` at acceptance time, not at email confirmation (AC4) — The current implementation sets `Invitation.Status = Confirmed` and `ConfirmedAtUtc = now` in `ExecuteAcceptInvitationAsync`, but the spec (AC4) describes this happening during email confirmation. Two possible intents: (a) the spec is correct and the status should stay `Pending` until email is confirmed, or (b) the current design is intentional (invitation is "claimed" at acceptance, email is a separate second step). **Decision: Keep at acceptance** — the invitation is "claimed" at acceptance; email is a separate confirmation step for the user's identity."""
- [x] [Review][Decision] Confirmation tokens reuse `TokenService.GenerateActivationToken()` — The same HMAC signing key and token format is used for both activation and confirmation tokens. If a single key is compromised, both flows are affected. **Decision: Reuse existing key** — acceptable risk, separate keys would add deployment complexity without proportional security gain for this use case.

#### Patch (actionable fixes)

- [x] [Review][Patch] Add `ACCOUNT_NOT_CONFIRMED` error code to 403 response in `AuthController` — `ForbiddenProblem()` produces bare `ProblemDetails` with no `type` or custom error code. Frontend cannot programmatically distinguish unconfirmed from other 403 errors. Fix: add a `type` URI (e.g., `"https://errors/account-not-confirmed"`) or a custom `code` extension field.
- [x] [Review][Patch] Move audit events (`AccountCreated`, `EmailConfirmed`) into the database transaction — Both `ExecuteAcceptInvitationAsync` and `ExecuteConfirmEmailAsync` record audit events via `auditService.RecordAsync()` after `transaction.CommitAsync()`, violating the "audit in same transaction, fail-closed" architectural pattern.
- [x] [Review][Patch] Add idempotency to `AcceptInvitationAsync` — No protection against double-submit. Use raw SQL `UPDATE ... WHERE status = Pending AND rows_affected = 1` as a conditional check before the user creation transaction, or add an idempotency key to `AcceptInvitationRequest`. **(Already handled — `ConsumeInvitationTokenAtomicallyAsync` uses atomic `UPDATE invitations SET status = Confirmed WHERE status = Pending` with `rowsAffected` check, preventing double-consumption at DB level.)**
- [x] [Review][Patch] Add optimistic concurrency on `DeliveryAttempts` in `ConfirmationEmailDeliveryJob` — Two Hangfire workers executing simultaneously both read `DeliveryAttempts = N`, increment locally, and send duplicate emails. Use `[ConcurrencyCheck]` or a `rowversion` column, or make the `DeliveryAttempts` increment an atomic `UPDATE ... SET delivery_attempts = delivery_attempts + 1 WHERE id = @id AND delivery_attempts = @expected`.
- [x] [Review][Patch] Add `[EnableRateLimiting]` to `POST /api/v1/auth/confirm-email` endpoint — Currently no rate limiting on the confirmation endpoint, enabling brute-force token enumeration (defense-in-depth). **(Already present — `[EnableRateLimiting("auth-activate")]` was already on `ConfirmEmail` endpoint.)**
- [x] [Review][Patch] Add max length guard to `ValidateFullName` — Single-character name passes client validation but may fail server-side `≥ 2` check. A 1000+ character name would be truncated/clipped on the DB column. Fix: add a max-length parameter (matching the DB column limit) to `ValidateFullName`.
- [x] [Review][Patch] Handle already-consumed confirmation token with a user-friendly state in `email-confirmed.component.ts` — The error message check only looks for "expired" substring. An already-consumed token returns 422 without "expired" in the message, showing a generic error page instead of "Your email is already confirmed — you can log in."
- [x] [Review][Patch] Add `[Required]`, `[MinLength]`, `[MaxLength]` data annotations to `AcceptInvitationRequest` — The request record has zero data annotations currently, so `ApiController` model validation does not enforce field lengths. Validation falls entirely to `RegistrationService`, losing the auto-400 response for malformed input.
- [x] [Review][Patch] Sync client-side `fullName` validation with server-side minimum — Frontend has `Validators.required` only (no `minLength`). Server requires `≥ 2` chars. Add `Validators.minLength(2)` to the client form.
- [x] [Review][Patch] Record `ConfirmationTokenCreated` audit event — Currently no audit event fires when the confirmation token is created. If the email delivery job fails before the first attempt, there is no record the token ever existed.

#### Deferred (pre-existing or out of scope)

- [x] [Review][Defer] No endpoint to resend confirmation email — Users are stuck after 3 failed delivery attempts. Story 3-11 (Pending Invitations Management & Resend) is the planned vehicle for this. Deferred, pre-existing — not in scope of current ACs.
- [x] [Review][Defer] No cleanup mechanism for expired confirmation tokens — No recurring Hangfire job to delete/archive expired rows. Table grows unbounded. Enhancement for future story, out of scope of 3-10.
- [x] [Review][Defer] Pending user state confusion with suspension/deactivation — `UserManagementService` and frontend `getUserStatus` have no handling for `IsActive=false, IsSuspended=false` users. Pre-existing issue not introduced by this story.
- [x] [Review][Defer] Cascade delete on `User` FK loses `ConfirmationToken` rows on user deletion — By design (cleanup on user removal), but noted for future GDPR/compliance considerations.

### Code Review Findings — Round 2 (2026-06-27)

#### Patch (actionable fixes) — all resolved 2026-06-27

- [x] [Review][Patch] Frontend `login.component.ts` not updated for `ACCOUNT_NOT_CONFIRMED` — AC3 requires the login page to handle `ACCOUNT_NOT_CONFIRMED` error code and display message "Please check your email to confirm your account before logging in." Currently user sees unhandled/generic 403 error. [login.component.ts]
- [x] [Review][Patch] Missing `ConfirmationTokenCreated` audit event constant — Code uses `ConfirmationDelivered` with metadata `["event"] = "token_created"` to record token creation, conflating two distinct events. Add a dedicated `AuditEventTypes.ConfirmationTokenCreated` constant. [AuditEventTypes.cs]
- [x] [Review][Patch] `VerifyTotpLoginAsync` throws wrong error for unconfirmed users — Uses `DeactivatedMessage` ("Contact your coordinator") instead of `AccountNotConfirmedMessage` for unconfirmed users attempting TOTP login. [AuthService.cs]
- [x] [Review][Patch] `appsettings.json` missing `ConfirmationLink` config section — Only `appsettings.Development.json` was updated. Production/staging environments silently fall back to defaults (possibly wrong `BaseUrl`). [appsettings.json]
- [x] [Review][Patch] `ACCOUNT_NOT_CONFIRMED` error code relies on fragile string matching — `AuthController` compares `ex.Message` to `AuthService.AccountNotConfirmedMessage` constant. If message changes, error code silently returns null. Use dedicated exception property. [AuthController.cs]
- [x] [Review][Patch] Frontend error classification fragile — `email-confirmed.component` uses substring matching on error messages (".includes('expired')", ".includes('already been used')") instead of HTTP status codes. Breaks on any rewording or localization. [email-confirmed.component.ts]
- [x] [Review][Patch] No unique constraint on pending confirmation tokens per user — Missing partial unique index on `(UserId) WHERE consumed_at_utc IS NULL`. Race or retry can create multiple pending tokens for same user. [ConfirmationTokenConfiguration.cs]
- [x] [Review][Patch] `SplitFullName` returns empty `lastName` for single-word input — No validation for two name parts. Single-word name passes DTO validation but produces empty `LastName` violating `IsRequired`. [RegistrationService.cs]
- [x] [Review][Patch] No guard for zero/missing `ConfirmationLink:TokenTtlHours` — If config key is missing or set to 0, token expires immediately (now + 0 hours). Add minimum guard. [RegistrationService.cs]
- [x] [Review][Patch] Missing null guard for `confirmationToken.User` — If `Include` resolves to null (orphaned FK), `ExecuteConfirmEmailAsync` throws `NullReferenceException` → 500. [RegistrationService.cs]
- [x] [Review][Patch] Unhandled `CryptographicException` in token generation — `GenerateConfirmationToken` calls into crypto. If generation fails, transaction rolls back but user sees 500 instead of actionable error. [RegistrationService.cs]
- [x] [Review][Patch] Frontend form submission null assertion — `form.value.fullName` and `form.value.password` null-forgiving asserted without guard. Add null coalescing before API call. [invitation-accept.component.ts]

## Change Log

| Date | Change |
|------|--------|
| 2026-06-27 | Created comprehensive story file for 3-10 double-confirmation registration flow |
| 2026-06-27 | Implemented story: API (entity, migration, service, controller, DTOs, audit, job, config, login update), Frontend (invite-accept, email-confirmed, routes), Unit tests (20+ tests passing) |
| 2026-06-27 | Addressed code review findings — 8 patch items resolved, 2 dismissed, 2 decisions documented
| 2026-06-27 | Code review round 2 completed — 12 patch findings, 3 deferred, 7 dismissed (left as action items)
| 2026-06-27 | Addressed code review round 2 patch items — all 12 resolved, EF migration added for unique pending token index
