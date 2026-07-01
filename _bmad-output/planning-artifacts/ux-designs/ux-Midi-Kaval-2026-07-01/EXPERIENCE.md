---
name: Kaval Online — 2FA Universal Enrollment & Administration
status: final
sources:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-07-01/prd.md
updated: 2026-07-01
---

# Kaval Online — 2FA Experience Spine

> Extends the existing Kaval Online Angular Material design system with 2FA-specific screens, flows, and components. DESIGN.md is the visual identity reference; this spine owns behavior, IA, states, interactions, and accessibility.

## Foundation

**Form factor:** Single-surface responsive web (Angular 19, Material). Extends the existing application — no new surface or platform.

**UI system:** [Angular Material](https://material.angular.io) with the project's existing theme. All new components use Material primitives (`MatTable`, `MatMenu`, `MatSlideToggle`, `MatDialog`, `MatSnackBar`, `MatTooltip`, `MatButton`, `MatFormField` + `MatInput`, `MatCheckbox`, `MatProgressSpinner`, `MatPaginator`, `MatSort`, `MatSelect`, `MatDatepicker`). The 80% of visual design inherits from the existing theme; this spine specifies only the behavioral delta and 2FA-specific component patterns.

## Information Architecture

### New & Modified Surfaces

| Surface | Route | Role | Type | Purpose |
|---------|-------|------|------|---------|
| **Vendor Settings** | `/vendor/settings` | Vendor | New | 2FA enrollment + password change for Vendor role |
| **Generic 2FA Enrollment** | `/settings/2fa` | All non-Vendor | New | 2FA enrollment for all other roles (triggered by org mandate or self-service) |
| **Staff Management** | `/admin/staff` | Director, Coordinator | Extended | New "2FA" column with contextual MatMenu actions |
| **Organisation Settings** | `/admin/settings` | Director | Extended | Two new toggles: Require 2FA + Delegate reset |
| **2FA Audit Log** | `/admin/audit/2fa` | Director | New | Filterable 2FA-specific audit log view |
| **Login Flow** | `/login` → ... | All | Extended | Post-login redirect guard for unenrolled users; backup code fallback on TOTP screen |
| **Profile** | `/settings` | All | Extended | Backup codes warning banner when < 3 remain |

### Navigation Structure

```
App Shell
├── Vendor Portal (guarded by VendorGuard)
│   └── /vendor/settings ← NEW
│       ├── 2FA Enrollment Card
│       └── Password Change (existing)
│
├── Admin Area (guarded by DirectorGuard)
│   ├── /admin/staff ← EXTENDED: 2FA column
│   ├── /admin/settings ← EXTENDED: 2FA toggles
│   └── /admin/audit/2fa ← NEW
│
├── Settings (guarded by AuthGuard)
│   ├── /settings/2fa ← NEW (enrollment page)
│   └── /settings ← EXTENDED (backup codes banner)
│
└── Login (guarded by GuestGuard)
    ├── /login
    ├── /login/otp
    └── /login/totp ← EXTENDED: "Use a backup code" link
```

### IA Closure

Every stated need from the PRD has a surface:
- **Vendor enrollment** → `/vendor/settings`
- **Non-Vendor enrollment** → `/settings/2fa`
- **Director team monitoring** → `/admin/staff` (2FA column)
- **Director org policy** → `/admin/settings` (toggles)
- **Director 2FA audit** → `/admin/audit/2fa`
- **Backup code management** → `/settings` (banner) + enrollment success screen
- **Emergency recovery** → Staff management → MatMenu → dialogs

## Voice and Tone

Microcopy for 2FA flows. The product voice is **clear, direct, and security-conscious without being alarming.** Brand voice and aesthetic posture live in `DESIGN.md.Brand & Style`.

### Principles

| Principle | Why |
|-----------|-----|
| **Explain what's happening** | Security actions feel risky if unexplained. Every dialog, banner, and error message says *what* is happening and *why.* |
| **Use plain language** | Avoid acronyms (TOTP, 2FA) in user-facing copy — say "authentication code" and "two-factor authentication" on first mention, then abbreviate. |
| **No false urgency** | "Set up now" not "Set up immediately or your account will be locked." The mandate toggle already enforces policy. |
| **Action > description** | Buttons say what they do ("Reset 2FA", "Generate Code", "Download Codes") not generic labels ("Submit", "Confirm"). |

### Microcopy Samples

| Context | Copy | Notes |
|---------|------|-------|
| First-time gate banner | *"Two-factor authentication is required to access vendor features."* | Clear reason + action |
| CTA not enrolled | **"Enable Two-Factor Authentication"** | Card title, button label |
| TOTP input prompt | *"Enter the 6-digit code from your authenticator app."* | Tells user where to look |
| Invalid TOTP | *"The code you entered is incorrect. Try again."* | No blame, clear next step |
| Expired session | *"Your enrollment session has expired. Please start again."* | No technical jargon |
| Reset confirmation | *"This will clear [user]'s 2FA enrollment. They will need to re-enroll on their next login."* | States consequence + action |
| Bypass code dialog | *"This code expires in 30 minutes and can only be used once. Share it securely with the user."* | Time bound + security warning |
| Backup codes title | *"Your Backup Codes"* | Simple, scannable |
| Reminder sent toast | *"Reminder sent to [user email]."* | Confirms action + who |
| Low backup codes | *"You have [N] backup code(s) remaining. Consider re-enrolling 2FA to generate new codes."* | States count + recommendation |
| Enrollment notification | *"[User full name] has enabled two-factor authentication."* | Passive, informative |

## Component Patterns

Behavioral specs. Visual specs (colors, sizing) live in `DESIGN.md.Components` or inherit from Angular Material defaults.

### Enrollment Card (Vendor Settings + Generic)

| Aspect | Spec |
|--------|------|
| **States** | Idle → Loading (QR generation spinner) → Ready (QR + key + input) → Verifying (button spinner) → Success (backup codes) → Error (inline message) |
| **Layout** | Single card. Top: QR code (left) + manual key with "Copy Key" (right). Bottom: 6-digit input + "Verify & Enable" button. |
| **QR generation** | Show `MatProgressSpinner` while provisioning URI is fetched. QR renders at min 200x200px via `qrcode` library. Light/dark theme detection: query `prefers-color-scheme` and apply dark module color if active. |
| **Manual key** | Display as monospace text in a non-editable `MatFormField` with `MatInput` (readonly). Adjacent "Copy Key" `MatButton` copies to clipboard with brief tooltip feedback ("Copied!"). |
| **TOTP input** | Single `MatFormField` with 6-digit `MatInput`, centered text, monospace font, `maxlength=6`, `inputmode="numeric"`. Auto-submit on 6 characters entered OR on button click. |
| **Error display** | Inline `MatError` below the input field. Red text, icon (warning triangle) + message. Two variants: invalid code, expired session. |
| **Success transition** | Card content smoothly replaces with backup codes display (no page navigation). Use Angular animation (fade, 300ms). |

### 2FA Column (Staff Management Table)

| Aspect | Spec |
|--------|------|
| **Cell rendering** | Icon-only: ✓ (green `check_circle` icon) or ✗ (red `cancel` icon). Icon centered in cell, no label text to minimize column width. |
| **Hover tooltip** | ✓ cell: "Enrolled on [date]" via `MatTooltip`. ✗ cell: "Not enrolled" via `MatTooltip`. |
| **Click → MatMenu** | Clicking the icon cell opens a `MatMenu` positioned to the right of the icon. Menu items vary by state and permissions (see table below). |
| **Enrolled state menu** | "Reset 2FA" — opens confirmation `MatDialog`. "Generate Bypass Code" — opens bypass code `MatDialog`. |
| **Not enrolled state menu** | "Reset 2FA" — opens confirmation `MatDialog`. "Send 2FA Reminder" — action fires immediately + `MatSnackBar` confirmation. |
| **No permission state** | Both icon and menu are hidden entirely. The column still occupies space (maintains table alignment) but shows an empty cell. |
| **Coordinator (delegated)** | Coordinator sees "Reset 2FA" in MatMenu for SocialWorker/CaseWorker rows only. No "Generate Bypass Code" option. |

### Confirmation Dialog (Reset 2FA)

| Aspect | Spec |
|--------|------|
| **Title** | "Reset Two-Factor Authentication" |
| **Body** | *"This will clear [user full name]'s 2FA enrollment. They will need to re-enroll on their next login. Continue?"* |
| **Buttons** | `[Cancel]` (secondary, default focus) | `[Reset 2FA]` (primary, red/warning color) |
| **Dismiss** | Escape key closes dialog without action. Click outside closes. |
| **Post-confirm** | Dialog closes. Success shown via `MatSnackBar`: *"[User]'s 2FA has been reset."* If bypass code generation follows, see Bypass Code Dialog. |

### Bypass Code Dialog

| Aspect | Spec |
|--------|------|
| **Title** | "Temporary Bypass Code" |
| **Code display** | Large monospace text in a box. Adjacent "Copy" `MatButton`. |
| **Warning text** | Below the code: *"This code expires in 30 minutes and can only be used once. Share it securely with the user."* |
| **Buttons** | `[Close]` (primary) — closes dialog. Code is no longer accessible after closing. |
| **Dismiss** | Escape key closes. |
| **Post-close** | Code cannot be viewed again. Logged in audit. |

### Organisation Settings Toggles

| Aspect | Spec |
|--------|------|
| **"Require 2FA for all staff"** | `MatSlideToggle` with label text. Tooltip: *"When enabled, users without two-factor authentication will be prompted to set it up on their next login."* Tooltip icon next to label for discoverability. |
| **"Allow Coordinators to reset 2FA"** | `MatSlideToggle` with label text. Tooltip: *"Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."* |
| **State persistence** | Toggle writes immediately on change (`PUT /admin/settings`). Show brief loading state during save. On failure, revert toggle to previous state + `MatSnackBar` error. |

### Backup Codes Display (Enrollment Success)

| Aspect | Spec |
|--------|------|
| **Layout** | Title: "Your Backup Codes" with explanatory text: *"Each code can only be used once. Store these in a secure place. If you lose access to your authenticator app, use one of these codes to log in."* |
| **Code rendering** | 8 codes displayed as a 2-column grid of monospace chips. Each chip: code text on a subtle grey background. |
| **Download button** | "Download as .txt" `MatButton` with download icon. Produces a plain text file. |
| **Close/dismiss** | No close button — this screen must be intentionally progressed through. "Continue" button at bottom confirms the user has saved their codes. After clicking, codes are never shown again. |

### Low Backup Codes Banner (Profile)

| Aspect | Spec |
|--------|------|
| **Trigger** | When `GET /auth/2fa-status` returns `backupCodesRemaining < 3`. |
| **Position** | Top of `/settings` page, above all other content. |
| **Appearance** | Warning banner with icon. Background tint, border, icon in warning color. |
| **Text** | *"You have [N] backup code(s) remaining. Consider re-enrolling two-factor authentication to generate new codes."* |
| **Action** | "Re-enroll 2FA" link/button → navigates to `/settings/2fa` enrollment flow. |
| **Dismiss** | Banner is dismissible with an × button. Does not reappear until page reload or the count decreases further. |

### In-App Notification (2FA Enrollment)

| Aspect | Spec |
|--------|------|
| **Delivery** | Existing in-app notification system (bell icon in top nav). |
| **Body** | *"[User full name] has enabled two-factor authentication."* |
| **Icon** | Security/shield icon (consistent with 2FA visual language). |
| **Action** | Clicking the notification navigates to Staff Management page scrolled/filtered to that user. |
| **Dismiss** | Click notification body → marks as read + navigates. × button → marks as read. |

### Backup Code Fallback (Login TOTP Screen)

| Aspect | Spec |
|--------|------|
| **Trigger** | Appears only after 1 failed TOTP attempt OR immediately if user has backup codes and selects "Use a backup code instead." |
| **Placement** | Below the TOTP input field. Link text: *"Use a backup code instead"* |
| **State transition** | Clicking the link replaces the TOTP input with a backup code input (alphanumeric, 10 chars) + "Verify" button. A "Back to TOTP" link is also shown. |
| **Validation** | Same rate limiting as TOTP (5 attempts/min). On success, mark code as used, grant access, show banner: *"Backup code used. You should re-enroll two-factor authentication to generate new codes."* |

## State Patterns

| State | Surface | Treatment |
|-------|---------|-----------|
| **Loading (QR gen)** | Enrollment card | `MatProgressSpinner` centered in QR placeholder area. Label: "Generating your code..." |
| **Loading (verify)** | Enrollment card | Button shows `MatProgressSpinner` inline, label changes to "Verifying..." |
| **Loading (toggle save)** | Org settings | Toggle shows indeterminate state; `MatProgressSpinner` tiny |
| **Empty / not enrolled** | 2FA column | `cancel` icon, red color, tooltip "Not enrolled" |
| **Enrolled success** | 2FA column | `check_circle` icon, green color, tooltip "Enrolled on [date]" |
| **Enrolled success** | Vendor settings badge | Badge: "Two-Factor Authentication: Enabled ✓" with enrollment date. "Disable" link below. |
| **Error — invalid code** | Enrollment card | `MatError`: "The code you entered is incorrect. Try again." Red text, warning icon. Input field remains populated. |
| **Error — expired session** | Enrollment card | `MatError`: "Your enrollment session has expired. Please start again." Button changes to "Start Over." |
| **Error — rate limited** | Any verify endpoint | `MatSnackBar`: "Too many attempts. Please wait and try again." |
| **Error — network failure** | Any surface | `MatSnackBar`: "Something went wrong. Please try again." Consistent with existing app pattern. |
| **Warning — low backup codes** | Profile | Themed warning banner, dismissible, with CTA |
| **Warning — first-time gate** | Post-login redirect | Banner at top of enrollment page: "Two-factor authentication is required to access vendor features." |
| **Toggle ON** | Org settings | `MatSlideToggle` in checked position, writes immediately |
| **Toggle OFF** | Org settings | `MatSlideToggle` in unchecked position, writes immediately |
| **Toggle save failure** | Org settings | Toggle reverts to previous state, `MatSnackBar` error appears |
| **Bypass code generated** | Bypass dialog | Code displayed in large monospace. "Copy" button visible. Warning text below. |
| **Bypass code expired** | (in transit) | No UI for this — the code simply fails validation at the API. |
| **Delegation enabled** | Coordinator view | "Reset 2FA" appears in MatMenu for SocialWorker/CaseWorker only |
| **Delegation disabled** | Coordinator view | MatMenu items disappear immediately (no page reload needed) |
| **No permission** | 2FA column cell | Cell visually empty (no icon, no menu) — column space preserved for alignment |

## Interaction Primitives

| Primitive | Pattern | Rationale |
|-----------|---------|-----------|
| **MatMenu trigger** | Click (not hover) | Hover menus are prone to accidental opens on a sensitive action like 2FA reset. Click is deliberate. |
| **Confirmation dialogs** | Escape key closes without action | Standard Material pattern. Prevents accidental resets. |
| **Destructive action buttons** | Red/warning color on confirm button | Visually signals "this action has consequences." Cancel button gets default focus to prevent accidental Enter-press. |
| **Bypass code copy** | Click → copies + brief tooltip "Copied!" | Silent copy (no `MatSnackBar`) to avoid drawing attention to the code on screen. Tooltip auto-dismisses. |
| **TOTP auto-submit** | On 6th character entered | Reduces friction — user doesn't need to reach for the mouse or tab to button. Falls back to manual button click for accessibility. |
| **Toggle immediate save** | On change, no "Save Settings" button | Follows Material design guidance for toggles — immediate action, no secondary confirmation. |
| **Banner dismiss** | × button | Low-stakes dismiss. Content is informational, not critical. |
| **Routing guard redirect** | Automatic, no user action | Enrolled user → normal route. Unenrolled user → pushed to enrollment with explanatory banner. User must complete enrollment before accessing protected routes. |

## Accessibility Floor

| Requirement | Implementation |
|------------|---------------|
| **Keyboard navigation** | All dialogs, menus, and forms are fully keyboard-operable (Tab, Enter, Escape) via Angular Material defaults. QR copy button focusable and activatable with Enter/Space. |
| **Screen reader labels** | QR image: `alt="Two-factor authentication QR code. Scan this with your authenticator app."` 2FA column icon: `aria-label="2FA enrolled"` / `aria-label="2FA not enrolled"`. MatMenu trigger: `aria-label="2FA actions"`. |
| **Color contrast** | All 2FA status indicators use both color AND icon (✓ green `check_circle` / ✗ red `cancel`) — never color alone. Backup codes use monospace for visual distinction. |
| **Focus management** | When a `MatDialog` opens, focus moves to the Cancel button (safe default). When dialog closes, focus returns to the trigger element. |
| **Error announcements** | Inline `MatError` messages are announced by screen readers via `aria-live="polite"`. |
| **Motion sensitivity** | All animations use `prefers-reduced-motion` media query. Disable entrance animations (fade, slide) when reduced motion is preferred. QR fade-in becomes instant. |
| **Touch targets** | All interactive elements (MatMenu items, toggle, copy button, icons) meet 48x48px minimum touch target. |
| **Form labels** | TOTP input has visible `<label>`. Backup code input has visible `<label>`. Placeholder is not a substitute for a label. |

## Key Flows

### Flow 1: Vendor Self-Enrollment (UJ-1)

**Protagonist:** Meena, system administrator at a vendor organisation.

```
1. Meena opens Kaval Online → `/login`
2. Enters email + password → clicks "Sign In"
3. Receives email OTP → enters code → clicks "Verify"
4. Login response returns `requires2faSetup: true, setupUrl: "/vendor/settings"`
5. Angular auth guard intercepts → redirects to `/vendor/settings`
6. **Climax:** Meena sees a banner: "Two-factor authentication is required to access vendor features."
   Below: "Enable Two-Factor Authentication" card with QR code + manual key + "Copy Key"
7. Meena opens Google Authenticator on her phone → scans QR code
8. Enters 6-digit code → clicks "Verify & Enable"
9. Success: Badge updates to "2FA: Enabled ✓". Backup codes screen appears.
10. Meena downloads backup codes as .txt, clicks "Continue"
11. Redirected to `/vendor` dashboard — full access granted.
```

**Failure path:** If Meena navigates away (e.g., closes tab) before step 8, the route guard catches her on next visit and redirects back to `/vendor/settings`. She cannot access any other vendor page.

### Flow 2: Director Managing Team 2FA (UJ-2)

**Protagonist:** Raj, Director overseeing 15 field workers.

```
1. Raj logs in (already enrolled in 2FA) → standard TOTP login
2. Opens Staff Management → sees new "2FA" column
3. Scans column: 12 ✓ (green), 3 ✗ (red)
4. Hovers a ✓ → tooltip: "Enrolled on June 15, 2026"
5. Clicks a ✗ cell → MatMenu opens with "Reset 2FA" + "Send Reminder"
6. Clicks "Send Reminder" → MatSnackBar: "Reminder sent to anita@example.org"
7. **Climax:** Raj navigates to Organisation Settings → toggles "Require 2FA for all staff" ON
   → Toggle saves immediately → tooltip on toggle explains behavior
8. Next time an unenrolled user logs in, they are redirected to enrollment.
```

### Flow 3: Field Worker Lockout Recovery (UJ-3)

**Protagonist:** Raj (Director) helping Priya (SocialWorker) who got a new phone.

```
1. Raj opens Staff Management → finds Priya → clicks ✓ cell → MatMenu
2. Selects "Reset 2FA" → confirmation dialog appears
3. Raj reads: "This will clear Priya Sharma's 2FA enrollment..."
4. Clicks "Reset 2FA" → dialog closes → MatSnackBar: "Priya Sharma's 2FA has been reset."
5. **Climax:** MatMenu is still open or Raj re-opens it → now shows "Generate Bypass Code"
6. Clicks "Generate Bypass Code" → dialog: large monospace code + "Copy" button
7. Warning text: "This code expires in 30 minutes and can only be used once..."
8. Raj copies code, shares over the phone with Priya → closes dialog
9. Priya logs in with email + password + bypass code → prompted to re-enroll 2FA
10. Priya re-enrolls with her new phone → new backup codes generated.
```

### Flow 4: Coordinator Reset (Delegated) (UJ-4)

**Protagonist:** Rahul, Coordinator, after Raj enabled delegation.

```
1. Rahul opens Staff Management → sees "Reset 2FA" button next to SocialWorker/CaseWorker users
2. (Before delegation: no button visible at all)
3. Clicks "Reset 2FA" for Anita → confirmation dialog
4. Confirms → Anita's 2FA cleared
5. Rahul does NOT see "Generate Bypass Code" in any menu
6. Later, Raj toggles delegation OFF → Rahul's view updates immediately
   → "Reset 2FA" buttons disappear without page reload
```

### Flow 5: Backup Code Login (Fallback)

**Protagonist:** Any enrolled user who lost their authenticator app.

```
1. User enters email + password at login
2. Enters email OTP → verified
3. TOTP screen appears → user types 6-digit code → fails (wrong app, new phone)
4. Below input: "Use a backup code instead" link appears
5. **Climax:** User clicks link → TOTP input replaced by backup code input
6. User enters backup code → "Verify" → success → logged in
7. Banner: "Backup code used. You should re-enroll two-factor authentication to generate new codes."
8. User navigates to profile → sees low backup codes warning banner.
```

---

→ Composition reference: `mockups/` contains 1:1 HTML mocks for key screens. Spine wins on conflict.

### Mock Coverage

| Mock | File | Illustrates |
|------|------|-------------|
| Vendor Settings — Enrollment Card | `mockups/key-vendor-settings.html` | FR-1.2–1.5, First-time banner, QR + manual key + TOTP verify, Password change section, Flow 1 |
| Staff Management — 2FA Column | `mockups/key-staff-2fa.html` | FR-2.1, MatMenu per cell state, Tooltip on hover, ✓/✗ icons with colors, Legend |
| Reset Confirmation + Bypass Code Dialogs | `mockups/key-dialogs.html` | FR-2.2, FR-2.5, Confirmation dialog with Cancel/Reset, Bypass code display with Copy + warning, Flow 3 |
| Backup Codes Display + Low Codes Warning | `mockups/key-backup-codes.html` | FR-3.4, FR-4.6, 2-column code grid, Download .txt button, Warning banner on profile, Flow 1 (steps 9-10), Flow 5 |
| Organisation Settings — 2FA Toggles | `mockups/key-org-settings.html` | FR-2.4, FR-2.7, Require-2FA toggle ON, Delegate toggle ON/OFF states, Tooltips, Flow 2 (step 6-7) |
