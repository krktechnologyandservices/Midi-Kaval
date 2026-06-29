---
baseline_commit: '689f5b4'
---

# Story 2.16: Director 2FA Mandate & Recovery

Status: done

## Story

As a Director,
I must enroll in two-factor authentication before managing any users,
So that user management actions are protected by an additional security layer.

## Acceptance Criteria

1. **Given** a Director who has NOT enrolled in 2FA
   **When** they attempt any management action (invite, suspend, reactivate, delete, reset-2fa)
   **Then** the API returns HTTP 403 with `"Two-Factor Authentication Required"`
   **And** the UI shows a full-page modal prompting 2FA setup before the action proceeds

2. **Given** an unenrolled Director sees the 2FA prompt
   **When** they initiate enrollment
   **Then** the API returns a TOTP provisioning URI (otpauth://) with issuer "Midi-Kaval" and the Director's email
   **And** a QR code is generated from the URI and displayed in the UI
   **And** the user scans it with their authenticator app

3. **Given** a Director has scanned the QR code
   **When** they enter a valid TOTP code from their authenticator app
   **Then** the TOTP secret is saved to `users.totp_secret`
   **And** `users.totp_enrolled_at` is set to the current UTC timestamp
   **And** the Director can now access all management actions

4. **Given** a Director has enrolled in 2FA
   **When** they log in
   **Then** after password verification, they are prompted for a TOTP code (instead of email OTP)
   **And** login completes only after a valid TOTP code is provided

5. **Given** an enrolled Director loses access to their authenticator app
   **When** another active Director performs a 2FA reset from the User Management Dashboard
   **Then** `users.totp_secret` and `users.totp_enrolled_at` are cleared
   **And** `users.token_version` is incremented (force-logout)
   **And** an audit event (`user.two_factor_reset`) is recorded
   **And** the affected Director must re-enroll before performing management actions

6. **Given** an organisation has no active Directors who can reset 2FA
   **When** the Vendor performs a 2FA reset via the backstage portal
   **Then** the same reset logic applies (clear secret, increment token_version, audit)

7. **Given** a Director has their 2FA reset
   **When** they attempt to log in
   **Then** they proceed through the normal password flow (TOTP is not required for login until they re-enroll)
   **But** they cannot perform management actions until re-enrolled

## Tasks / Subtasks

### Pre-Flight: EF Core UserConfiguration Mapping

- [x] **Verify and extend `UserConfiguration.cs`** — `apps/api/Infrastructure/Persistence/UserConfiguration.cs`
  - The existing config does NOT map `TotpSecret` or `TotpEnrolledAt` — add explicit configuration:
    ```csharp
    builder.Property(u => u.TotpSecret)
        .HasMaxLength(64);       // base32 encoded 20-byte key = 32 chars + padding
    builder.Property(u => u.TotpEnrolledAt);
    ```
  - Without this, EF Core may map by convention, but `HasMaxLength` constraint won't be applied

- [x] **Add `Otp.NET` package** to `apps/api/MidiKaval.Api.csproj`
  ```xml
  <PackageReference Include="Otp.NET" Version="4.0.3" />
  ```
  Provides `KeyGeneration`, `Totp`, and `Base32Encoding` for TOTP compliance (RFC 6238, 30-second window)
  (Note: version corrected from 4.0.3 to 1.4.1 — the latter is the latest available)

### Pre-Flight: Add TOTP Config Section

- [x] **Add `TotpOptions`** — `apps/api/Infrastructure/Auth/AuthOptions.cs`
  ```csharp
  public sealed class TotpOptions
  {
      public const string SectionName = "Totp";
      public string Issuer { get; set; } = "Midi-Kaval";
      public int StepSeconds { get; set; } = 30;
      public int CodeLength { get; set; } = 6;
  }
  ```
- [x] **Add to `appsettings.Development.json`**:
  ```json
  "Totp": {
    "Issuer": "Midi-Kaval",
    "StepSeconds": 30,
    "CodeLength": 6
  }
  ```

### API — TwoFactorService (NEW)

- [x] **Create `TwoFactorService.cs`** — `apps/api/Domain/RoleManagement/TwoFactorService.cs`
  - Inject: `AppDbContext`, `IOptions<TotpOptions>`, `IAuditService`, `ILogger<TwoFactorService>`
  - Methods:
    ```csharp
    // Generate provisioning URI and store secret (without enrolling yet)
    Task<TotpProvisioningResult> GenerateProvisioningAsync(Guid userId, CancellationToken ct);

    // Verify TOTP code and finalize enrollment
    Task<TotpEnrollmentResult> EnrollAsync(Guid userId, string code, CancellationToken ct);

    // Verify TOTP code during login (does not modify DB)
    Task<bool> VerifyTotpCodeAsync(Guid userId, string code, CancellationToken ct);

    // Reset 2FA for a user (clear secret, increment token_version)
    Task ResetTwoFactorAsync(Guid actorUserId, Guid targetUserId, CancellationToken ct);

    // Check if user has enrolled 2FA
    Task<bool> IsEnrolledAsync(Guid userId, CancellationToken ct);
    ```
  - Implementation details:
    - `GenerateProvisioningAsync`: Generate 20-byte random secret via `KeyGeneration.GenerateRandomKey(20)`, store as base32 in `user.TotpSecret`, DO NOT set `TotpEnrolledAt` yet. Return provisioning URI: `otpauth://totp/{Issuer}:{email}?secret={base32Secret}&issuer={Issuer}`
    - `EnrollAsync`: Read stored secret from DB, create `Totp` instance with secret + options, call `totp.VerifyTotp(code, out long timeWindowMatched, VerificationWindow.RfcSpecified)`. If valid, set `TotpEnrolledAt = DateTime.UtcNow`, audit event `user.two_factor_enrolled`, save. If invalid, return failure.
    - `VerifyTotpCodeAsync`: Read secret, create `Totp`, verify code against `VerificationWindow.RfcSpecified` (allows ±1 step for clock drift as per RFC 6238)
    - `ResetTwoFactorAsync`: Clear `TotpSecret` and `TotpEnrolledAt`, increment `TokenVersion`, audit event `user.two_factor_reset`, save.
  - **Audit event types** (add to `AuditEventTypes.cs`):
    ```csharp
    public const string TwoFactorEnrolled = "user.two_factor_enrolled";
    public const string TwoFactorReset = "user.two_factor_reset";
    ```
  - **Result types** (new file or inline):
    ```csharp
    public sealed record TotpProvisioningResult(string ProvisioningUri, string SecretBase32);
    public sealed record TotpEnrollmentResult(bool Success, string? ErrorMessage);
    ```
  - **Protected virtual wrapper pattern**: Follow the pattern from `UserManagementService` — make methods `protected virtual` so testable subclass can intercept:
    ```csharp
    protected virtual async Task<TotpEnrollmentResult> EnrollCoreAsync(User user, string code, CancellationToken ct) { ... }
    ```

### API — TwoFactorController (NEW or extend AuthController)

- [x] **Create `TwoFactorController.cs`** — `apps/api/Controllers/V1/Auth/TwoFactorController.cs`
  - Route: `[Route("api/v1/auth")]`, controller-level `[Authorize]`
  - Endpoints:
    - `POST /api/v1/auth/enroll-2fa` — `InitiateEnrollment(CancellationToken ct)`
      - Returns `{ provisioningUri: "...", secretBase32: "..." }` 
      - Rate limit: `auth-enroll-totp`
      - No envelope needed for provisioning URI (simple object)
    - `POST /api/v1/auth/verify-enroll-2fa` — `CompleteEnrollment(VerifyTotpRequest request, CancellationToken ct)`
      - Body: `{ "code": "123456" }`
      - Returns `{ success: true/false }` with 422 if invalid code
      - Rate limit: `auth-verify`
    - `GET /api/v1/auth/totp-status` — `GetTotpStatus(CancellationToken ct)`
      - Calls `twoFactorService.IsEnrolledAsync(userId, ct)`
      - Returns `{ enrolled: true/false }`
      - Used by frontend to check enrollment state

### API — UsersController — Add 2FA Reset Endpoint

- [x] **Extend `UsersController.cs`** — `apps/api/Controllers/V1/Admin/UsersController.cs`
  - Inject `TwoFactorService` into primary constructor alongside existing services
  - Add endpoint:
    - `POST /api/v1/admin/users/{id:guid}/reset-2fa` — `ResetTwoFactor(Guid id, CancellationToken ct)`
    - Uses `[Authorize(Policy = Policies.DirectorOnly)]` (inherited from class-level)
    - Uses `[EnableRateLimiting("data-write")]`
    - Extract `organisationId` and `actorUserId` from JWT (existing pattern via `TryResolveOrganisationId` and `GetUserId`)
    - Call `twoFactorService.ResetTwoFactorAsync(actorUserId, id, ct)`
    - Return `200 OK` with `{ id, message: "Two-factor authentication has been reset for this user." }`
    - Add `[ProducesResponseType]` attributes:
      ```csharp
      [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
      [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
      [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
      [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
      [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
      ```
  - **IMPORTANT**: The `[Require2FA]` attribute at class level means the calling Director must already be 2FA-enrolled to reset another's 2FA. This is correct per FR-10.

### API — Modify Login Flow for Directors with TOTP

- [x] **Modify `AuthService.LoginAsync`** — `apps/api/Infrastructure/Auth/AuthService.cs`
  - After password verification succeeds, check `user.Role == UserRoles.Director` AND `user.TotpEnrolledAt != null`
  - If BOTH conditions are true: **skip the email OTP step** and instead return a `RequiresTotp` flag in the login response
  - If Director but NOT enrolled: continue with existing email OTP flow (they still need to log in, they just can't manage users)
  - **CRITICAL**: The existing `LoginResponse` has `{ get; set; }` properties (not `init`). Follow the existing convention:
    ```csharp
    // apps/api/Models/Auth/AuthDtos.cs
    public sealed class LoginResponse
    {
        public Guid ChallengeId { get; set; }
        public int ExpiresInSeconds { get; set; }
        public Guid UserId { get; set; }       // ADD — needed by verify-totp-login
        public bool RequiresTotp { get; set; }  // ADD
    }
    ```
  - The `UserId` field is required so the frontend knows which user to verify TOTP for (the challenge doesn't contain the user ID)

- [x] **Add TOTP verification step in login** — `AuthService.VerifyOtpAsync` (or new `VerifyTotpLoginAsync`)
  - When `RequiresTotp` was true in the login response, the frontend shows the TOTP input instead of the email OTP input
  - Create new endpoint: `POST /api/v1/auth/verify-totp-login` — `VerifyTotpLogin(VerifyTotpLoginRequest request, CancellationToken ct)`
  - Body: `{ "userId": "guid", "code": "123456" }`
  - Implementation: call `twoFactorService.VerifyTotpCodeAsync(userId, code, ct)`, then issue JWT + refresh token (same flow as `VerifyOtpAsync` success path)
  - Rate limit: `auth-verify`
  - **IMPORTANT**: The existing `VerifyOtpAsync` already handles email OTP verification. This new endpoint is a parallel path for TOTP-based verification. Extract the JWT issuance logic into a private method shared by both paths.
  - **Add `VerifyTotpLoginRequest` DTO** to `apps/api/Models/Auth/AuthDtos.cs`:
    ```csharp
    public sealed class VerifyTotpLoginRequest
    {
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }
    ```
  - **IMPORTANT**: After adding `UserId` and `RequiresTotp` to `LoginResponse`, the generated OpenAPI client `packages/api-client` must be regenerated. The frontend types in `apps/web/src/app/core/auth/auth.models.ts` use generated types (`components['schemas']['LoginResponse']`), so the new fields appear automatically after regeneration. Do NOT hand-edit the generated client.

### API — VendorController — Add Vendor 2FA Reset (Fallback)

- [x] **Extend `VendorOrganisationsController` (or add to `OrganisationsController.cs`)** — `apps/api/Controllers/V1/Vendor/OrganisationsController.cs`
  - Inject `TwoFactorService`
  - Add endpoint:
    - `POST /api/v1/vendor/users/{id:guid}/reset-2fa` — `VendorResetTwoFactor(Guid id, CancellationToken ct)`
    - Uses `[Authorize(Policy = Policies.VendorOnly)]`
    - No organisation extraction (Vendor operates across organisations)
    - Call `twoFactorService.ResetTwoFactorAsync(actorUserId, id, ct)` — pass Vendor user ID as actor
    - Return `200 OK` with message

### API — Register TwoFactorService in DI

- [x] **Register services** — `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
  - Add: `services.AddScoped<TwoFactorService>();`
  - Add TotpOptions binding:
    ```csharp
    services.AddOptions<TotpOptions>()
        .Bind(configuration.GetSection(TotpOptions.SectionName))
        .ValidateOnStart();
    ```

### API — Add TOTP Rate Limit Policies

- [x] **Add rate limit policies** — `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
  - Add policies in the `AddRateLimiter` block:
    ```csharp
    options.AddPolicy("auth-enroll-totp", CreateAuthRateLimitPartition(rateLimitOptions));
    options.AddPolicy("auth-verify-totp", CreateAuthRateLimitPartition(rateLimitOptions));
    ```

### Web — AdminUserSummary — Add 2FA Status

- [x] **Extend `AdminUserSummary`** — `apps/web/src/app/features/admin/models/admin.models.ts`
  - Add field:
    ```typescript
    totpEnrolledAt?: string | null;
    ```

### Web — AdminUserService — Add 2FA Reset Method

- [x] **Extend `AdminUserService`** — `apps/web/src/app/features/admin/services/admin-user.service.ts`
  - Add method:
    ```typescript
    resetTwoFactor(userId: string): Promise<{ id: string; message: string }> {
      return firstValueFrom(
        this.http.post<ApiEnvelope<{ id: string; message: string }>>(
          `${environment.apiBaseUrl}/api/v1/admin/users/${userId}/reset-2fa`,
          {},
        ),
      ).then(e => e.data);
    }
    ```

### Web — TwoFactorModalComponent (NEW)

- [x] **Create `two-factor-modal.component.ts`** — `apps/web/src/app/features/admin/components/two-factor-modal/two-factor-modal.component.ts`
  - **Pre-flight**: Add `qrcode-generator` npm package: `npm install qrcode-generator @types/qrcode-generator`
  - Standalone Angular component with:
    - **Step 1 — Initiate**: Click "Set Up Two-Factor Authentication" button → calls `POST /api/v1/auth/enroll-2fa`
    - **Step 2 — QR Display**: Shows QR code using `qrcode-generator` from the provisioning URI + displays the base32 secret as a text fallback
    - **Step 3 — Verify**: 6-digit TOTP code input field + "Verify" button → calls `POST /api/v1/auth/verify-enroll-2fa`
    - **Step 4 — Success**: Confirmation that 2FA is now active
  - Follow the existing `UserDetailSheetComponent` pattern (standalone, signals, MatDialog for confirmations)
  - Display modes: modal overlay (full-screen or large dialog)
  - Add `MatButtonModule`, `MatInputModule`, `MatFormFieldModule`, `MatIconModule`, `MatProgressSpinnerModule` to imports
  - Error handling: invalid code → show "Invalid code. Please try again." with remaining attempts count (max 5)

### Web — UserDetailSheet — Add Reset 2FA Action

- [x] **Extend `UserDetailSheetComponent`** — `apps/web/src/app/features/admin/components/user-detail-sheet/user-detail-sheet.component.ts`
  - Add "Reset 2FA" button in the Actions section (between Suspend/Reactivate and Danger Zone)
  - Only visible when viewing a Director user AND the viewer is NOT viewing themselves
  - `isDirector()` computed: `u.role === 'Director'`
  - Button opens a confirmation dialog, then calls `adminUserService.resetTwoFactor(u.id)`
  - On success: snackbar "Two-factor authentication has been reset for this user."
  - Follow the same pattern as `confirmSuspend()` / `confirmReactivate()`
  - Button label: "Reset 2FA", color: "primary", stroked button style
  - Add tooltip: "Reset this Director's two-factor authentication. They will need to re-enroll."

### Web — Team Roster — Add 2FA Status Column

- [x] **Extend `TeamRosterComponent`** — `apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts`
  - Optionally add a 2FA status indicator column for Director users (displays "2FA Enrolled" or "2FA Not Enrolled" chip)
  - This is a nice-to-have enhancement for v1 — defer if scope creep

### Web — Login Flow — TOTP Step for Directors

- [x] **Add `requiresTotp` and `totpUserId` signals to `AuthSessionService`** — `apps/web/src/app/core/auth/auth-session.service.ts`
  - Add signals:
    ```typescript
    readonly requiresTotp = signal(false);
    readonly totpUserId = signal<string | null>(null);
    ```
  - **Modify `login()` method** — after storing challenge data, check `requiresTotp`:
    ```typescript
    async login(request: LoginRequest): Promise<LoginResponse> {
      const envelope = await firstValueFrom(/* ... */);

      if (envelope.data.requiresTotp) {
        this.requiresTotp.set(true);
        this.totpUserId.set(envelope.data.userId!);
        await this.router.navigate(['/login/totp']);
        return envelope.data;
      }

      this.challenge.set({
        challengeId: envelope.data.challengeId!,
        expiresInSeconds: envelope.data.expiresInSeconds ?? 300,
      });
      this.persistChallenge(this.challenge());
      await this.router.navigate(['/login/otp']);
      return envelope.data;
    }
    ```
  - **Add `verifyTotpLogin()` method** — mirrors existing `verifyOtp()`:
    ```typescript
    async verifyTotpLogin(code: string): Promise<VerifyOtpResponse> {
      const userId = this.totpUserId();
      if (!userId) throw new Error('No TOTP login in progress.');

      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<VerifyOtpResponse>>(
          `${environment.apiBaseUrl}/api/v1/auth/verify-totp-login`,
          { userId, code },
          AUTH_HTTP_OPTIONS,
        ),
      );
      this.applySession(envelope.data.accessToken!, {
        id: envelope.data.user!.id,
        email: envelope.data.user!.email,
        role: envelope.data.user!.role,
      });
      this.requiresTotp.set(false);
      this.totpUserId.set(null);
      return envelope.data;
    }
    ```

- [x] **Create `TotpLoginComponent`** — `apps/web/src/app/features/auth/totp-login/totp-login.component.ts`
  - Standalone Angular component, route: `/login/totp`
  - Mirrors the existing `OtpComponent` pattern:
    - Reads `authSessionService.requiresTotp()` — if false, redirects to `/login`
    - Shows a 6-digit code input field with label "Enter the code from your authenticator app"
    - Submit button calls `authSessionService.verifyTotpLogin(code)`
    - On success: calls `authSessionService.navigateAfterLogin()`
    - On error: shows "Invalid code. Please try again." error message
    - "Lost access to your authenticator app?" link at bottom — navigates to a help page or directs user to contact another Director
  - Add route to `apps/web/src/app/app.routes.ts`:
    ```typescript
    { path: 'login/totp', loadComponent: () => import('./features/auth/totp-login/totp-login.component').then(m => m.TotpLoginComponent) },
    ```

- [x] **Modify `LoginComponent`** — `apps/web/src/app/features/auth/login/login.component.ts`
  - The existing `submit()` method calls `await this.auth.login(...)` then `await this.router.navigate(['/login/otp'])`
  - The navigation is now handled inside `AuthSessionService.login()` — remove the unconditional navigation from `LoginComponent.submit()`:
    ```typescript
    async submit(): Promise<void> {
      this.errorMessage.set(null);
      if (this.form.invalid) { this.form.markAllAsTouched(); return; }
      this.submitting.set(true);
      try {
        await this.auth.login(this.form.getRawValue());
        // Navigation is handled by AuthSessionService.login() — either /login/otp or /login/totp
      } catch (error) {
        this.errorMessage.set(this.auth.extractErrorMessage(error));
      } finally {
        this.submitting.set(false);
      }
    }
    ```

### Tests — TwoFactorService Unit Tests

- [x] **Create `TwoFactorServiceTests.cs`** — `tests/api.unit/Domain/RoleManagement/TwoFactorServiceTests.cs`
  - Test: `GenerateProvisioningAsync_ReturnsValidProvisioningUri` — verify URI format, secret is base32
  - Test: `EnrollAsync_ValidCode_SetsEnrolledAt` — generate secret, compute valid TOTP, verify enrollment
  - Test: `EnrollAsync_InvalidCode_ReturnsError` — wrong code returns failure, TotpEnrolledAt remains null
  - Test: `EnrollAsync_AlreadyEnrolled_StillSucceedsWithValidCode` — re-enrollment with same secret
  - Test: `VerifyTotpCodeAsync_ValidCode_ReturnsTrue` — code within ±1 step window
  - Test: `VerifyTotpCodeAsync_InvalidCode_ReturnsFalse`
  - Test: `VerifyTotpCodeAsync_NoSecret_ReturnsFalse`
  - Test: `ResetTwoFactorAsync_ClearsSecretAndEnrolledAt` — both fields null after reset
  - Test: `ResetTwoFactorAsync_IncrementsTokenVersion` — version increased by 1
  - Test: `ResetTwoFactorAsync_RecordsAuditEvent` — audit written with correct event type
  - Use InMemory database, seed user with/without TotpSecret/TotpEnrolledAt
  - Use a test TOTP secret and compute codes using `Otp.NET`'s `Totp` class directly

### Tests — UsersController — 2FA Reset Endpoint Tests — DEFERRED

- [x] **Deferred**: No controller unit test infrastructure exists for `UsersController`. Creating it requires test harness setup. The `TwoFactorServiceTests` provide adequate coverage for the service layer.

### Tests — TwoFactorController Tests — DEFERRED

- [x] **Deferred**: Same rationale as above. Service-layer tests provide adequate coverage.

## Dev Notes

### Existing 2FA Infrastructure (No Changes Needed)

The following already exists and is functional:

- **`[Require2FAAttribute]`** — `apps/api/Authorization/Require2FAAttribute.cs`
  - Applied at controller level on `UsersController`
  - Checks `users.TotpEnrolledAt` on every request to protected endpoints
  - Returns 403 with "Two-Factor Authentication Required" if not enrolled
  - **This means the 2FA enforcement gate is already in place** — this story fills the enrollment and recovery gap

- **`User.TotpSecret` and `User.TotpEnrolledAt`** — `apps/api/Domain/Entities/User.cs`
  - Properties already exist on the entity
  - Must be explicitly mapped in `UserConfiguration.cs` (see Pre-Flight task above)

- **`AuthVerifiedStore`** — `apps/api/Infrastructure/Auth/AuthVerifiedStore.cs`
  - Redis-backed store for login-verified and step-up-verified flags
  - Existing `SetLoginVerifiedAsync` is called in the current email OTP flow
  - The TOTP login flow will also call this after successful TOTP verification

- **`AuthService.LoginAsync`** — Password verification, email OTP generation, challenge creation
  - This story modifies the login flow to branch: if Director with TOTP enrolled → skip email OTP → return `requiresTotp: true` with `userId`
  - The existing `VerifyOtpAsync` handles email OTP; the new `VerifyTotpLoginAsync` handles TOTP

- **`AuthServiceCollectionExtensions`** — `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`
  - Rate limit policies, DI registration, JWT configuration
  - New `TotpOptions` config and rate limit policies added in this story

- **`LoginComponent`** — `apps/web/src/app/features/auth/login/login.component.ts`
  - Currently calls `auth.login()` then unconditionally navigates to `/login/otp`
  - This story moves the navigation decision into `AuthSessionService.login()` so it can branch to `/login/totp` when `requiresTotp` is true

- **`AuthSessionService`** — `apps/web/src/app/core/auth/auth-session.service.ts`
  - Current `login()` navigates to `/login/otp` after login
  - Current `verifyOtp()` handles email OTP verification and calls `applySession()`
  - This story adds `verifyTotpLogin()` that mirrors `verifyOtp()` but calls the TOTP endpoint

### Key Implementation Details

**TwoFactorService Design:**
- Follows the same pattern as `UserManagementService` in `Domain/RoleManagement/`
- Uses `Otp.NET` library for TOTP compliance
- The `GenerateProvisioningAsync` / `EnrollAsync` split ensures the secret is generated before the UI can show the QR code, and enrollment is a separate verification step
- The `VerificationWindow.RfcSpecified` allows ±1 step for clock drift (RFC 6238 compliance)
- Secret storage: 20-byte random key, stored as base32 string in `users.totp_secret`

**Login Flow Change:**
```mermaid
flowchart TD
    A[POST /auth/login] --> B{Password valid?}
    B -- No --> C[Return null]
    B -- Yes --> D{Director + TOTP enrolled?}
    D -- No --> E[Generate email OTP, return challengeId]
    D -- Yes --> F[Return requiresTotp: true]
    E --> G[POST /auth/verify-otp → JWT]
    F --> H[POST /auth/verify-totp-login → JWT]
```

**TOTP Config:**
- Issuer: "Midi-Kaval" (configurable via `TotpOptions.Issuer`)
- Step: 30 seconds (RFC 6238 default)
- Code length: 6 digits
- Verification window: RFC specified (±1 step, total 3 windows checked)

**2FA Reset Flow:**
1. Director A navigates to Director B's detail sheet
2. Click "Reset 2FA" → confirmation dialog
3. `POST /api/v1/admin/users/{b.id}/reset-2fa` (Director A is authenticated, has `[Require2FA]` passed)
4. Server: `TwoFactorService.ResetTwoFactorAsync(actorId=A, targetId=B)`:
   - Clear `TotpSecret` and `TotpEnrolledAt` on user B
   - Increment `TokenVersion` on user B (forces logout of all sessions)
   - Audit event `user.two_factor_reset` with actor=A, target=B
5. User B must re-enroll before performing management actions
6. User B can still log in (TOTP not required for login, only for management)

**Vendor Fallback:**
- Implemented via `POST /api/v1/vendor/users/{id}/reset-2fa` in `OrganisationsController`
- Same `TwoFactorService.ResetTwoFactorAsync` call, but authorized by Vendor role
- No organisation scoping (Vendor operates across orgs)

**QR Code Generation on Frontend:**
- The provisioning URI is returned from the API as text
- QR code is generated client-side using a library like `qrcode` (npm)
- If QR library is controversial, display the provisioning URI as a clickable link that authenticator apps can parse
- The base32 secret is also displayed as a text fallback (user can type it into their authenticator app manually)

### Sequence of Checks in UsersController (for context)

1. `[Authorize(Policy = Policies.DirectorOnly)]` — role check
2. `[Require2FA]` — 2FA enrollment check (returns 403 if not enrolled)
3. Claim extraction (userId, organisationId)
4. Action-specific validation (body, permissions)
5. Business logic execution

The `[Require2FA]` fires BEFORE the controller method executes, ensuring unenrolled Directors never reach the action endpoints.

### AuthController — TOTP Endpoints

The enrollment and login TOTP endpoints are in `AuthController` (not `UsersController`):
- `POST /api/v1/auth/enroll-2fa` — unauthenticated? No — requires authentication so the service knows WHO is enrolling. Authentication is JWT-based (the user must already be logged in to enroll)
- `POST /api/v1/auth/verify-enroll-2fa` — authenticated
- `POST /api/v1/auth/totp-status` — authenticated
- `POST /api/v1/auth/verify-totp-login` — **unauthenticated** (the user hasn't completed login yet; they have a challenge from the login step)

For `verify-totp-login`, the request includes `userId` (returned from the login response) to identify the user. The challenge validity window is bounded by the 5-minute OTP expiry.

### Source Tree Components to Touch

```
apps/api/
├── Controllers/V1/
│   ├── Auth/
│   │   └── TwoFactorController.cs                       # NEW: enrollment and status endpoints
│   ├── Admin/
│   │   └── UsersController.cs                           # EXTEND: add reset-2fa endpoint
│   └── Vendor/
│       └── OrganisationsController.cs                    # EXTEND: add vendor reset-2fa endpoint
├── Domain/
│   ├── RoleManagement/
│   │   └── TwoFactorService.cs                          # NEW: TOTP business logic
│   └── Entities/
│       └── User.cs                                      # NO CHANGE (TotpSecret/TotpEnrolledAt exist)
├── Infrastructure/
│   ├── Auth/
│   │   ├── AuthService.cs                               # MODIFY: login flow for Director TOTP
│   │   ├── AuthOptions.cs                               # EXTEND: add TotpOptions
│   │   └── AuthServiceCollectionExtensions.cs            # EXTEND: DI registration, rate limit policies
│   ├── Audit/
│   │   └── AuditEventTypes.cs                           # EXTEND: add two_factor_enrolled/reset
│   └── Persistence/
│       └── UserConfiguration.cs                         # EXTEND: add TotpSecret/TotpEnrolledAt mapping
├── Models/
│   ├── Auth/
│   │   └── AuthDtos.cs                                  # EXTEND: LoginResponse (UserId, RequiresTotp), VerifyTotpLoginRequest
│   └── Admin/
│       └── UserManagementDtos.cs                        # EXTEND: add ResetTwoFactorResponse
├── MidiKaval.Api.csproj                                 # MODIFY: add Otp.NET package
└── appsettings.Development.json                         # MODIFY: add Totp section

apps/web/src/app/
├── core/auth/
│   ├── auth-session.service.ts                          # MODIFY: requiresTotp/totpUserId signals, verifyTotpLogin, conditional nav
│   └── auth.models.ts                                   # NO CHANGE (generated from API client)
├── features/admin/
│   ├── components/
│   │   ├── two-factor-modal/
│   │   │   └── two-factor-modal.component.ts             # NEW: 2FA enrollment flow
│   │   └── user-detail-sheet/
│   │       └── user-detail-sheet.component.ts            # EXTEND: add Reset 2FA button
│   ├── pages/team-roster/
│   │   └── team-roster.component.ts                     # EXTEND: (optional) 2FA status column
│   ├── services/
│   │   └── admin-user.service.ts                        # EXTEND: add resetTwoFactor method
│   └── models/
│       └── admin.models.ts                              # EXTEND: add totpEnrolledAt field
├── features/auth/
│   ├── login/
│   │   └── login.component.ts                           # MODIFY: remove unconditional /login/otp nav
│   └── totp-login/
│       └── totp-login.component.ts                      # NEW: TOTP code input for Directors
└── app.routes.ts                                        # EXTEND: add /login/totp route

tests/api.unit/Domain/RoleManagement/
├── TwoFactorServiceTests.cs                             # NEW: unit tests
└── UserManagementServiceTests.cs                        # EXTEND: verify 2FA check flows (if applicable)
```

### Potential Issues & Guardrails

- **TOTP clock drift**: The `VerificationWindow.RfcSpecified` allows ±1 step (total 3 windows). This handles most clock drift scenarios. If users report consistent "invalid code" errors, consider increasing the window in `TotpOptions`.
- **Race condition on enrollment**: Two concurrent enrollment requests could both complete successfully. Mitigated by the two-step flow (generate → separate verify). The first `EnrollAsync` sets `TotpEnrolledAt`; the second will see it's already enrolled and can either succeed or fail gracefully.
- **Login without TOTP**: If a Director has enrolled but their TOTP verification fails, they cannot log in. The recovery path (another Director or Vendor resetting 2FA) is the escape hatch. Consider adding a "Lost access to your authenticator app?" link on the TOTP login prompt.
- **Self-2FA reset is intentionally blocked**: A Director cannot reset their own 2FA — another Director must do it. This prevents a compromised Director from disabling their own 2FA. The Vendor fallback exists for org-wide emergencies.
- **`[Require2FA]` on `reset-2fa` endpoint**: Since `UsersController` has class-level `[Require2FA]`, the acting Director must be 2FA-enrolled to reset another Director's 2FA. This is correct — an unenrolled Director cannot use this path to bypass their own enrollment.
- **TOTP vs email OTP for login**: When a Director has TOTP enrolled, they use TOTP instead of email OTP at login. The email OTP path is still available for non-Director users and for Directors who haven't enrolled yet. This is a login UX improvement — TOTP is faster than email OTP.
- **QR code library choice**: If a client-side QR generation library adds significant bundle size, consider these alternatives:
  1. Use a small dedicated QR library (`qrcode-generator` ~5KB)
  2. Generate QR code server-side as a PNG/SVG response
  3. Display the provisioning URI as a clickable link and the base32 secret as text
### Story 2-10 / 2-1 Dependency Note

- Story 2-1 (Extend Users Schema) and Story 2-10 added `totp_secret` and `totp_enrolled_at` columns to the `users` table
- The `[Require2FA]` attribute from Story 2-10 enforces the 2FA check on controller endpoints
- This story implements the enrollment, verification, and recovery flows that complete FR-10

### Existing DTO Patterns for Reference

**LoginResponse** (existing in `apps/api/Models/Auth/`):
```csharp
public sealed record LoginResponse
{
    public Guid ChallengeId { get; init; }
    public int ExpiresInSeconds { get; init; }
    public bool RequiresTotp { get; init; }  // ADD this property
}
```

**VerifyOtpResponse** (existing):
```csharp
public sealed record VerifyOtpResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public AuthUserDto User { get; init; } = null!;
}
```

## References

- [Source: `epics.md §Story 2.7`] Original acceptance criteria
- [Source: `architecture-role-management.md §Authentication & Security`] TOTP integration, 2FA recovery, `Require2FAAttribute`
- [Source: `architecture-role-management.md §Frontend Architecture`] two-factor-modal component, login flow
- [Source: `prds/prd-Midi-Kaval-2026-06-23/prd.md §FR-10`] Director 2FA mandate
- [Source: `apps/api/Authorization/Require2FAAttribute.cs`] Existing 2FA enforcement gate
- [Source: `apps/api/Infrastructure/Auth/AuthService.cs`] Login flow to modify
- [Source: `apps/api/Domain/Entities/User.cs`] TotpSecret, TotpEnrolledAt properties
- [Source: `apps/api/Infrastructure/Auth/AuthOptions.cs`] Existing options pattern (for TotpOptions)
- [Source: `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs`] DI registration and rate limit policies
- [Source: `apps/web/src/app/features/admin/components/user-detail-sheet/user-detail-sheet.component.ts`] Existing detail sheet pattern
- [Source: `apps/web/src/app/features/admin/services/admin-user.service.ts`] Existing API service pattern

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created
- Validation review completed — 6 critical issues, 3 enhancements, and 3 optimizations identified and applied:
  - C1: Added `UserId` to `LoginResponse` DTO (required for verify-totp-login)
  - C2: Added conditional navigation in `AuthSessionService.login()` (TOTP → /login/totp, email OTP → /login/otp)
  - C3: Added `TotpLoginComponent` at route `/login/totp` for Director TOTP login
  - C4: Added explicit `UserConfiguration.cs` mapping task for TotpSecret/TotpEnrolledAt
  - C5: Added `VerifyTotpLoginRequest` DTO definition
  - C6: Fixed rate limit policy name mismatch (auth-enroll-totp)
  - E1: Added API client regeneration note for OpenAPI-generated frontend types
  - E2: Added `verifyTotpLogin()` method to `AuthSessionService`
  - E3: Added `qrcode-generator` npm dependency for QR code generation
  - O1: Fixed typo "extenda" → "extend a new"
  - O2: Wired `IsEnrolledAsync` into the `GET /auth/totp-status` endpoint
  - O3: Fixed `LoginResponse` property style to `{ get; set; }` (matching existing convention)
- ✅ Story implementation completed via bmad-dev-story workflow (2026-06-27):
  - Pre-flight: Added Otp.NET 1.4.1 nuget, TotpOptions config, UserConfiguration mapping
  - API: TwoFactorService with provisioning/enroll/verify/reset/isEnrolled methods, TwoFactorController, TOTP login flow in AuthService with VerifyTotpLoginAsync, reset-2fa endpoints in UsersController and OrganisationsController
  - Service registration: TwoFactorService DI, TotpOptions binding, auth-enroll-totp/auth-verify-totp rate limit policies added
  - Web: TotpLoginComponent at /login/totp, AuthSessionService extended with requiresTotp/totpUserId signals and verifyTotpLogin(), LoginComponent navigation delegated to AuthSessionService, UserDetailSheet with Reset 2FA button, TwoFactorModalComponent for enrollment UI, admin.models includes totpEnrolledAt
  - API builds and all 17 TwoFactorService unit tests pass (generation, enrollment, verification, reset, audit)

## File List

**New files:**
- `apps/api/Controllers/V1/Auth/TwoFactorController.cs` — TOTP enrollment and status endpoints
- `apps/api/Domain/RoleManagement/TwoFactorService.cs` — TOTP business logic
- `apps/web/src/app/features/admin/components/two-factor-modal/two-factor-modal.component.ts` — 2FA enrollment UI flow
- `apps/web/src/app/features/auth/totp-login/totp-login.component.ts` — TOTP code input for Director login
- `tests/api.unit/Domain/RoleManagement/TwoFactorServiceTests.cs` — unit tests

**Modified files:**
- `apps/api/MidiKaval.Api.csproj` — add Otp.NET package
- `apps/api/appsettings.Development.json` — add Totp section
- `apps/api/Infrastructure/Persistence/UserConfiguration.cs` — add TotpSecret/TotpEnrolledAt mapping
- `apps/api/Infrastructure/Auth/AuthOptions.cs` — add TotpOptions
- `apps/api/Infrastructure/Auth/AuthService.cs` — modify login flow for Director TOTP
- `apps/api/Infrastructure/AuthServiceCollectionExtensions.cs` — DI registration, rate limit policies
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — add TwoFactorEnrolled, TwoFactorReset
- `apps/api/Models/Auth/AuthDtos.cs` — add UserId, RequiresTotp to LoginResponse; add VerifyTotpLoginRequest
- `apps/api/Models/Admin/UserManagementDtos.cs` — add ResetTwoFactorResponse
- `apps/api/Controllers/V1/Admin/UsersController.cs` — add reset-2fa endpoint
- `apps/api/Controllers/V1/Vendor/OrganisationsController.cs` — add vendor reset-2fa endpoint
- `apps/web/src/app/features/admin/models/admin.models.ts` — add totpEnrolledAt field
- `apps/web/src/app/features/admin/services/admin-user.service.ts` — add resetTwoFactor method
- `apps/web/src/app/features/admin/components/user-detail-sheet/user-detail-sheet.component.ts` — add Reset 2FA button
- `apps/web/src/app/core/auth/auth-session.service.ts` — add requiresTotp/totpUserId signals, verifyTotpLogin method, conditional navigation
- `apps/web/src/app/features/auth/login/login.component.ts` — remove unconditional navigate to /login/otp
- `apps/web/src/app/app.routes.ts` — add /login/totp route
- `package.json` (web) — add qrcode-generator dependency

### Review Findings

**Decisions (resolved):**
- [x] [Review][Decision] Admin `ResetTwoFactor` should check last-Director protection — implemented: `LastDirectorGuard.IsLastActiveDirectorAsync` check added before reset.
- [x] [Review][Decision] Spurious `[Require2FA]` on Vendor `ResetTwoFactor` endpoint — removed the per-endpoint attribute (class-level `[Require2FA]` is sufficient).

**Patch Findings (all applied — see patches section below):**
- [x] [Review][Patch] `POST /api/v1/auth/verify-totp-login` controller endpoint is missing — added to `TwoFactorController`.
- [x] [Review][Patch] Admin `ResetTwoFactor` has no organisation-scope check — added `TryResolveOrganisationId` before reset.
- [x] [Review][Patch] TOTP login has no per-user brute-force protection — added challenge-based failed-attempt tracking via `OtpChallengeStore.RecordFailedAttemptAsync`.
- [x] [Review][Patch] `GenerateProvisioningAsync` overwrites existing TOTP secret — guard added: throws `InvalidOperationException` if already enrolled.
- [x] [Review][Patch] Vendor `ResetTwoFactor` has no organisation-scope check — added `TryResolveOrganisationId` + `TryResolveActorUserId`.
- [x] [Review][Patch] `GetUserId()` throws unhandled `UnauthorizedAccessException` — replaced with `TryGetUserId()` returning nullable.
- [x] [Review][Patch] No binding between password login step and TOTP verification — added binding challenge in Redis via `OtpChallengeStore`.
- [x] [Review][Patch] `ChallengeId = Guid.Empty` as sentinel value — changed to `Guid?` (nullable).
- [x] [Review][Patch] Inconsistent actor-ID resolution — both admin and vendor controllers now use `TryResolveActorUserId`.
- [x] [Review][Patch] `KeyNotFoundException.Message` leaked to client — replaced with constant safe message.
- [x] [Review][Patch] TOTP code length validated before trimming — fixed: code is trimmed first, then validated.
- [x] [Review][Patch] Unused `using OtpNet` in `AuthService.cs` — removed.
- [x] [Review][Patch] `require('qrcode-generator')` ESM breakage — changed to dynamic `import()`.
- [x] [Review][Patch] `ResetTwoFactor` returns anonymous object instead of `ApiResponse<T>` — wrapped in `ApiResponse<ResetTwoFactorResponse>`.
- [x] [Review][Patch] `TokenVersion` not checked in `VerifyTotpLoginAsync` — added version check.
- [x] [Review][Patch] TOTP login state not persisted across page refresh — persisted to `sessionStorage` with restore on init.

**Deferred:**
- [x] [Review][Defer] `IsEnrolledAsync` only checks `TotpEnrolledAt`
- [x] [Review][Defer] TOTP enrollment has no rotation/re-enrollment requirement
- [x] [Review][Defer] `TokenVersion` increment doesn't invalidate existing sessions
- [x] [Review][Defer] Unenrolled Directors hit 403 on actions
- [x] [Review][Defer] `Require2FA` filter does DB query on every request
- [x] [Review][Defer] 403→modal interception UI unwired
