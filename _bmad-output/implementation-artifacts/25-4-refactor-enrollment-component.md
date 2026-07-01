---
baseline_commit: d83f8e3f0e66844e029a24bb022b9c1e73b2366d
---

# Story 25.4: Refactor Enrollment Component — Shared 2FA Component, Backup Code Display, TwoFactorSetupGuard, Route-Based Enrollment

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: review

## Story

As a **user of any role (Vendor, Director, Coordinator, field worker)**,
I want **a shared, role-agnostic 2FA enrollment component with backup code display and a route guard for unenrolled users**,
so that **I can enroll in 2FA through a dedicated settings route, see my backup codes after enrollment, and be redirected to setup if my organisation requires 2FA.**

## Acceptance Criteria

1. **Create `TwoFactorEnrollmentComponent`** at `shared/components/2fa/two-factor-enrollment.component.ts` as a standalone, role-agnostic component. The existing `TwoFactorModalComponent` at `features/admin/components/two-factor-modal/` is unused (no references outside its own file) and is removed. All enrollment flows go through the new shared component.

2. **`TwoFactorEnrollmentComponent`** supports two modes:
   - **Dialog mode** (default): rendered inside a `MatDialog` — injects `MatDialogRef`, closes with `dialogRef.close(true)` on success (existing behavior preserved)
   - **Page mode**: rendered inline in a route — accepts a `pageMode` input (`@Input({ transform: booleanAttribute }) pageMode = false`), does NOT close on success, shows a "Go to Dashboard" button instead
   - Both modes share the same 5-step flow: `initiate` → `qr-display` → `verify` → `backup-codes` → `success`

3. **Step flow updated:**
   - `initiate` — "Set Up Two-Factor Authentication" button → `POST /api/v1/auth/enroll-2fa` (unchanged)
   - `qr-display` — QR code (200x200px, theme-aware via `prefers-color-scheme` media query on the `.qr-code` class) + secret fallback + "Copy Key" button for the Base32 secret (copies via `navigator.clipboard.writeText`) + "I've scanned the code" button (unchanged)
   - `verify` — 6-digit TOTP input with auto-submit on 6th character (UX-DR2) + "Verify & Activate" button → `POST /api/v1/auth/verify-enroll-2fa`
   - `backup-codes` — NEW step: calls `GET /api/v1/auth/2fa-status` or directly invokes backup code generation. Displays 8 backup codes in a 2-column monospace chip grid (UX-DR3). "Download as .txt" button with file header containing user email (UX-DR4). "I've saved my backup codes" button to proceed.
   - `success` — Confirmation message, auto-closes dialog after 1.5s in dialog mode; shows "Enrollment complete" with "Go to Dashboard" button in page mode

4. **Backup code generation integrated**:
   - After successful TOTP verification, call `POST /api/v1/auth/generate-backup-codes` (new endpoint added to `TwoFactorController` in this story) — returns `ApiEnvelope<{ codes: string[] }>` with 8 plaintext backup codes
   - Store returned plaintext codes in component state (displayed exactly once)
   - The 8 codes are rendered as `<code>` elements in a 2-column CSS grid with monospace font
   - "Download as .txt" exports a text file with header: `Kaval Online Backup Codes — user@email.com`, then one code per line

5. **`TwoFactorSetupGuard`** created at `core/auth/two-factor-setup.guard.ts` (standalone `CanActivateFn`):
   - Reads `requires2faSetup` signal from `AuthSessionService` (set after login response)
   - If `true` AND the current route is NOT `/settings/2fa`, redirects to the `setupUrl` stored in the auth session
   - If `false` or already on the setup route, allows navigation
   - Accesses current route via `inject(Router).url` to exclude the setup route from redirect (prevents infinite loop)

6. **`AuthSessionService`** gains:
   - `readonly requires2faSetup = signal(false)` — new signal (set during `login()` from envelope data)
   - `readonly setupUrl = signal<string | null>(null)` — stores the role-aware URL from login response
   - `readonly orgRequires2fa = signal(false)` — stores org mandate flag
   - `login()` method updated to read `requires2faSetup`, `setupUrl`, `orgRequires2fa` from the login response envelope (alongside existing `requiresTotp` check)
   - `clearSession()` resets these to defaults: `requires2faSetup.set(false)`, `setupUrl.set(null)`, `orgRequires2fa.set(false)`
   - `navigateAfterLogin()` updated to check `requires2faSetup` first: if `true`, redirects to `setupUrl` before any role-based routing. This ensures unenrolled users land on the setup page after OTP/TOTP verification, rather than flash-redirecting from their default route.

7. **Route `/settings/2fa`** added to `app.routes.ts`:
   - Path: `settings/2fa`
   - Uses `authGuard` only (NOT `twoFactorSetupGuard` — the guard would cause a redirect loop since this IS the setup route)
   - Loads `TwoFactorEnrollmentComponent` in page mode
   - The `navigateAfterLogin()` method on `AuthSessionService` handles the redirect to this route for unenrolled users

8. **Updated `auth.models.ts`** — `LoginResponse` type gains optional fields `requires2faSetup`, `setupUrl`, `orgRequires2fa` (matching API contract from Story 25-3).

9. **Angular API service layer** created at `shared/services/two-factor.service.ts` and `shared/services/backup-code.service.ts`:
   - `TwoFactorService` — wraps `POST /auth/enroll-2fa`, `POST /auth/verify-enroll-2fa`, `GET /auth/2fa-status`
   - `BackupCodeService` — wraps `POST /auth/generate-backup-codes`, `POST /auth/verify-backup-code`
   - Follows existing `VendorApiService` pattern: injects `HttpClient`, returns promises via `firstValueFrom`
   - `TwoFactorEnrollmentComponent` consumes these services instead of making inline `HttpClient` calls

10. **`navigateAfterLogin()`** updated on `AuthSessionService` (see AC 6): checks `requires2faSetup` first and redirects to setup URL before any role-based routing.

## Tasks / Subtasks

- [x] Create `shared/components/2fa/two-factor-enrollment.component.ts` with `TwoFactorEnrollmentComponent` (AC: 1, 2, 3)
  - [x] Support dialog/page modes via `pageMode` input
  - [x] 5-step flow: initiate → qr-display → verify → backup-codes → success
  - [x] Theme-aware QR code (light/dark via `prefers-color-scheme`)
  - [x] "Copy Key" button for Base32 secret
  - [x] 6-digit TOTP auto-submit on 6th character
  - [x] Backup codes grid (2-column monospace chips) + download .txt (AC: 4)
  - [x] Page mode: role-aware "Go to Dashboard" button on success (uses `navigateAfterLogin()`)
- [x] Create `shared/components/2fa/two-factor-enrollment.component.html` (template)
- [x] Create `shared/components/2fa/two-factor-enrollment.component.scss` (styles)
- [x] Create `shared/components/2fa/backup-codes-display.component.ts` — separate reusable component for backup code display (AC: 4)
  - [x] 2-column monospace chip grid
  - [x] "Download as .txt" button with file header `Kaval Online Backup Codes — user@email.com`
  - [x] Input: `codes: string[]`, `email: string`
  - [x] Emits `confirmed` event when user clicks "I've saved my codes"
- [x] Create `shared/services/two-factor.service.ts` — wraps enroll-2fa, verify-enroll-2fa, 2fa-status (AC: 9)
- [x] Create `shared/services/backup-code.service.ts` — wraps generate-backup-codes, verify-backup-code (AC: 9)
- [x] Create `core/auth/two-factor-setup.guard.ts` with `CanActivateFn` (AC: 5)
  - [x] Exclude `/settings/2fa` route from redirect to prevent infinite loop
- [x] Update `AuthSessionService` — add signals + read from login response (AC: 6)
  - [x] `clearSession()` resets requires2faSetup, setupUrl, orgRequires2fa
  - [x] `navigateAfterLogin()` checks requires2faSetup first, redirects to setupUrl
- [x] Add `POST /auth/generate-backup-codes` endpoint to `TwoFactorController` (AC: 4, API)
  - [x] Calls `BackupCodeService.GenerateAsync`, returns `{ codes: string[] }`
  - [x] Requires `[Authorize]`, rate limited with `data-write` policy
- [x] Add `settings/2fa` route to `app.routes.ts` with authGuard only (no twoFactorSetupGuard — avoids redirect loop) (AC: 7)
- [x] Update `auth.models.ts` LoginResponse type with new fields — OR document that Record<,> indexing pattern handles them (AC: 8)
- [x] Remove existing `TwoFactorModalComponent` (unused — zero references outside its own file) (AC: 1)

## Dev Notes

### Architecture Compliance

**This story implements:**
- FR-1.3 (Enrollment flow shared across roles) — AC 1, 2
- FR-4.4 (Backup codes display once on enrollment success) — AC 3, 4
- FR-5.1/5.2 (requires2faSetup signal from login response drives guard) — AC 5, 6
- FR-5.3 (Angular client uses setupUrl from response) — AC 5, 6
- FR-6 (API Endpoints) — `POST /auth/generate-backup-codes` added
- UX-DR1 (Theme-aware QR) — AC 3
- UX-DR2 (TOTP auto-submit) — AC 3
- UX-DR3 (Backup codes 2-column monospace chip grid) — AC 4
- UX-DR4 (Download .txt with user email header) — AC 4
- UX-DR10 (QR alt text, aria-labels) — AC 3
- UX-DR11 (Reduced motion) — AC 3
- UX-DR12 (Touch targets 48x48px) — AC 3

**Does NOT implement (deferred):**
- FR-1 (Full Vendor Settings Page with password change) — Story 25-5
- FR-2.1 (2FA column in Staff Management) — Story 25-6
- FR-2.4 (Org settings toggles) — Story 25-7
- FR-2.6 (2FA Audit Log) — Story 25-7
- FR-3 (Onboarding Emails) — Story 25-8
- FR-4.6 (Warning banner when < 3 backup codes remain) — Story 25-5

### Source Tree Components to Touch

**New files:**
```
apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.ts
apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.html
apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.scss
apps/web/src/app/shared/components/2fa/backup-codes-display.component.ts
apps/web/src/app/shared/services/two-factor.service.ts
apps/web/src/app/shared/services/backup-code.service.ts
apps/web/src/app/core/auth/two-factor-setup.guard.ts
```

**Modified files:**
```
apps/web/src/app/core/auth/auth-session.service.ts          # MODIFY: + 3 signals, update login(), clearSession(), navigateAfterLogin()
apps/web/src/app/core/auth/auth.models.ts                   # MODIFY: + fields to LoginResponse type
apps/web/src/app/app.routes.ts                              # MODIFY: + /settings/2fa route
apps/web/src/app/features/admin/components/two-factor-modal/
  two-factor-modal.component.ts                             # DELETE: unused (zero references)
apps/api/Controllers/V1/Auth/TwoFactorController.cs          # MODIFY: + POST /auth/generate-backup-codes endpoint
```

### Existing Patterns & Conventions (MUST FOLLOW)

**Standalone component pattern (from `TwoFactorModalComponent`):**
- `imports` array with `MatButtonModule`, `MatDialogModule`, `MatFormFieldModule`, `MatIconModule`, `MatInputModule`, `FormsModule`, `MatProgressSpinnerModule`
- `signal()` for reactive state
- `inject()` for DI (no constructor injection)
- `HttpClient` via `private readonly http = inject(HttpClient)`
- API calls via `firstValueFrom(this.http.post(...))`
- Error handling: `error instanceof HttpErrorResponse ? error.error?.detail ?? 'Fallback message' : 'Fallback message'`

**Auth guard pattern (from `auth.guard.ts`):**
- `CanActivateFn` function
- `inject(AuthSessionService)` and `inject(Router)`
- `return router.createUrlTree(['/path'])` for redirect
- `return true` to allow

**AuthSessionService login() method changes:**
The `login()` method currently reads `requiresTotp`, `userId`, `tokenVersion`, `totpChallengeId` from the envelope data using `as Record<string, unknown>` indexing. Add reads for the new fields:

```typescript
this.requires2faSetup.set((loginData['requires2faSetup'] as boolean) ?? false);
this.setupUrl.set((loginData['setupUrl'] as string) ?? null);
this.orgRequires2fa.set((loginData['orgRequires2fa'] as boolean) ?? false);
```

**Auth session route-based component loading:**
For the `/settings/2fa` route, load the component lazily with `authGuard` only (NOT `twoFactorSetupGuard` — that guard would cause a redirect loop since this IS the setup route):

```typescript
{
  path: 'settings/2fa',
  canActivate: [authGuard],
  loadComponent: () =>
    import('./shared/components/2fa/two-factor-enrollment.component').then(
      (m) => m.TwoFactorEnrollmentComponent,
    ),
  data: { pageMode: true },
}
```

Note: Use `data` to pass `pageMode = true` since route inputs aren't available. The component reads `pageMode` via `@Inject(ActivatedRoute)` or via `inject(ActivatedRoute).snapshot.data['pageMode']`.

### `auth.models.ts` LoginResponse Type

The `LoginResponse` type is auto-generated from the OpenAPI client (`components['schemas']['LoginResponse']`). If Story 25-3's API backend has been rebuilt and the OpenAPI client regenerated, the type already includes the new fields. If not yet regenerated, the `AuthSessionService.login()` method already accesses envelope data via `as Record<string, unknown>` indexing (line 83), so no type changes are strictly needed — the new fields are read at runtime regardless of the TypeScript type.

The developer should:
1. First try adding the fields to the type (if OpenAPI client can be regenerated)
2. Fall back to the existing `Record<string, unknown>` casting pattern — it works correctly at runtime

### Guard Route Exclusion Logic

The `TwoFactorSetupGuard` must exclude the `/settings/2fa` route from redirect:

```typescript
export const twoFactorSetupGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionService);
  const router = inject(Router);

  if (!auth.requires2faSetup()) {
    return true; // enrolled — allow
  }

  // Prevent infinite loop on the setup route itself
  if (router.url.startsWith('/settings/2fa')) {
    return true;
  }

  return router.createUrlTree([auth.setupUrl() ?? '/settings/2fa']);
};
```

### Theme-Aware QR Code

- Use CSS media query `@media (prefers-color-scheme: dark)` on the `.qr-code` class:
  ```scss
  .qr-code {
    width: 200px;
    height: 200px;
    border-radius: 8px;
    border: 1px solid var(--border-color, #E2E5EB);
    filter: drop-shadow(0 1px 2px rgba(0,0,0,0.1));
  }
  ```
- The existing `qrcode-generator` library renders a black-on-white QR — in dark mode, invert via CSS filter or ensure sufficient contrast with a white-boxed container

### TOTP Auto-Submit

- Add `(input)` handler on the verification code input:
  ```html
  <input
    matInput
    type="text"
    inputmode="numeric"
    maxlength="6"
    placeholder="000000"
    [(ngModel)]="verificationCode"
    (input)="onCodeInput($event)"
  />
  ```
- `onCodeInput(event)` checks if `this.verificationCode().length === 6` and auto-triggers `verifyEnrollment()`
- Button remains as fallback for accessibility

### Backup Code Display

- `BackupCodesDisplayComponent` is a separate, reusable standalone component:
  ```typescript
  @Component({
    selector: 'app-backup-codes-display',
    standalone: true,
    imports: [MatButtonModule, MatIconModule],
    template: `...`
  })
  export class BackupCodesDisplayComponent {
    codes = input<string[]>([]);
    email = input<string>('');
    readonly saved = output<void>();
    // ...
  }
  ```
- Layout: CSS grid with 2 columns, each cell a monospace `<code>` element
- Download: creates a `Blob` with `text/plain` content and triggers `<a download>` click
- Backup code format (from architecture): `A3K9-X7M2-P1` (10 chars, dashed groups)

### Angular API Service Layer

Create two service files following the existing `VendorApiService` pattern:

```typescript
// shared/services/two-factor.service.ts
@Injectable({ providedIn: 'root' })
export class TwoFactorService {
  private readonly http = inject(HttpClient);

  enroll(): Promise<{ provisioningUri: string; secretBase32: string }> {
    return firstValueFrom(this.http.post<{ provisioningUri: string; secretBase32: string }>(
      `${environment.apiBaseUrl}/api/v1/auth/enroll-2fa`, {},
    ));
  }

  verifyEnroll(code: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(
      `${environment.apiBaseUrl}/api/v1/auth/verify-enroll-2fa`,
      { code },
    ));
  }
}
```

```typescript
// shared/services/backup-code.service.ts
@Injectable({ providedIn: 'root' })
export class BackupCodeService {
  private readonly http = inject(HttpClient);

  generate(): Promise<{ codes: string[] }> {
    return firstValueFrom(this.http.post<{ codes: string[] }>(
      `${environment.apiBaseUrl}/api/v1/auth/generate-backup-codes`, {},
    ));
  }
}
```

### `navigateAfterLogin()` Changes

The method must check `requires2faSetup` before role-based routing:

```typescript
navigateAfterLogin(): void {
  if (this.requires2faSetup()) {
    const url = this.setupUrl();
    if (url) {
      void this.router.navigate([url]);
      return;
    }
  }
  // ... existing role-based routing unchanged ...
}
```

### `clearSession()` Changes

```typescript
clearSession(): void {
  // ... existing resets ...
  this.requires2faSetup.set(false);
  this.setupUrl.set(null);
  this.orgRequires2fa.set(false);
}
```

### Testing Standards

- Unit test: `TwoFactorEnrollmentComponent` in page mode shows role-aware "Go to Dashboard" on success, does NOT close
- Unit test: Backup codes displayed in 2-column grid, download produces .txt with email header
- Unit test: `TwoFactorSetupGuard` redirects when `requires2faSetup = true` (and not on setup route), allows when `false`
- Unit test: `AuthSessionService.login()` populates `requires2faSetup` signal from envelope
- Unit test: `AuthSessionService.navigateAfterLogin()` redirects to setupUrl when requires2faSetup is true
- Unit test: `AuthSessionService.clearSession()` resets requires2faSetup, setupUrl, orgRequires2fa
- Unit test: TOTP auto-submit fires verifyEnrollment at 6 chars
- Unit test: `POST /auth/generate-backup-codes` returns 8 codes with correct format

## Project Structure Notes

- New `shared/components/2fa/` follows the project convention for shared/reusable components
- New `shared/services/` follows the project convention for shared API services
- The backup codes display is extracted into its own component for reuse in later stories (Story 25-5 Vendor Settings will also display backup codes)
- The guard lives in `core/auth/` alongside `auth.guard.ts`, `vendor.guard.ts`, `director.guard.ts`
- Route at `/settings/2fa` is a top-level route (not nested under `/admin` or `/vendor`) — it's role-agnostic and any authenticated user can access it
- The old `TwoFactorModalComponent` is removed — it was unused (zero references outside its own file)
- `POST /auth/generate-backup-codes` endpoint is added to the API's `TwoFactorController` to support the backup-codes step in enrollment flow

## References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 4: "Refactor enrollment component"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "Frontend Architecture" (component reuse, Pin-driven role config), "Implementation Patterns", "File Changes — Web"
- **UX Design:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/DESIGN.md` — Design tokens (QR container 8px radius, badge colors), backup code format
- **UX Experience:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/EXPERIENCE.md` — Microcopy, voice rules
- **Existing component:** `apps/web/src/app/features/admin/components/two-factor-modal/two-factor-modal.component.ts` — current 4-step modal
- **Existing auth session:** `apps/web/src/app/core/auth/auth-session.service.ts` — signals, login flow
- **Existing auth models:** `apps/web/src/app/core/auth/auth.models.ts` — types, login response
- **Existing routes:** `apps/web/src/app/app.routes.ts` — route patterns, guards
- **Existing guards:** `apps/web/src/app/core/auth/auth.guard.ts` — CanActivateFn pattern
- **Existing vendor API:** `apps/web/src/app/features/vendor/vendor-api.service.ts` — API call pattern
- **Story 25-3 (prerequisite):** `_bmad-output/implementation-artifacts/25-3-login-response-contract.md` — login response fields consumed by guard
- **API contract (LoginResponse fields):** `apps/api/Models/Auth/AuthDtos.cs` — Requires2faSetup, SetupUrl, OrgRequires2fa

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List
- Added `POST /auth/generate-backup-codes` endpoint to `TwoFactorController` (API)
- Created `TwoFactorEnrollmentComponent` — shared component supporting dialog/page modes with 5-step flow
- Created `BackupCodesDisplayComponent` — reusable backup code display with download .txt and 2-column grid
- Created `TwoFactorService` and `BackupCodeService` Angular API service layer
- Created `TwoFactorSetupGuard` with route exclusion for `/settings/2fa`
- Updated `AuthSessionService`: added `requires2faSetup`, `setupUrl`, `orgRequires2fa` signals; updated `login()`, `clearSession()`, `navigateAfterLogin()`
- Added comment documenting that new LoginResponse fields are accessed via Record pattern at runtime
- Added `/settings/2fa` route to `app.routes.ts` with `authGuard` only
- Removed unused `TwoFactorModalComponent` (zero references outside own file)
- Build: API 0 errors, Web 0 errors (all warnings pre-existing)

### File List
- `apps/api/Controllers/V1/Auth/TwoFactorController.cs` — MODIFIED: + POST /auth/generate-backup-codes endpoint
- `apps/web/src/app/core/auth/auth-session.service.ts` — MODIFIED: +3 signals, updated login/clearSession/navigateAfterLogin
- `apps/web/src/app/core/auth/auth.models.ts` — MODIFIED: added comment about Record pattern for new fields
- `apps/web/src/app/app.routes.ts` — MODIFIED: + /settings/2fa route
- `apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.ts` — NEW
- `apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.html` — NEW
- `apps/web/src/app/shared/components/2fa/two-factor-enrollment.component.scss` — NEW
- `apps/web/src/app/shared/components/2fa/backup-codes-display.component.ts` — NEW
- `apps/web/src/app/shared/services/two-factor.service.ts` — NEW
- `apps/web/src/app/shared/services/backup-code.service.ts` — NEW
- `apps/web/src/app/core/auth/two-factor-setup.guard.ts` — NEW
- `apps/web/src/app/features/admin/components/two-factor-modal/two-factor-modal.component.ts` — DELETED

### Review Findings

#### patch
- [x] [Review][Patch] Page mode InputSignal overwritten in constructor — `two-factor-enrollment.component.ts:54-59` assigns `true` to the read-only InputSignal, replacing it with a plain boolean. Calling `this.pageMode()` throws `TypeError: pageMode is not a function`. Fix: use a separate writable signal for route-driven page mode.
- [x] [Review][Patch] Guard never wired to routes — `two-factor-setup.guard.ts` is defined but never imported or applied in `app.routes.ts`. The redirect-to-setup logic is dead code. Fix: import and add `twoFactorSetupGuard` to appropriate route `canActivate` arrays.
- [x] [Review][Patch] Infinite redirect after successful enrollment — `requires2faSetup` is never set to `false` after TOTP enrollment completes. `navigateAfterLogin()` redirects right back to setup. Fix: call `authSession.requires2faSetup.set(false)` on successful enrollment completion.
- [x] [Review][Patch] `GenerateBackupCodes` doesn't verify TOTP enrollment — `TwoFactorController.cs:149` generates backup codes for any authenticated user regardless of enrollment status. Fix: check `user.TotpEnrolledAt is not null` before generating.
- [x] [Review][Patch] 2FA setup signals not persisted across page reload — `requires2faSetup`, `setupUrl`, `orgRequires2fa` are in-memory only with no `sessionStorage` backup. Page refresh silently forgets the pending enrollment requirement. Fix: persist to sessionStorage in `login()` and restore in `bootstrapSession()`.
- [x] [Review][Patch] `navigateAfterLogin()` doesn't fall back to `/settings/2fa` when `setupUrl` is null — `auth-session.service.ts:277-282` silently falls through to role-based routing if `setupUrl()` returns null. Inconsistent with the guard which falls back to `'/settings/2fa'`. Fix: if `requires2faSetup()` is true but `setupUrl()` is null, redirect to `'/settings/2fa'`.
- [x] [Review][Patch] Empty 401 body on `GenerateBackupCodes` — `TwoFactorController.cs:148` returns `Unauthorized()` with no ProblemDetails body, contradicting the `[ProducesResponseType]` attribute. Fix: return `Unauthorized(new ProblemDetails { ... })`.
- [x] [Review][Patch] No audit event on backup code generation — `TwoFactorController.cs:154` generates codes but never records an audit event. Security monitoring cannot trace when/by whom codes were generated. Fix: add audit event recording in `BackupCodeService.GenerateAsync`.

#### defer
- [x] [Review][Defer] Vendor route `/vendor/settings` doesn't exist — handled by Story 25-5 (Vendor Settings Page).
- [x] [Review][Defer] Vendor 2FA reset doesn't revoke backup codes — pre-existing issue in vendor controller, not introduced by this story.
- [x] [Review][Defer] `orgRequires2fa` signal never consumed — exists for downstream stories (25-5, 25-7) to consume.
- [x] [Review][Defer] Download .txt error handling / copy button / user-select:all — nice-to-have UX enhancements, not in ACs.
- [x] [Review][Defer] No `canDeactivate` guard on `/settings/2fa` — out of scope, not in ACs.
- [x] [Review][Defer] No idempotency/quota on code generation — architectural concern, not in story scope.
- [x] [Review][Defer] Plaintext codes in response/logs — intentional one-time display design.
