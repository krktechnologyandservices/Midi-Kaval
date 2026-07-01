# PRD Extract — 2FA Universal Enrollment & Administration

> Extracted from: `prd-Midi-Kaval-2026-07-01\prd.md`, `docs/lowlevel-technical-document.md`, `docs/user-manual.md`
> Date: 2026-07-01

---

## 1. All Screens/Pages Requiring UX Design

| # | Page/Screen | Route | Primary Role(s) | PRD Ref | Description |
|---|-------------|-------|-----------------|---------|-------------|
| 1 | **Vendor Settings Page** | `/vendor/settings` | Vendor | FR-1 | 2FA self-enrollment: QR code, manual key, TOTP verify, backup codes, password change. First-time banner gate. |
| 2 | **Staff Management Page** | `/admin/staff` | Director, Coordinator | FR-2.1, FR-2.2, FR-2.3 | New "2FA" column in user table with MatMenu actions (Reset 2FA, Send Reminder, Generate Bypass Code). Role-aware visibility. |
| 3 | **Organisation Settings Page** | `/admin/settings` (existing) | Director | FR-2.4, FR-2.7 | Two new toggles: "Require 2FA for all staff" and "Allow Coordinators to reset 2FA for field workers". |
| 4 | **2FA Audit Log Page** | New view in admin area | Director | FR-2.6 | Filterable table: Timestamp, User, Event Type, IP Address. CSV export. Scoped to Director's org. |
| 5 | **Login Page** | `/login` | All | FR-5 | Updated login response contract: redirect unenrolled users to role-aware setup URL. |
| 6 | **Login → TOTP Screen** | `/login/totp` | All (when enrolled) | — | Existing TOTP code entry during login. Needs backup code fallback link (FR-4.5). |
| 7 | **Profile/Settings — 2FA Enrollment** | `/settings/2fa` | All non-Vendor roles | FR-2.4 | Generic 2FA enrollment for non-Vendor users. QR → TOTP verify → backup codes. |
| 8 | **Enrollment Success Screen** | (step within enrollment) | All | FR-4.4 | Displays 8 backup codes exactly once with "Download as .txt" button. |
| 9 | **Backup Codes Warning Banner** | On user profile | All | FR-4.6 | Warning banner when < 3 backup codes remain: "You have [N] backup code(s) remaining. Consider re-enrolling 2FA." |
| 10 | **Invitation Flow — 2FA Checkbox** | `/admin/invitations` (existing) | Director | FR-3.3 | "Include 2FA setup instructions in invitation email" checkbox (default: checked). |
| 11 | **Post-Login Redirect Guard** | (interstitial route guard) | All unenrolled | FR-1.4, FR-2.4 | After login, if user is unenrolled and org mandates 2FA, redirect to enrollment with explanatory banner. |

---

## 2. All Flows (User Journeys UJ-1 to UJ-4)

### UJ-1: Vendor Self-Enrollment (7 steps + failure path)

| Step | Actor Action | System Response | UI Element |
|------|-------------|-----------------|------------|
| 1 | Navigate to Kaval Online, log in with email + password | Login form | `LoginComponent` |
| 2 | Enter email OTP | OTP sent to email | `OtpLoginComponent` |
| 3 | App lands on `/vendor/settings` | First-time banner: *"Two-factor authentication is required to access vendor features."* | Banner component |
| 4 | Sees "Enable Two-Factor Authentication" card with QR code + manual setup key | Displays QR (200x200px, theme-aware) + key + "Copy Key" button | QR component, setup key display |
| 5 | Opens authenticator app, scans QR code | — | — |
| 6 | Enters 6-digit TOTP code | Verifies via `POST /auth/verify-enroll-2fa` | TOTP input field |
| 7 | **Success:** Badge updates to "2FA: Enabled ✓" | 8 backup codes displayed with "Download as .txt" | Badge component, backup codes list |
| **Failure** | Closes page without enrolling | Next `/vendor/*` navigation redirects back to `/vendor/settings` | Route guard redirect |

**Error states (FR-1.3):**
- Invalid TOTP code → inline error: *"The code you entered is incorrect. Try again."*
- Expired enrollment session → *"Your enrollment session has expired. Please start again."*

### UJ-2: Director Monitoring Team 2FA (7 steps)

| Step | Actor Action | System Response | UI Element |
|------|-------------|-----------------|------------|
| 1 | Raj logs in (already enrolled in 2FA) | Standard TOTP login flow | `TotpLoginComponent` |
| 2 | Opens Staff Management page | Existing staff table with new "2FA" column | `StaffListComponent` |
| 3 | Sees 2FA column: ✓ (green, 12 users), ✗ (red, 3 users) | Hover tooltip on ✓ shows enrollment date | MatTable, tooltip |
| 4 | Clicks a ✗ cell | Contextual `MatMenu` opens with actions | `MatMenu` |
| 5 | Clicks "Send 2FA Reminder" | Email sent: *"Raj requires you to set up two-factor authentication."* Confirmation toast appears | Toast notification |
| 6 | Navigates to Organisation Settings, toggles "Require 2FA for all staff" ON | `PUT /admin/settings/require-2fa` | `mat-slide-toggle` |
| 7 | Next login for unenrolled users redirects them to enrollment | Login response includes `orgRequires2fa: true` + `setupUrl` | Route guard |

**MatMenu actions per state:**
- ✓ state: "Reset 2FA", "Generate Bypass Code"
- ✗ state: "Reset 2FA", "Send Reminder"
- No permissions: menu hidden entirely

### UJ-3: Field Worker Lockout Recovery (7 steps)

| Step | Actor Action | System Response | UI Element |
|------|-------------|-----------------|------------|
| 1 | Field worker (Priya) calls Director (Raj) | — | — |
| 2 | Raj opens Staff Management, finds Priya, clicks the ✓ cell → MatMenu → "Reset 2FA" | Confirmation dialog appears | `MatMenu`, confirmation dialog |
| 3 | Dialog: *"This will clear Priya's 2FA enrollment. She'll need to re-enroll on next login. Continue?"* | — | Confirmation dialog (Cancel / Confirm) |
| 4 | Raj confirms | `totp_secret` cleared, audit event `2fa_reset` logged | — |
| 5 | Raj generates bypass code | Dialog shows code + "Copy" button + warning text: *"This code expires in 30 minutes and can only be used once. Share it securely."* | Bypass code dialog |
| 6 | Priya logs in with email + password + bypass code | Bypass code validated from Redis | Login with bypass code field |
| 7 | Priya re-enrolls 2FA with new phone, new backup codes generated | Standard enrollment flow | Enrollment flow |

### UJ-4: Coordinator 2FA Reset (Delegated) (7 steps)

| Step | Actor Action | System Response | UI Element |
|------|-------------|-----------------|------------|
| 1 | Before delegation: Coordinator sees Staff Management page | "Reset 2FA" button not visible for any user | Conditional rendering |
| 2 | Director toggles "Allow Coordinators to reset 2FA for field workers" ON | `PUT /admin/settings/delegate-2fa-reset` | `mat-slide-toggle` |
| 3 | Coordinator now sees "Reset 2FA" button | Only for SocialWorker/CaseWorker users | Conditional action visibility |
| 4 | Coordinator clicks "Reset 2FA" for a field worker | Confirmation dialog | Confirmation dialog |
| 5 | Confirms | `totp_secret` cleared | — |
| 6 | Field worker logs in, re-enrolls | Standard enrollment | Enrollment flow |
| 7 | Director toggles delegation OFF | All Coordinator reset buttons disappear immediately | Reactive UI update |

**Constraints:**
- Coordinator never sees "Generate Bypass Code"
- Toggle has tooltip: *"Coordinators can reset 2FA for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."*

---

## 3. All Reusable UI Components Required

| Component | Usage | PRD Ref | Notes |
|-----------|-------|---------|-------|
| **MatMenu** (Angular Material) | Context menu on 2FA column cells in Staff Management | FR-2.1 | Role-aware actions (Reset, Reminder, Bypass Code) |
| **mat-slide-toggle** | Two toggles in Organisation Settings | FR-2.4, FR-2.7 | "Require 2FA" toggle + "Delegate reset" toggle |
| **QR Code Component** | Vendor Settings and generic enrollment page | FR-1.3 | Min 200x200px, theme-aware (light/dark), displays provisioning URI |
| **Badge Component** | 2FA status indicator | FR-1.2 | "Not Set Up" / "Enabled" with icon, enrollment date tooltip |
| **Copy-to-Clipboard Button** | Manual setup key copy | FR-1.3 | "Copy Key" button |
| **Backup Codes Display** | Enrollment success step | FR-4.4 | Shows 8 codes, single-use, displayed exactly once |
| **Download as .txt Button** | Backup codes download | FR-4.4, NFR-4.4 | Produces plain `.txt` with header: "Kaval Online Backup Codes — [User Email] — Store these securely. Each code can only be used once." |
| **Confirmation Dialog** | Reset 2FA confirmation | FR-2.2, FR-2.7 | "This will clear [user]'s 2FA enrollment. They'll need to re-enroll on next login. Continue?" with Cancel/Confirm |
| **Bypass Code Dialog** | Generated bypass code display | FR-2.5 | Shows code + "Copy" button + warning text (30-min expiry, single-use) |
| **Toast Notification** | Reminder sent confirmation | FR-2.3 | "Reminder sent to [user email]." |
| **Tooltip** | Hover on ✓ cell in 2FA column | FR-2.1 | Shows enrollment date |
| **CTA Button ("Enable Now")** | Prompt unenrolled users | FR-1.2 | Primary action button |
| **TOTP Input Field** | 6-digit code entry during enrollment | FR-1.3 | With inline error handling |
| **Inline Error Display** | Invalid code / expired session messages | FR-1.3 | Human-readable errors, not JSON |
| **First-Time Banner** | Top of page when 2FA not enrolled | FR-1.4 | *"Two-factor authentication is required. Please set it up to continue."* |
| **Warning Banner (Low Backup Codes)** | Profile page when < 3 codes remain | FR-4.6 | *"You have [N] backup code(s) remaining. Consider re-enrolling 2FA to generate new codes."* |
| **Checkbox** | Invitation flow | FR-3.3 | "Include 2FA setup instructions in invitation email" (default: checked) |
| **In-App Notification (Bell Icon)** | Director notified when user enrolls 2FA | FR-2.8 | Existing notification system: *"[User full name] has enabled two-factor authentication."* |
| **ChangePasswordComponent** | Reused on Vendor Settings page | FR-1.5 | Existing component, no changes needed |
| **Enroll2FAComponent** | Refactored for role-agnostic enrollment | Dependencies | Existing Director component, refactor for reuse |

---

## 4. All States

### 4.1 State Categories

| State Type | Examples | PRD Ref |
|-----------|---------|---------|
| **Loading** | QR code generation loading; TOTP verification spinner; API call states | Implied throughout |
| **Empty / Not Enrolled** | 2FA column shows ✗ (red); "Not Set Up" badge; no backup codes | FR-1.2, FR-2.1 |
| **Success** | 2FA "Enabled ✓" badge; backup codes generated; enrollment date shown | FR-1.2, FR-4.1 |
| **Error — Invalid Code** | Inline error: *"The code you entered is incorrect. Try again."* | FR-1.3 |
| **Error — Expired Session** | *"Your enrollment session has expired. Please start again."* | FR-1.3 |
| **Error — 5 Failed TOTP Lockout** | TOTP verification locked for 15 min; email OTP still works | NFR-1.7 |
| **Error — Rate Limited** | 5 attempts/min exceeded on verify endpoints | NFR-1.4, NFR-1.5 |
| **Warning — Low Backup Codes** | Banner when < 3 backup codes remain | FR-4.6 |
| **Warning — First-Time Gate** | Redirect banner: *"Two-factor authentication is required."* | FR-1.4 |
| **Redirect / Guarded** | Unenrolled user navigates to `/vendor/*` → redirected to `/vendor/settings` | FR-1.4 |
| **Disabled State** | MatMenu hidden when no permissions (Coordinator before delegation) | FR-2.1, FR-2.7 |
| **Bypass Code Generated** | Single-use, 30-min TTL code displayed with copy button | FR-2.5 |
| **Bypass Code Expired** | Code no longer valid after 30 min | FR-2.5 |
| **Toggle ON/OFF** | Org mandate toggle; delegation toggle — clear on/off visual state | FR-2.4, FR-2.7 |

### 4.2 2FA Column Cell States (Staff Management)

| State | Visual | Icon | Hover/Click |
|-------|--------|------|-------------|
| Enrolled | Green | ✓ | Tooltip: enrollment date; MatMenu: Reset 2FA, Generate Bypass Code |
| Not Enrolled | Red | ✗ | MatMenu: Reset 2FA, Send Reminder |
| No Permission | Hidden | — | Menu not rendered |

### 4.3 Backup Code States

| State | Visual | Action |
|-------|--------|--------|
| Freshly generated (after enrollment) | Full screen display, 8 codes | "Download as .txt" available |
| 3+ codes remaining | No banner | Normal operation |
| < 3 codes remaining | Warning banner at top of profile | "Consider re-enrolling 2FA" |
| All 8 used | Banner: "You have 0 backup code(s) remaining" | Must re-enroll or contact Director |

### 4.4 Edge Cases

| Edge Case | Handling | PRD Ref |
|-----------|----------|---------|
| User lost authenticator app (new phone) | Director can reset 2FA + generate bypass code | UJ-3 |
| Exhausted all backup codes + no phone | Contact Director for reset + new bypass code | OQ-2 |
| TOTP clock drift | Verification window: ±1 step (allows ±30s drift) | Tech doc §5.5 |
| 5 consecutive failed TOTP attempts | 15-min lockout; email OTP still works; audit logged | NFR-1.7 |
| Bypass code rate limit exceeded | 2 per hour per Director, enforced server-side | FR-2.5 |
| Redis unavailable for bypass codes | Bypass code generation blocked; DB fallback may be needed (A-2) | FR-2.5 (assumption) |
| Coordinator delegation revoked mid-reset | Any subsequent API call returns 403 | FR-2.7 |
| User has no compatible smartphone | Manual arrangement via Director (A-1) | §2.4 |
| Failed email delivery for reminders | No retry specified; A-5 flags this risk | FR-2.3 (assumption) |

---

## 5. Existing Angular Material Components (to Reuse from Tech Doc)

### 5.1 Component Library

| Component | Package | Usage in App | Reuse for 2FA |
|-----------|---------|-------------|---------------|
| `MatTable` | `@angular/material` | Staff list, case registry, audit log | 2FA column in Staff Management |
| `MatMenu` | `@angular/material` | Context menus | 2FA cell actions (Reset, Reminder, Bypass) |
| `MatSlideToggle` | `@angular/material` | Various toggles | Org mandate + delegation toggles |
| `MatDialog` | `@angular/material` | Confirmation dialogs | Reset 2FA confirmation, bypass code display |
| `MatSnackBar` / Toast | `@angular/material` | Notifications | Reminder sent confirmation |
| `MatTooltip` | `@angular/material` | Hover hints | Enrollment date on ✓ cells |
| `MatIcon` / `material-icons` | `material-icons` | Icons throughout | ✓ (check_circle), ✗ (cancel), 2FA icons |
| `MatBadge` | `@angular/material` | Status badges | 2FA status badge (or use custom) |
| `MatFormField` + `MatInput` | `@angular/material` | All forms | 6-digit TOTP input, manual key display |
| `MatButton` | `@angular/material` | All buttons | CTA, Copy, Download, Cancel, Confirm |
| `MatProgressSpinner` | `@angular/material` | Loading states | QR generation loading, TOTP verification |
| `MatCheckbox` | `@angular/material` | Forms | Invitation "Include 2FA setup instructions" |
| `MatChip` / `MatChipList` | `@angular/material` | Tag/chip display | Backup code chips? (optional) |
| `MatPaginator` | `@angular/material` | Table pagination | 2FA Audit Log pagination |
| `MatSort` | `@angular/material` | Table sorting | 2FA Audit Log sortable columns |
| `MatDatepicker` | `@angular/material` | Date range filters | 2FA Audit Log date range filter |
| `MatSelect` | `@angular/material` | Dropdown filters | 2FA Audit Log event type filter |
| `MatButtonToggle` | `@angular/material` | Toggle groups | Alternative to individual toggles |

### 5.2 Existing App Patterns (Tech Doc)

| Pattern | Implementation | Reuse Notes |
|---------|---------------|-------------|
| **Standalone components** (no NgModules) | Angular 19 pattern | All new components must follow this pattern |
| **Auth guards** | `AuthGuard`, `GuestGuard`, `DirectorGuard`, `VendorGuard` | New guard needed for 2FA enrollment redirect |
| **Route structure** | Path + Component + Guard pattern | Follow existing conventions for new routes |
| **API services** | `HttpClient` + `environment.apiBaseUrl` + `Observable<ApiResponse<T>>` | New services for 2FA, admin endpoints |
| **Auth state** | `AuthSessionService` with signals (`currentUser`, `isAuthenticated`) | Add `requires2faSetup` signal |
| **CSS** | Angular Material theme + SCSS | Follow existing theming; QR code must be theme-aware (NFR-4.2) |
| **Error handling** | `ApiProblemDetailsMiddleware` returns RFC 9457 ProblemDetails | Client shows human-readable messages not JSON (NFR-4.3) |
| **Response envelope** | `{ data: ..., meta: { requestId } }` | All API responses follow this pattern |
| **CSP** | `img-src 'self' data: blob:` | QR code rendering uses `data:` which is already allowed |

### 5.3 Existing Components to Refactor/Extend

| Existing Component | PRD Usage | Work Required |
|-------------------|-----------|---------------|
| `Enroll2FAComponent` | Role-agnostic enrollment flow | Refactor from Director-only to generic; add backup codes display |
| `StaffListComponent` | 2FA column + MatMenu actions | Add column, cell rendering, MatMenu integration |
| `SettingsComponent` | Vendor Settings page | Add 2FA enrollment section, password change section |
| `ChangePasswordComponent` | Reuse on Vendor Settings | No changes needed |
| Auth guard pipeline | 2FA enrollment redirect | New redirect guard for `2FA_SETUP_REQUIRED` login response |

---

## 6. Existing Page Patterns to Align With (from User Manual)

### 6.1 Staff Management Page Pattern (`/admin/staff`)

Current pattern (user manual §11.1):
- Table view of all staff in organisation
- Filter by role, status, or name
- User profile actions: Suspend, Reactivate, Reset 2FA, Force Password Reset
- Click user → open profile → actions available

**2FA extension:** Add inline 2FA column to table (no need to open profile for 2FA actions). Actions via MatMenu directly on table row.

### 6.2 Organisation Settings Page Pattern

Current pattern (implied throughout user manual):
- Configuration page for org-level settings
- Toggle-based controls

**2FA extension:** Add two `mat-slide-toggle` controls with tooltips. Already exists as an extendable page.

### 6.3 Login Page & Authentication Flow Pattern (User manual §3)

Current flow:
```
Email + Password → OTP via Email → Access Granted (non-Director, no 2FA)
Email + Password → TOTP Code → Access Granted (Director with 2FA)
```

**2FA extension post-PRD:**
```
Email + Password → OTP via Email → [if org requires 2FA & not enrolled] → Redirect to enrollment
Email + Password → TOTP Code → [if enrolled] → Access Granted
Email + Password → Bypass Code → [if bypass mode] → Redirect to enrollment
Email + Password → [if login response has requires2faSetup] → Redirect to /vendor/settings or /settings/2fa
```

### 6.4 2FA Enrollment Pattern (Existing Director flow, user manual §3.2)

Current flow:
```
Admin → My Profile → Enable 2FA
  → Scan QR code with authenticator app
  → Enter verification code to confirm
  → 2FA is enabled
```

**2FA extension:** This exact pattern reused for:
- Vendors at `/vendor/settings`
- All roles at `/settings/2fa`
- On first-login enrollment prompt (when org mandate is enabled)

### 6.5 In-App Notification Pattern (User manual §13)

Current pattern:
- Bell icon in header showing unread count
- Click to expand notification list
- Notifications for: case assignments, visit reminders, claim approvals, system alerts
- Dismissible notifications

**2FA extension:** New notification type: *"[User full name] has enabled two-factor authentication."* for Director when staff enrolls (FR-2.8).

### 6.6 Audit Log Pattern (User manual §12.4)

Current pattern (`/admin/audit`):
- Filterable by: date range, event type, user
- Paginated results with timestamp and actor details

**2FA extension:** New 2FA-specific audit log view (FR-2.6) following same pattern but scoped to 2FA events.

### 6.7 Invitation Flow Pattern (User manual §11.6)

Current flow:
```
Admin → Invitations → Send Invitation
  → Enter email and role
  → System sends invitation link
  → Recipient creates account via link
```

**2FA extension:** Add checkbox: "Include 2FA setup instructions in invitation email" (default: checked) (FR-3.3).

### 6.8 Profile/Settings Pattern (User manual §3.2)

Current pattern:
- User settings at `/settings`
- Password change form
- 2FA enrollment for Directors only

**2FA extension:** Add backup codes warning banner (FR-4.6) when < 3 codes remain. Add generic 2FA enrollment section for all roles.

### 6.9 Role-Based UI Pattern

Current pattern (throughout app):
- Directors see admin features (staff mgmt, audit, org settings, invitations)
- Vendors see vendor portal at `/vendor`
- Coordinators see staff data but limited actions
- Field workers (SocialWorker, CaseWorker) see cases, visits

**2FA extension:** Follow same role-based visibility:
- Director: Full 2FA management (reset, remind, bypass, audit, toggles)
- Coordinator: Limited reset (only SocialWorker/CaseWorker, after delegation, no bypass)
- Vendor: Self-enrollment + password change only
- Field workers: Self-enrollment, backup code management

---

## Appendix: Data Flow Summary

```
Vendor Settings Page        Staff Management         Organisation Settings
┌──────────────────────┐   ┌───────────────────┐    ┌────────────────────────┐
│ QR Code + Manual Key │   │ MatTable + 2FA    │    │ mat-slide-toggle       │
│ TOTP Input + Verify  │   │ Column (✓/✗)      │    │ "Require 2FA"          │
│ Backup Codes Display │   │ MatMenu per cell  │    │ mat-slide-toggle       │
│ Download .txt        │   │ Reset | Remind    │    │ "Delegate to Coord."   │
│ Password Change      │   │ | Bypass Code     │    └────────────────────────┘
└──────────────────────┘   └───────────────────┘
         │                         │                        │
         ▼                         ▼                        ▼
┌──────────────────────────────────────────────────────────────┐
│                       API Layer                              │
│  POST /auth/enroll-2fa    POST /admin/users/{id}/reset-2fa   │
│  POST /auth/verify-enroll POST /admin/.../send-2fa-reminder  │
│  GET /auth/2fa-status     POST /admin/.../generate-bypass    │
│                           GET /admin/audit/2fa               │
│                           PUT /admin/settings/require-2fa    │
│                           PUT /admin/settings/delegate-reset │
└──────────────────────────────────────────────────────────────┘
```
