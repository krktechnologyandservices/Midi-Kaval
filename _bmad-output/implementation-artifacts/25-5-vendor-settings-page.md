# Story 25.5: Vendor Settings Page — 2FA Enrollment Card, Backup Codes Display, Low-Codes Warning Banner, Guard Redirect

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: ready-for-dev

## Story

As a **Vendor user**,
I want **a dedicated `/vendor/settings` page where I can enroll in 2FA, view my backup codes, see a warning when I have few remaining, and change my password**,
so that **I can secure my account and vendor features are gated behind 2FA enrollment.**

## Acceptance Criteria

1. **Route `/vendor/settings`** added to `app.routes.ts` as a standalone top-level route:
   - Uses `authGuard` + `vendorGuard` + `twoFactorSetupGuard` (the guard excludes `/vendor/settings` from redirect, preventing a loop)
   - Loads `VendorSettingsComponent`
   - Appears as a "Settings" navigation link in the vendor page header alongside "Dashboard" and "Organisations"

2. **`VendorSettingsComponent`** created at `features/vendor/settings/vendor-settings.component.ts` as a standalone Angular Material component with two sections stacked vertically:

3. **Section 1 — 2FA Enrollment Status & Card** (top of page, most prominent):
   - If NOT enrolled (`enrolled === false`):
     - Displays a dismissible info banner: "Two-factor authentication is required to access vendor features." (with close button, `aria-label="Dismiss banner"`)
     - Embeds the shared `TwoFactorEnrollmentComponent` in page mode (`data: { pageMode: true }` or via route data)
     - The enrollment component renders its full 5-step flow: initiate → QR → verify → backup codes → success
   - If enrolled (`enrolled === true`):
     - Displays a 2FA status badge: green checkmark + "Two-Factor Authentication: Enabled" + enrollment date
     - Shows remaining backup code count (NOT the codes themselves — per FR-4.4, codes are displayed exactly once on enrollment and cannot be recovered from the SHA-256 hashed DB storage)
     - If remaining backup codes < 3, shows a dismissible warning banner: "You have [N] backup code(s) remaining. Consider re-enrolling 2FA." (UX-DR14, FR-4.6). Dismissible via × button, persists dismissal for the current browser session via `sessionStorage` key `midi_kaval_backup_warning_dismissed`
     - Shows "Re-enroll" button that re-opens `TwoFactorEnrollmentComponent` in dialog mode
     - Shows "Regenerate Backup Codes" button that calls `POST /auth/backup-codes/regenerate` and shows new codes in a dialog exactly once

4. **Section 2 — Change Password** (bottom of page):
   - "Update Password" button opens a `MatDialog` with current password + new password + confirm password fields
   - Calls `POST /auth/change-password` (new endpoint added in this story) — requires `[Authorize]`
   - On success: `MatSnackBar` "Password updated successfully."

6. **Vendor redirect guard integration**:
   - The existing `navigateAfterLogin()` method on `AuthSessionService` (updated in Story 25-4) redirects `requires2faSetup = true` Vendors to `/vendor/settings`
   - `twoFactorSetupGuard` is applied to BOTH the `/vendor` and `/vendor/settings` routes. The guard already excludes `/vendor/settings` from redirect (prevents loop), so it allows navigation to the settings route while still redirecting unenrolled users away from `/vendor` and other vendor sub-routes

7. **Backup code count API**:
   - `GET /auth/backup-codes/remaining` endpoint (new on `TwoFactorController` or a dedicated controller) returns `{ remaining: number }`
   - Calls `BackupCodeService.GetRemainingCountAsync`
   - Requires `[Authorize]`, rate limited with `"data-read"` policy

8. **Backup code re-download API**:
   - `POST /auth/backup-codes/regenerate` endpoint (new on `TwoFactorController`) — regenerates backup codes, invalidates all existing unused codes, returns new codes
   - Calls `BackupCodeService.RevokeAllAsync(userId, ct)` first to invalidate existing unused codes, then `BackupCodeService.GenerateAsync(userId, 8, ct)` to generate new codes (these are separate methods — `GenerateAsync` does NOT call `RevokeAllAsync` internally)
   - Returns `{ codes: string[] }`
   - Requires `[Authorize]`, rate limited with `"data-write"` policy

9. **`VendorApiService`** extended with new methods for backup code operations:
   - `getBackupCodeRemainingCount(): Promise<{ remaining: number }>`
   - `regenerateBackupCodes(): Promise<{ codes: string[] }>`

10. **`VendorComponent` header/nav** updated to include a "Settings" link:
    - Adding `/vendor/settings` navigation along with existing "Back to List" and "Create Organisation" buttons
    - Conditional visibility: shown in the header nav at top of the vendor page

## Tasks / Subtasks

- [ ] Create `features/vendor/settings/vendor-settings.component.ts` with `VendorSettingsComponent` (AC: 2)
  - [ ] Sections: 2FA status card, backup codes warning banner, password change
  - [ ] Unenrolled state: info banner + embedded `TwoFactorEnrollmentComponent` in page mode
  - [ ] Enrolled state: status badge + `BackupCodesDisplayComponent` + "Re-enroll" button
- [ ] Create `features/vendor/settings/vendor-settings.component.html` (template)
- [ ] Create `features/vendor/settings/vendor-settings.component.scss` (styles)
- [ ] Add `/vendor/settings` route to `app.routes.ts` (AC: 1)
  - [ ] Child of `/vendor` path, uses `authGuard` + `vendorGuard`
- [ ] Add `GET /auth/backup-codes/remaining` endpoint to `TwoFactorController` (AC: 7)
  - [ ] Calls `BackupCodeService.GetRemainingCountAsync`, returns `{ remaining: number }`
  - [ ] `[Authorize]`, rate limited with `"data-read"`
- [ ] Add `POST /auth/backup-codes/regenerate` endpoint to `TwoFactorController` (AC: 8)
  - [ ] Calls `BackupCodeService.RevokeAllAsync(userId, ct)` then `BackupCodeService.GenerateAsync(userId, 8, ct)` (NOT just `GenerateAsync` — these are separate methods)
  - [ ] Returns `{ codes: string[] }`
  - [ ] `[Authorize]`, rate limited with `"data-write"`
- [ ] Add `POST /auth/change-password` endpoint (AC: 5)
  - [ ] Create `ChangePasswordRequest` DTO in `Models/Auth/AuthDtos.cs`
  - [ ] Add `ChangePasswordAsync` to `AuthService` — validates current password, hashes new password, saves
  - [ ] Create endpoint on `AuthController` with `[Authorize]`
- [ ] Create `ChangePasswordDialog` component (inline or separate file) (AC: 5)
  - [ ] Form: current password, new password, confirm new password (min 8 chars)
  - [ ] MatDialog with Cancel and Save
  - [ ] On success: MatSnackBar "Password updated successfully."
- [ ] Extend `VendorApiService` with backup code and password change methods (AC: 9)
- [ ] Update `VendorComponent` header nav to include Settings link (AC: 10)
- [ ] Wire the `TwoFactorSetupGuard` on the vendor parent route to redirect unenrolled Vendors (AC: 6)
- [ ] Run `npm run build` to verify compilation
- [ ] Run `dotnet build` to verify API compilation
- [ ] Verify no regressions in existing vendor flows

## Dev Notes

### Architecture Compliance

**This story implements:**
- FR-1.1 (Vendor-accessible settings route `/vendor/settings`) — AC 1
- FR-1.2 (Current 2FA status badge) — AC 3
- FR-1.3 (Enrollment flow via shared component) — AC 3
- FR-1.4 (Redirect guard for unenrolled Vendors) — AC 6
- FR-1.5 (Password change form) — AC 4
- FR-4.6 (Warning banner when < 3 backup codes remain) — AC 3
- FR-6 (API Endpoints) — AC 7, 8, plus `POST /auth/change-password`
- UX-DR14 (Dismissible banner) — AC 3

**Does NOT implement (deferred):**
- FR-2.1 (2FA column in Staff Management) — Story 25-6
- FR-2.4 (Org settings toggles) — Story 25-7
- FR-2.6 (2FA Audit Log) — Story 25-7
- FR-3 (Onboarding Emails) — Story 25-8
- FR-4.5 (Login fallback — backup code verification during login) — Story 25-3 (API) + Story 25-5 would add the UI link

### Source Tree Components to Touch

**New files:**
```
apps/web/src/app/features/vendor/settings/vendor-settings.component.ts
apps/web/src/app/features/vendor/settings/vendor-settings.component.html
apps/web/src/app/features/vendor/settings/vendor-settings.component.scss
```

**Modified files:**
```
apps/web/src/app/app.routes.ts                              # MODIFY: + /vendor/settings route
apps/web/src/app/features/vendor/vendor.component.ts         # MODIFY: + navigation link to settings
apps/web/src/app/features/vendor/vendor.component.html       # MODIFY: + Settings nav link in header
apps/web/src/app/features/vendor/vendor-api.service.ts       # MODIFY: + backup code + password change methods
apps/api/Controllers/V1/Auth/TwoFactorController.cs          # MODIFY: + backup-codes/remaining + backup-codes/regenerate
apps/api/Controllers/V1/AuthController.cs                   # MODIFY: + POST /auth/change-password
apps/api/Infrastructure/Auth/AuthService.cs                  # MODIFY: + ChangePasswordAsync method
apps/api/Models/Auth/AuthDtos.cs                            # MODIFY: + ChangePasswordRequest DTO
```

### Existing Patterns & Conventions (MUST FOLLOW)

**Standalone component pattern:**
- Same conventions as `VendorComponent`: standalone, `inject()` for DI, `signal()` for reactive state
- Use `MatCardModule`, `MatButtonModule`, `MatIconModule`, `MatDialogModule`, `MatSnackBarModule`
- API calls via `VendorApiService` (already injected) and shared `BackupCodeService`

**Route registration pattern (from `app.routes.ts`):**
The existing vendor route:
```typescript
{
  path: 'vendor',
  canActivate: [authGuard, vendorGuard],
  loadComponent: () =>
    import('./features/vendor/vendor.component').then((m) => m.VendorComponent),
},
```

Add `/vendor/settings` as a separate top-level route (not a child, since `VendorComponent` is not a shell with `<router-outlet>`):
```typescript
{
  path: 'vendor/settings',
  canActivate: [authGuard, vendorGuard],
  loadComponent: () =>
    import('./features/vendor/settings/vendor-settings.component').then(
      (m) => m.VendorSettingsComponent,
    ),
},
```

**Backup code session persistence:**
- The low-codes warning banner dismissal is stored in `sessionStorage` (not `localStorage`) — cleared on browser close
- Storage key: `midi_kaval_backup_warning_dismissed`

**Backup code regeneration safety:**
- `BackupCodeService.GenerateAsync` and `BackupCodeService.RevokeAllAsync` are **separate methods** — `GenerateAsync` does NOT call `RevokeAllAsync` internally
- The `POST /auth/backup-codes/regenerate` endpoint must call `RevokeAllAsync(userId, ct)` first, then `GenerateAsync(userId, 8, ct)` in sequence
- Old codes are invalidated, new codes returned — displayed exactly once in a dialog

**Vendor navigation update:**
Add a "Settings" link in the existing vendor header nav (in `vendor.component.html`), alongside "Back to List", "Create Organisation", and "Log out":
```html
<nav>
  <a routerLink="/vendor/settings" class="nav-link">Settings</a>
  <!-- existing nav items remain -->
</nav>
```
Use `routerLink` and `routerLinkActive="active"` pattern for the active state.

**Password change dialog and API:**
- A new `POST /auth/change-password` endpoint is required on `AuthController` (or `TwoFactorController`):
  - Accepts `ChangePasswordRequest { CurrentPassword, NewPassword, ConfirmNewPassword }`
  - Requires `[Authorize]`
  - Validates current password, updates to new password
  - Returns `{ message: "Password updated successfully." }`
- Create `ChangePasswordRequest` DTO in `Models/Auth/AuthDtos.cs`
- Add `ChangePasswordAsync` method to `AuthService` — validates current password via `passwordHasher.VerifyHashedPassword`, then hashes and saves new password
- `ChangePasswordDialog` component:
  - Form with current password, new password, confirm new password fields (all `type="password"`, min 8 chars)
  - MatDialog with Cancel and Save buttons
  - On success: MatSnackBar "Password updated successfully." and close dialog

### Testing Standards

- Unit test: Unenrolled Vendor visits `/vendor/settings` → sees info banner + enrollment component
- Unit test: Enrolled Vendor visits `/vendor/settings` → sees status badge + remaining count + password change
- Unit test: Backup codes < 3 shows warning banner, dismissal persists for session
- Unit test: "Re-enroll" button opens enrollment dialog
- Unit test: "Regenerate Backup Codes" calls RevokeAllAsync then GenerateAsync, shows new codes in dialog
- Unit test: `GET /auth/backup-codes/remaining` returns correct count
- Unit test: `POST /auth/change-password` validates current password, rejects wrong password
- Integration test: Unenrolled Vendor navigating to `/vendor` → redirects to `/vendor/settings`
- All existing tests must pass

### Prerequisites (from previous stories)

- Story 25-1: `BackupCodeService` with `GenerateAsync`, `VerifyAsync`, `GetRemainingCountAsync`, `RevokeAllAsync`
- Story 25-3: Login response contract with `requires2faSetup`, `setupUrl: "/vendor/settings"` for Vendors
- Story 25-4: `TwoFactorEnrollmentComponent` (shared, page mode + dialog mode), `BackupCodesDisplayComponent`, `TwoFactorService`, `BackupCodeService` Angular services, `TwoFactorSetupGuard`, `navigateAfterLogin()` updates

## Project Structure Notes

- `VendorSettingsComponent` lives in `features/vendor/settings/` alongside the existing `VendorComponent`
- The page is a standard settings page layout — not a wizard, not a modal
- Uses shared components from `shared/components/2fa/` to avoid duplicating enrollment logic
- New API endpoints on `TwoFactorController` keep 2FA-related routes co-located
- The password change section is intentionally simple: button → dialog, reusing existing API. A separate password settings page was discussed but is out of scope.

## References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 5: "Vendor Settings Page"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "Frontend Architecture" (component reuse), "File Changes — Web"
- **UX Design:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/.working/key-vendor-settings.html` — full page layout with QR section, manual key, Copy Key, TOTP input, Verify & Enable, password change
- **UX Experience:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/EXPERIENCE.md` — Microcopy, voice rules
- **Existing vendor component:** `apps/web/src/app/features/vendor/vendor.component.ts` — viewState-based pages
- **Existing vendor template:** `apps/web/src/app/features/vendor/vendor.component.html` — nav + content layout
- **Existing vendor API:** `apps/web/src/app/features/vendor/vendor-api.service.ts` — API call pattern
- **Story 25-4 (prerequisite):** `_bmad-output/implementation-artifacts/25-4-refactor-enrollment-component.md` — shared enrollment component
- **Story 25-1 (prerequisite):** `_bmad-output/implementation-artifacts/25-1-data-model-api-foundation.md` — BackupCodeService
- **Story 25-3 (prerequisite):** `_bmad-output/implementation-artifacts/25-3-login-response-contract.md` — login response fields

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

### Completion Notes List

### File List
