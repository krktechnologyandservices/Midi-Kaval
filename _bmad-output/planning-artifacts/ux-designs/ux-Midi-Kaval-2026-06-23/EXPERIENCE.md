---
name: Midi-Kaval Role Management
status: final
updated: 2026-06-23
sources:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-23/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md
---

# Midi-Kaval Role Management — Experience Spine

> Multi-surface: web (Angular PWA + Angular Material) for admin surfaces; React Native mobile for Director on-the-go. `DESIGN.md` owns visual tokens (navy enterprise skin); this spine owns behavior. **Spines win on conflict** with mockups.

## Foundation

| Surface | Users | Stack |
|---------|-------|-------|
| Web — Vendor Backstage | Vendor (Midi-Kaval team) | Angular PWA + Angular Material, restrictive-access page |
| Web — Director Dashboard | Director | Angular PWA + Angular Material, sidebar nav |
| Web — Activation / Registration | First Director (via email link) | Angular PWA, standalone page (no shell/sidebar) |
| Web — Invitation landing | Invited user (Coordinator / Field Worker) | Angular PWA, standalone page |
| Mobile — Director Companion | Director | React Native |

The admin surfaces use {DESIGN.md.colors.primary} navy enterprise skin, deliberately distinct from the main Kaval teal field app. The activation and invitation landing pages use a simplified version of this skin — no sidebar, minimal chrome — to reduce cognitive load for first-time users.

## Information Architecture

### Web — Vendor Backstage

| Surface | Reached from | Purpose |
|---------|--------------|---------|
| Organisation Roster | Vendor dashboard home | List all orgs; status (active/inactive); bootstrap new org |
| Org Detail | Roster row click | Org info, current Directors, activation status, resend activation link, safety-net reset |

The Vendor Backstage is not in the main app shell. It is a separate, restricted-access page with its own login (2FA-mandated per FR-10). Navigation is a minimal top bar (org name, support link, logout).

### Web — Director Admin

| Surface | Reached from | Purpose | Epic/Story |
|---------|--------------|---------|------------|
| Dashboard (Team Roster) | Sidebar → "Team" or app open | User list with statuses, role filters, search | Epic 3 |
| User Detail | Roster row click (slide-in Sheet) | User profile, status, role, action buttons (suspend/reactivate) | Epic 3 |
| Invite User | Dashboard → "Invite" button | Role+email form, confirmation, audit preview | Epic 4 |
| Invitation History | Sidebar → "Invitations" | Table of sent invitations with status (pending/confirmed/expired) | Epic 4 |
| Audit Log | Sidebar → "Audit Log" | Chronological event list, filterable by user/event-type/date | Epic 6 |
| Settings (Profile) | Avatar menu → "Settings" | 2FA enrollment/management, personal details | Epic 5 |
| Login / 2FA | App launch | Email/password + TOTP code (Director mandate) | Epic 5 |

### Web — Standalone Pages (no shell)

| Surface | Reached from | Purpose | Epic |
|---------|--------------|---------|------|
| Activate Organisation | Activation link in email | Director sets name, password, enrolls 2FA | Epic 1 |
| Accept Invitation | Invitation link in email | Invited user sets password, verifies details | Epic 4 |
| Safety Net Reset | Vendor-initiated reset link | Existing Director-less org gets new activation | Epic 2 |

### Mobile — Director Companion

| Surface | Reached from | Purpose |
|---------|--------------|---------|
| Team Summary | App open | Quick roster view: total active, pending invites, alerts |
| User Quick Actions | Roster tap | Suspend, reactivate, view status (no full detail/editing) |
| Pending Invitations | Tab / section | See pending invites, resend or revoke |
| Notifications | Bell | New registration alerts, invitation confirmations, suspension notices |

Mobile is a companion for urgent/on-the-go actions only. Full team management requires web.

## Voice and Tone

Enterprise admin surfaces. Microcopy is **neutral, precise, and confidence-inspiring**. Brand voice lives in `DESIGN.md.Brand & Style`.

| Do | Don't |
|----|-------|
| "2 pending invitations" | "You've got invites waiting!" |
| "User suspended. They will be logged out within 60 seconds." | "User deactivated." |
| "Enter the 6-digit code from your authenticator app." | "Enter OTP" |
| "This action will be logged and visible to the organisation audit." | "This will be recorded." |
| "No active users in this organisation." | "There are no users sadface" |
| "Are you sure? This will permanently remove this user's access." | "Are you sure?" |
| "Last active: 2 hours ago" | "Active recently" |
| "No matching users. Try adjusting your filters." | "0 results" |

**Error messages** follow the pattern: what happened + what the user can do next.

| Situation | Message |
|-----------|---------|
| Invite email bounces | "Invitation could not be delivered to {email}. Verify the address and resend." |
| User tries to deactivate last Director | "Cannot deactivate — at least one active Director is required. Assign another Director first." |
| 2FA code invalid | "Invalid code. Check your authenticator app and try again." |
| Activation link expired | "This link has expired. Contact your organisation administrator for a new invitation." |

## Component Patterns

Behavioral. Visual specs live in `DESIGN.md.Components`. Inherited Angular Material components (MatButton, MatDialog, MatBottomSheet, MatTable, MatSnackBar, MatBadge, MatInput, MatSelect, MatMenu, MatTabs, MatChips, MatSidenav, MatDivider, MatProgressSpinner, MatSort, MatPaginator) follow Material behavioral contracts unless overridden here.

| Component | Surface | Behavioral rules |
|-----------|---------|------------------|
| Team Roster Table | Dashboard | Columns: Name, Email, Role, Status (badge), Last Active, Actions (kebab menu). Sortable by name, role, status, last active. Filterable by role and status. Click row → User Detail sheet. |
| User Detail Sheet | Dashboard | Slides in from right. Sections: Profile (name, email, role pill), Status (active badge + toggle), Activity (last active, member since), Actions (suspend/reactivate, change role — Director only). Sheet closes on Esc or backdrop click. |
| Invite Dialog | Dashboard | Modal dialog. Email input, Role select (Coordinator / Field Worker), optional message. "Send Invitation" primary button. Preview section shows what the user will receive. Double-confirmation per decision log: after submit, shows confirmation card with invitation details before final send. |
| Invitation Table | Dashboard | Columns: Email, Role, Status (badge), Sent, Expires, Actions (resend/revoke). Status badges: Pending (amber), Confirmed (green), Expired (gray). |
| Audit Event Row | Audit Log | Event type icon + label, Actor name, Target name, timestamp, IP address. Expand row → full detail: event metadata, target snapshot (JSON), IP. Filters: event type dropdown, date range picker, user search. |
| Activation Page (standalone) | Activate Organisation | Centered card layout. Step indicator (1/3, 2/3, 3/3). Steps: 1) Verify email, 2) Set name + password, 3) Enroll 2FA. Progress saved server-side; link reentry resumes at the incomplete step. |
| Invitation Acceptance (standalone) | Accept Invitation | Centered card. Pre-filled email (read-only from token). Name fields. Password set with strength indicator. No 2FA (Coordinator/Field Worker don't require it initially per FR-10 scope). |
| Status Toggle | User Detail Sheet | Toggle switch for suspend/reactivate. Toggle + confirmation dialog before action commits. Toggle is optimistic — updates UI immediately, rolls back on failure. |
| Safety Net Banner | Org Detail (Vendor) | "This organisation has no active Directors. Send a new activation link to resume operations." Prominent banner, top of page, dismissible only after sending new link. |

## State Patterns

| State | Surface | Treatment |
|-------|---------|-----------|
| Empty team roster | Dashboard | "No team members yet. Invite your first Coordinator or Field Worker." Single CTA: "Invite Team Member" |
| Empty invitations | Invitation History | "No invitations sent yet." |
| Empty audit log | Audit Log | "No events recorded in this date range." |
| Loading roster | Dashboard | `MatProgressSpinner` (indeterminate) in table overlay or `MatTable` skeleton via `loading` template. |
| Loading user detail | Sheet | Skeleton-style placeholders using `MatProgressSpinner` + text blocks. |
| Searching roster | Dashboard | Debounced 300ms input. Show loading spinner in search input. Results update in-place. |
| Invitation pending | Invitation History | Amber badge. See countdown to expiry. |
| Invitation expired | Invitation History | Gray badge. "Expired {N} days ago." Resend action available. |
| User suspended | Roster + Sheet | Red badge in table. Sheet shows "Suspended since {date}" with Reactivate toggle. Suspended users cannot log in. |
| User active | Roster + Sheet | Green badge. Sheet shows "Active — last login {time}" with Suspend toggle. |
| Last Director protection | Roster | Suspend toggle disabled on last active Director. Tooltip: "At least one Director must remain active." |
| Expired activation link | Activation Page | "This link has expired. Contact the Vendor to request a new activation link." No other actions. |
| Invalid/expired invitation link | Invitation Acceptance | "This invitation is no longer valid. Ask your Director to send a new one." |
| 2FA enrollment in progress | Settings / Activation | Step-by-step: 1) Show QR code + manual key, 2) User scans in authenticator app, 3) User enters 6-digit code to verify. Errors clear on new code entry. |
| Force logout on suspension | All surfaces | User's active session invalidated. Next action redirects to login with toast: "Your account has been suspended. Contact your Director." |
| API error on action | Any | `MatSnackBar` with `panelClass: ['snackbar-error']`: "{Action} failed. {Reason}. Try again." |
| Network offline | Mobile Director | Banner at top: "You're offline. Changes will sync when reconnected." Local reads work; mutations held for retry. |
| Session expired | All | Redirect to login. Toast: "Your session has expired. Please log in again." |

## Interaction Primitives

**Web (admin surfaces):**
- Click table row → open detail sheet. Click outside sheet or Esc → close.
- Primary action buttons follow reading order (left-to-right: Cancel, Confirm).
- Toggle switches commit immediately after confirmation dialog — no "Save" button on the sheet.
- Search input on roster uses debounce (300ms) — no submit button.
- Table column headers are click-to-sort. Active sort indicator shows arrow.
- Invitation double-confirmation: first submit opens confirmation card with summary; second submit sends.
- Multi-select filters (role, status) use `MatSelect` with `multiple="true"`.
- Pagination on audit log — 20 events per page. "Load more" at bottom.

**Banned everywhere:**
- Infinite scroll on tables (use pagination)
- Drag-to-reorder in v1
- Hover-only affordances on touch widths
- Modal stacks > 1 level deep
- Auto-save without user awareness
- Silent failures (every mutation shows a result)

**Mobile (Director Companion):**
- Tap roster row → quick action sheet (not full detail).
- Pull-to-refresh on roster and invitations.
- Swipe to dismiss notifications.
- Bottom tab bar: Team · Invitations · Notifications · More.

## Accessibility Floor

Behavioral. Visual contrast lives in `DESIGN.md` (inherits Angular Material WCAG AA-compliant defaults; brand palette overrides verified).

- WCAG 2.2 AA across all web surfaces.
- Screen reader announces page/surface on navigation: "Team Roster — {N} members" / "Audit Log — {M} events, page {P}."
- Status badges **always include text label** — never rely on colour alone. Badge reads: "Active" + `aria-label="Status: Active"`.
- Table rows are keyboard-navigable (Tab through rows, Enter/Space to open detail).
- Sortable column headers announce sort state: "Name, sorted ascending" / "Name, not sorted."
- Invite dialog and confirmation card are focus-trapped; return focus to the "Invite" trigger on close.
- Sheet detail is focus-trapped; `aria-modal="true"` when open.
- Activation and invitation pages are single-column, keyboard-operable from top to bottom.
- Step indicators in activation flow announce current step: "Step 2 of 3: Set your password."
- Toggle switches for suspend/reactivate announce state: "Active. Press Space to suspend."
- Toast notifications use `role="alert"` and `aria-live="assertive"`.
- All form inputs have associated labels (not placeholders as labels).
- Error messages are linked to inputs via `aria-describedby`.
- Colour contrast ratios verified for all status badges per WCAG 2.1 AA.

## Responsive & Platform

| Breakpoint | Web behavior |
|------------|--------------|
| `≥ 1280px` | Sidebar + full table width. Audit log shows 6+ columns. |
| `1024–1279px` | Sidebar collapsed to icons. Tables remain full width. |
| `768–1023px` | Sidebar becomes Sheet (hamburger). Tables scroll horizontally. Detail sheet slides over full viewport. |
| `< 768px` | Warning banner: "For full team management, use a desktop browser." Read-only roster view with essential actions. |

Activation and invitation pages are responsive single-column layouts — they work on any screen size since users may open links on mobile.

Mobile Director Companion surfaces are React Native, separate responsive treatment per platform.

## Inspiration & Anti-patterns

- **Lifted from:** enterprise admin dashboards (Stripe Dashboard, Clerk user management) — the table-as-primary-interface pattern, slide-in detail sheets, status badge vocabulary.
- **Lifted from:** bank-portal UX — navy primary, restrained palette, conrmation on destructive actions, logged-everywhere transparency.
- **Lifted from:** Angular Material `MatBottomSheet` and `MatDialog` patterns for detail views and modals.
- **Rejected — Gamified team management:** no activity streaks, no "top inviter" badges, no celebration animations on user registration.
- **Rejected — Chat-based team management:** no in-app chat for Directors. All communication is email-based (invitations, notifications).
- **Rejected — Self-serve registration:** no public signup. Every user enters via invitation or activation link.
- **Rejected — Decorated empty states:** empty states are informative and actionable, not whimsical illustrations.

## Key Flows

### Flow 1 — Vendor bootstraps a new organisation (Grace, Vendor Operations Lead)

1. Grace logs into the Vendor Backstage (2FA-mandated per FR-10).
2. Organisation Roster loads — she sees all active orgs with status badges.
3. She clicks "New Organisation", enters the trust name and the Director's work email.
4. System creates the org (inactive) and generates a one-time activation link.
5. **Climax:** Grace sees the confirmation: "Activation link sent to {email}. The organisation will be active once the Director completes registration." The org row appears in the roster with a "Pending Activation" badge. Grace can resend the link or cancel the pending org.

**Failure:** Email delivery fails → in-line error on the confirmation screen: "Email could not be delivered. Verify the address and try again." The activation token remains valid; Grace can retry from the org detail page.

### Flow 2 — Director self-registers via activation link (James, NGO Executive Director)

1. James receives the activation email on his phone. The email is minimal: his name, the organisation name, and a "Activate Your Account" button.
2. He opens the link. The Activation page loads — centred card, no sidebar, no chrome. Step 1 of 3: email pre-filled and confirmed.
3. Step 2: James enters his full name, sets a password (strength indicator visible), accepts terms.
4. Step 3: QR code appears for 2FA enrollment. He opens his authenticator app, scans the code, enters the 6-digit code to verify.
5. **Climax:** "Your organisation is active. Welcome to Kaval." The organisation status flips to active. James lands on the Director Dashboard — empty team roster, "Invite Your First Team Member" CTA prominent. The journey from "I got an email" to "I can manage my team" is under 3 minutes.

**Failure:** James delays >48 hours → link expired. He sees the expired-link message and contacts Grace (Vendor). Grace resends from the Vendor Backstage with a new 48-hour token.

### Flow 3 — Director invites a new team member (James invites Priya as Coordinator)

1. James is on the Director Dashboard, team roster visible. He clicks "Invite User."
2. The Invite Dialog opens — modal, focus-trapped. Email input, Role dropdown (Coordinator / Field Worker), optional message field.
3. He selects "Coordinator" and enters Priya's email. Clicks "Send Invitation."
4. A confirmation card slides in — shows a summary: "Inviting Priya Sharma (priya@trust.org) as Coordinator. They will receive an email with a 24-hour invitation link. This will be logged."
5. James clicks "Confirm & Send."
6. **Climax:** Toast: "Invitation sent to priya@trust.org." The invitation appears in the Invitation History with a "Pending" badge and 23h59m countdown. The roster does not change (Priya hasn't registered yet). James can continue inviting or return to the roster.

**Failure 1:** Priya's email bounces → Toast (destructive): "Invitation could not be delivered. Verify the address." The invitation row shows a warning icon. James edits and resends.
**Failure 2:** Priya's link expires → badge turns gray. James opens the invitation row, clicks "Resend" — new 24-hour token, confirmation: "New invitation sent."

### Flow 4 — Director manages team status (James suspends a Field Worker)

1. James scrolls the team roster. He spots a Field Worker who has been inactive for 3 months.
2. He clicks the row → User Detail sheet slides in from the right. Profile section shows name, email, role pill. Status section shows "Active — last login 3 months ago."
3. He clicks the Suspend toggle. A confirmation dialog appears: "Suspend this user? They will lose access immediately. This action will be logged. You can reactivate at any time."
4. James confirms. The toggle flips to "Suspended." The sheet updates: "Suspended — {timestamp}. Reactivate available."
5. The roster row updates optimistically — green badge → red badge, "Suspended" label.
6. **Climax:** The suspended user's active sessions are invalidated. If they try any action, they see: "Your account has been suspended. Contact your Director."

**Edge case — Last Director:** James tries to suspend the only other Director. The toggle is disabled. Tooltip: "At least one Director must remain active. Promote another user to Director first." The action is blocked pre-confirmation.

**Edge case — Reactivation:** James clicks the Reactivate toggle. Confirmation: "Reactivate this user? They will regain access immediately." Confirms → badge flips to green, user receives a notification email.

### Flow 5 — Director reviews audit trail (James investigates a change)

1. James opens the Audit Log from the sidebar. The table loads with the last 20 events, most recent first.
2. Columns: timestamp, event type (icon + label), actor, target, IP address. He can filter by event type ("All types" dropdown) or date range.
3. He filters by "User Suspended" events in the last 7 days. Two events appear — one for the Field Worker he suspended, and one from another Director.
4. He expands the row from the other Director. The expanded detail shows: "Actor: Maria (Director) suspended Target: David (Field Worker). IP: 203.0.113.42. Target snapshot at time of event: { name: 'David Kim', email: 'david@trust.org', role: 'field_worker' }."
5. **Climax:** James has everything he needs for a compliance inquiry — who did what, when, from where, and what the state was at the time. No support ticket needed, no database query.

**Empty state:** No events in filter range → "No events recorded in this date range." Filters remain visible; clear filters CTA.

### Flow 6 — Vendor safety net (Grace recovers an org that lost all Directors)

1. Grace receives an alert or is contacted by an org that has no active Directors (both left the org).
2. She opens the Vendor Backstage, navigates to the org detail page.
3. A prominent banner: "No active Directors. Send a new activation link to resume operations."
4. Grace enters the new Director's email and clicks "Send Activation Link."
5. **Climax:** A new activation token is generated. The existing organisation record is preserved — users, invitations, and audit log remain intact. The new Director follows Flow 2 to register. On registration, the org becomes active again, and the new Director sees the existing team roster (Coordinator and Field Worker accounts are unaffected).

**Edge case:** The "last Director" left without handing over. The Vendor Backstage shows the org's last known Director name and the date they were last active, so Grace can verify the situation before issuing the reset.

### Flow 7 — Director manages invitations on mobile (James on the go)

1. James is at a grant meeting, waiting for a session to start. He opens the Director mobile app.
2. The Team Summary shows: 12 active, 2 pending invitations, 1 suspended.
3. He taps "Pending Invitations" → sees two pending invites. One was sent 20 hours ago — expiring in 4 hours.
4. He taps the expiring invitation → resend option. He taps "Resend."
5. **Climax:** Toast: "Invitation resent to anna@partner.org." The new 24-hour clock starts. James puts his phone away — no need to wait for desktop to manage his team.

## Open Questions

1. Third-party identity provider (Google SSO, Azure AD) — should the activation/invitation flow support social login for Coordinators and Field Workers, or is email-password sufficient for v1?
2. Bulk invitation — import a CSV of emails for large orgs, or is single-entry sufficient for v1?
3. Invitation link expiry duration — 24 hours is the default in these flows. Should this be configurable per-organisation?
4. Mobile Director Companion — is this part of the existing Kaval mobile app (new section) or a separate app?
5. Dark mode — `DESIGN.md` defines dark tokens. Should admin surfaces default to light mode (like main app) or follow system preference?
6. Notification channels — email-only for v1? Push notifications for mobile Director Companion?

## Assumptions Index

- [ASSUMPTION] Invitation links expire in 24 hours (configurable per-org in future).
- [ASSUMPTION] Activation links expire in 48 hours.
- [ASSUMPTION] Suspended users are force-logged out within 60 seconds of suspension action.
- [ASSUMPTION] The Director mobile companion is a tab/section within the existing Kaval mobile app, not a separate app.
- [ASSUMPTION] All admin surfaces default to light mode; dark mode follows system preference.
- [ASSUMPTION] Audit log retains events indefinitely (v1 — no retention policy UI).
- [ASSUMPTION] Role changes are immediate — no "pending approval" workflow for role elevation.
- [ASSUMPTION] User deletion (permanent) follows the anonymization model per FR-8: PII scrubbed, audit rows preserved.
- [ASSUMPTION] Invitation and activation emails are sent via the existing email service (SendGrid/SMTP per project-context.md).
- [ASSUMPTION] The team roster shows all users in the organisation, not paginated per-role — filtering/scoping by role is done via UI filters.
