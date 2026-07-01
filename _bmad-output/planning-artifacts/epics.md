---
stepsCompleted: [1, 2]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-07-01/prd.md
  - _bmad-output/planning-artifacts/architecture-2fa.md
  - _bmad-output/planning-artifacts/architecture-role-management.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/EXPERIENCE.md
workflowType: 'epic-creation'
project_name: 'Midi-Kaval'
user_name: 'Admin'
date: '2026-07-01'
---

# Midi-Kaval - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Midi-Kaval, decomposing the requirements from the PRD, UX Design, and Architecture requirements for **Two-Factor Authentication — Universal Enrollment & Administration** into implementable stories.

## Requirements Inventory

### Functional Requirements

#### FR-1: Vendor Settings Page
- **FR-1.1** — Vendor-accessible settings route `/vendor/settings` protected by `VendorGuard`
- **FR-1.2** — Page displays current 2FA status as a badge (Not enrolled / Enrolled with date)
- **FR-1.3** — Enrollment flow: `POST /auth/enroll-2fa` → QR code (200x200px, theme-aware) + manual setup key + "Copy Key" button → 6-digit TOTP input → `POST /auth/verify-enroll-2fa`
- **FR-1.4** — Redirection guard: if Vendor is authenticated but `totp_secret IS NULL`, any `/vendor/*` route redirects to `/vendor/settings` with banner: *"Two-factor authentication is required. Please set it up to continue."*
- **FR-1.5** — Page also includes password change form (reuses existing `ChangePasswordComponent`)

#### FR-2: Director 2FA Management
- **FR-2.1** — Staff Management table gains a "2FA" column: ✓ (green) with tooltip showing enrollment date, ✗ (red) if unenrolled. Clicking opens MatMenu with contextual actions
- **FR-2.2** — "Reset 2FA" action: confirmation dialog → `POST /admin/users/{id}/reset-2fa` → clears `totp_secret` → audit event logged
- **FR-2.3** — "Send Reminder" action: emails user with 2FA setup prompt → confirmation toast
- **FR-2.4** — Organisation-level "Require 2FA" toggle in Org Settings → `PUT /admin/settings/require-2fa` → stored as `require_2fa` on `organisations` table
- **FR-2.5** — Temporary bypass code generation: `POST /admin/users/{id}/generate-bypass-code` → single-use, 30-min TTL, rate-limited 2/hr per Director
- **FR-2.6** — 2FA audit log view: `/admin/audit/2fa` — filterable by event type, user, date range — CSV export
- **FR-2.7** — Delegation to Coordinators: toggle to allow Coordinators to reset 2FA for SocialWorker/CaseWorker only (no bypass code access)
- **FR-2.8** — Enrollment notification: Director receives in-app notification when any user in org enrolls

#### FR-3: Proactive Onboarding Emails
- **FR-3.1** — Welcome email with 2FA setup link on new account creation
- **FR-3.2** — 2FA enrollment link in email OTP message footer
- **FR-3.3** — Director-initiated invitation flow: checkbox "Include 2FA setup instructions" (default: checked)
- **FR-3.4** — Legacy migration campaign: one-time Hangfire job scanning users with `totp_secret IS NULL`, sends migration email, throttled 100/hr

#### FR-4: Backup Codes
- **FR-4.1** — 8 single-use backup codes generated on successful 2FA enrollment
- **FR-4.2** — Each code is 10-char alphanumeric (e.g., `A3K9X7M2P1`)
- **FR-4.3** — Codes hashed (SHA-256) and stored in `backup_codes` table with `used` flag
- **FR-4.4** — Displayed exactly once on enrollment success screen with "Download as .txt" button
- **FR-4.5** — Login fallback: after 1 failed TOTP, show "Use a backup code instead" → verify → mark used → grant access. After use, prompt re-enrollment
- **FR-4.6** — Warning banner at profile when < 3 backup codes remain: *"You have [N] backup code(s) remaining. Consider re-enrolling 2FA."*

#### FR-5: Login Response Contract Updates
- **FR-5.1** — Login response for unenrolled users includes `{ requires2faSetup: true, setupUrl: "/vendor/settings" }` (role-aware URL)
- **FR-5.2** — When org-level `require_2fa` is enabled, response also includes `"orgRequires2fa": true`
- **FR-5.3** — Angular client uses `setupUrl` from response to redirect post-login (dynamic, not hardcoded)

#### FR-6: API Endpoints
| Method | Path | Role | Notes |
|--------|------|------|-------|
| `GET` | `/auth/2fa-status` | All | Check enrollment state |
| `POST` | `/admin/users/{id}/reset-2fa` | Director/Coordinator* | Clear TOTP secret |
| `POST` | `/admin/users/{id}/send-2fa-reminder` | Director | Send reminder email |
| `POST` | `/admin/users/{id}/generate-bypass-code` | Director | Generate 30-min bypass |
| `GET` | `/admin/audit/2fa` | Director | 2FA audit log |
| `PUT` | `/admin/settings/require-2fa` | Director | Toggle org mandate |
| `PUT` | `/admin/settings/delegate-2fa-reset` | Director | Delegate to Coordinators |
| `POST` | `/auth/verify-backup-code` | All | Verify backup code during login |
*Coordinator scope limited to SocialWorker/CaseWorker

**Existing endpoints reused:** `POST /auth/enroll-2fa`, `POST /auth/verify-enroll-2fa`

#### FR-7: Backup Codes Data Model
- New `backup_codes` table: `id UUID PK`, `user_id UUID FK`, `code_hash TEXT`, `used BOOLEAN`, `created_at_utc`, `used_at_utc`
- Indexes: `ix_backup_codes_user_id`, `ix_backup_codes_user_id_unused` (partial, WHERE `used = FALSE`)
- New column `require_2fa` on `organisations` (boolean, default FALSE)

### Non-Functional Requirements

#### NFR-1: Security
- **NFR-1.1** — TOTP secrets stored encrypted-at-rest (`bytea` + EF Core value converter)
- **NFR-1.2** — Bypass codes: single-use, 30-min TTL, SHA-256 hash in Redis
- **NFR-1.3** — Backup codes: single-use, SHA-256 hash, regenerated on every enrollment
- **NFR-1.4** — Rate limit: 5 attempts/min per user on `/auth/verify-enroll-2fa`
- **NFR-1.5** — Rate limit: 5 attempts/min per user on `/auth/verify-backup-code`
- **NFR-1.6** — Rate limit: 2 per hour per Director on bypass code generation
- **NFR-1.7** — After 5 consecutive failed TOTP attempts: 15-min lockout, logged as `2fa_failed_totp`. Email OTP still works during lockout.

#### NFR-2: Audit
- **NFR-2.1** — 11 event types in `audit_events`: `2fa_enrolled`, `2fa_disabled`, `2fa_reset`, `2fa_bypass_generated`, `2fa_bypass_used`, `2fa_backup_used`, `2fa_failed_totp`, `2fa_mandate_enabled`, `2fa_mandate_disabled`, `2fa_delegation_enabled`, `2fa_delegation_disabled`

#### NFR-3: Performance
- **NFR-3.1** — `GET /auth/2fa-status` completes in < 50ms
- **NFR-3.2** — Backup code verification completes in < 100ms
- **NFR-3.3** — All 2FA endpoints respect existing rate limiting policy

#### NFR-4: UX
- **NFR-4.1** — Vendor settings page fully keyboard-navigable
- **NFR-4.2** — QR code renders at min 200x200px with light/dark theme support
- **NFR-4.3** — All error states show human-readable messages (no JSON/stack traces)
- **NFR-4.4** — Backup code download produces plain `.txt` file with clear labeling
- **NFR-4.5** — MatMenu interaction for 2FA column consistent with existing Material patterns

### Additional Requirements (Architecture)

- **Starter template:** Not applicable — brownfield extension to existing project
- QR code rendering via `qrcode` npm package (client-side rendering, theme-aware dark mode via `prefers-color-scheme`)
- Refactor existing `Enroll2FAComponent` from Director-only to role-agnostic (PIN-driven role configuration)
- New `TwoFactorSetupGuard` route guard — reads login response `setupUrl`, redirects unenrolled users
- New service layering: `TwoFactorController → TwoFactorService → BackupCodeService → AppDbContext`, `AdminTwoFactorController → AdminTwoFactorService`
- Bypass code storage in Redis following existing `OtpChallengeStore` pattern
- Login response is append-only (backward compatible — existing clients ignore unknown fields)
- `.cxx` build directory must be cleaned and Android build must run from short path or with `GRADLE_USER_HOME` set to avoid Windows MAX_PATH (260 char) limit
- All new Angular components are standalone (no NgModules)
- Existing `Enroll2FAComponent` needs code audit to extract any Director-specific business logic before refactoring

### UX Design Requirements

- **UX-DR1 (Theme-aware QR):** QR code must render at min 200x200px, respect light/dark theme via `prefers-color-scheme` media query
- **UX-DR2 (TOTP auto-submit):** 6-digit TOTP input auto-submits on 6th character entered; falls back to manual button click for accessibility
- **UX-DR3 (Backup codes grid):** 8 backup codes displayed as 2-column monospace chip grid on enrollment success
- **UX-DR4 (Download .txt):** "Download as .txt" button on backup codes screen; file header includes user email
- **UX-DR5 (✓/✗ indicators):** 2FA column uses both color AND icon (green `check_circle` / red `cancel`) — never color alone
- **UX-DR6 (MatMenu by state):** Clicking 2FA column icon opens MatMenu with contextual actions per enrollment state and permissions
- **UX-DR7 (Confirmation dialog):** Reset 2FA uses `MatDialog` with Cancel (default focus) and Red "Reset 2FA" buttons
- **UX-DR8 (Bypass code dialog):** Large monospace code display with Copy button, warning text about 30-min expiry
- **UX-DR9 (Toggle immediate save):** `MatSlideToggle` writes immediately on change with loading state; reverts on failure
- **UX-DR10 (Keyboard + screen reader):** All dialogs/menus/forms keyboard-operable; QR `alt` text; icon `aria-label`; `aria-live="polite"` on errors
- **UX-DR11 (Reduced motion):** All animations respect `prefers-reduced-motion` media query
- **UX-DR12 (Touch targets):** All interactive elements meet 48x48px minimum touch target
- **UX-DR13 (Error microcopy):** Human-readable error messages per EXPERIENCE.md voice rules — "The code you entered is incorrect. Try again."
- **UX-DR14 (Banner dismissible):** Low backup codes warning banner is dismissible with × button

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR-1 | Epic 1 | Vendor Settings Page |
| FR-2 | Epic 1 | Director 2FA Management |
| FR-3 | Epic 1 | Proactive Onboarding Emails |
| FR-4 | Epic 1 | Backup Codes |
| FR-5 | Epic 1 | Login Response Contract |
| FR-6 | Epic 1 | API Endpoints |
| FR-7 | Epic 1 | Data Model |

## Epic List

### Epic 1: 2FA Universal Enrollment & Administration

Every role — Vendor, Director, Coordinator, and field workers — can enroll in 2FA through the web UI. Directors gain full visibility and control over their team's 2FA status, with tools to reset, remind, mandate, delegate, and audit. Backup codes and bypass codes provide recovery paths.

**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7

**Implementation sequence (ordered stories):**
1. Data model + API foundation — `backup_codes` table, `require_2fa` column, `BackupCodeService`, extend `TwoFactorController`
2. Admin API — `AdminTwoFactorController` with reset/remind/bypass/audit/toggle endpoints
3. Login response contract — Extend `AuthService` to return `requires2faSetup` + `setupUrl`
4. Refactor enrollment component — Make `Enroll2FAComponent` role-agnostic
5. Vendor Settings page — New route, enrollment card, backup codes display, guard redirect
6. Director Staff Management — 2FA column, MatMenu actions, dialogs
7. Organisation Settings + Audit Log — Toggles, 2FA audit view, delegation
8. Proactive onboarding emails — Welcome email, OTP footer, invite checkbox, Hangfire migration job

