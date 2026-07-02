# Story 25.6: Director Staff Management — 2FA Column, MatMenu Actions, Dialogs

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: done

baseline_commit: bbc03cf20619dd4f00c964a3d6041ed4a2712f48

## Story

As a **Director** (viewing the Team Roster page),
I want **the staff management table to include a "2FA" column with visual enrollment indicators and contextual actions (Reset 2FA, Send Reminder, Generate Bypass Code)**,
so that **I can see at a glance which users have 2FA enabled and take administrative actions without navigating to a separate detail view.**

## Acceptance Criteria

1. **"2FA" column added to the Team Roster table** in `team-roster.component.ts`:
   - Displayed between the "Role" and "Status" columns in the column order: `['name', 'email', 'role', '2fa', 'status', 'createdAtUtc', 'actions']`
   - Uses Material icon `check_circle` (green `#2E7D32`) when `totpEnrolledAt` is non-null
   - Uses Material icon `cancel` (red `#C62828`) when `totpEnrolledAt` is null or undefined
   - Icon cells are styled: centered, 40x40px, rounded, hover background (`#f0f0f0`) indicating interactivity
   - **Tooltip**: Use a component method `getEnrollmentTooltip(user)` that returns "Enrolled on [date]" (formatted with `toLocaleDateString`) for enrolled, "Not enrolled" for unenrolled — avoids Angular `date` pipe inside string interpolation. Bound via `[matTooltip]="getEnrollmentTooltip(u)"`.
   - **Deleted users** (`getUserStatus(user) === 'deleted'`): hide icon entirely, show dash `—` in gray
   - **Suspended users** (`getUserStatus(user) === 'suspended'`): Same as deleted — show dash `—` with no MatMenu interactivity. Suspended user management actions belong in the detail sheet, not the 2FA column.
   - Column header: "2FA", centered text, non-sortable (no `mat-sort-header` — `totpEnrolledAt` is not a server sort parameter)

2. **Clicking the 2FA icon opens a contextual MatMenu** attached to the icon cell:
   - **Enrolled users** (`totpEnrolledAt` non-null):
     - "Reset 2FA" — always visible (matches UX-DR6), `[disabled]="isLastDirectorUser(u)"` when user is the last active Director (matching existing pattern from the suspend button in the actions column). Disabled state shows tooltip: "At least one Director must remain active."
     - MatMenu divider
     - "Generate Bypass Code" — always visible
   - **Unenrolled users** (`totpEnrolledAt` null/undefined):
     - "Reset 2FA" — always visible, `[disabled]="isLastDirectorUser(u)"` with same tooltip
     - MatMenu divider
     - "Send Reminder" — always visible
   - **Deleted/Suspended users** (`getUserStatus(user) === 'deleted' || getUserStatus(user) === 'suspended'`): 2FA icon is dash `—` with no MatMenu interactivity
   - MatMenu items use Material icons: `security` for Reset 2FA, `vpn_key` for Generate Bypass Code, `notifications` for Send Reminder
   - "Reset 2FA" styled as `.danger` class (red text, red icon color `#C62828`)
   - MatMenu trigger: `[matMenuTriggerFor]` on the icon container div, `(click)="$event.stopPropagation()"` to prevent row click from opening the detail sheet simultaneously

3. **Reset 2FA** action:
   - Opens a `MatDialog` confirmation dialog — create an **inline dialog** in `team-roster.component.ts` (do NOT reuse the existing `ConfirmDialogComponent` from `features/admin/components/confirm-dialog/` because it uses `color="primary"` but UX-DR7 requires a **red** "Reset 2FA" button with `color="warn"`)
   - Dialog title: "Reset Two-Factor Authentication"
   - Body text: "This will clear **[User Full Name]**'s two-factor authentication enrollment. They will need to re-enroll on their next login."
   - Cancel button (default focus, `mat-dialog-close`) + Red "Reset 2FA" button (`[mat-dialog-close]="true"`, styled with `color="warn"`)
   - On confirm: calls `AdminUserService.resetTwoFactor(user.id)`
   - On success: `MatSnackBar` "Two-factor authentication has been reset for [name].", reload table
   - On error (422 "last active Director"): `MatSnackBar` "Cannot reset 2FA for the last active Director."
   - On other error: `MatSnackBar` "Failed to reset 2FA. Please try again."

4. **Generate Bypass Code** action:
   - Calls `AdminUserService.generateBypassCode(userId)` (NEW method — see AC 6)
   - Opens a display dialog (inline or separate `BypassCodeDialogComponent`) showing:
     - Title: "Temporary Bypass Code"
     - Large monospace code: 32px, `'SF Mono'/'Fira Code'/monospace`, letter-spacing 0.15em
     - "Copy Code" button: copies code to clipboard via `navigator.clipboard.writeText()`, changes label to "Copied!" momentarily (2s reset)
     - Warning banner: orange background `#FFF3E0` with icon: "This code expires in **30 minutes** and can only be used once. Share it securely with the user."
     - Close button (primary color)
   - On rate limit (429): `MatSnackBar` "Bypass code limit reached. You can generate 2 codes per hour. Try again later."
   - On error: `MatSnackBar` "Failed to generate bypass code. Please try again."

5. **Send Reminder** action:
   - No confirmation dialog — calls `AdminUserService.sendReminder(userId)` directly (NEW method)
   - On success: `MatSnackBar` "Reminder sent to [email]."
   - On error (404): `MatSnackBar` "User not found."
   - On error: `MatSnackBar` "Failed to send reminder. Please try again."

6. **New `AdminUserService` methods** added to `features/admin/services/admin-user.service.ts`:
   - `generateBypassCode(userId: string): Promise<{ bypassCode: string; expiresInSeconds: number }>` — POST `/api/v1/admin/users/${userId}/generate-bypass-code`
   - `sendReminder(userId: string): Promise<{ message: string }>` — POST `/api/v1/admin/users/${userId}/send-2fa-reminder`
   - Both follow the existing `ApiEnvelope<T>` pattern used by all other methods in this service
   - Error handling: non-2xx responses throw `HttpErrorResponse` (standard Angular HttpClient behavior) — no custom error class needed

## Tasks / Subtasks

- [x] Add `generateBypassCode()` and `sendReminder()` methods to `AdminUserService` (AC: 6)
- [x] Add `"2fa"` column to `team-roster.component.ts`:
  - [x] Insert `'2fa'` between `'role'` and `'status'` in `displayedColumns` (AC: 1)
  - [x] Add `<ng-container matColumnDef="2fa">` with icon + tooltip + MatMenu template
  - [x] Styles: icon size 20px, centered cell, hover background, green/red colors from DESIGN.md
  - [x] Import `MatMenuModule`, `MatTooltipModule`, and `MatDialogModule` (verify MatMenuModule and MatTooltipModule are already imported; `MatDialogModule` is NOT yet imported and must be added to the imports array)
- [x] Implement MatMenu with contextual items per enrollment state (AC: 2)
  - [x] "Reset 2FA" with `security` icon, `.danger` styling, disabled state for last-Director
  - [x] "Generate Bypass Code" with `vpn_key` icon (enrolled only)
  - [x] "Send Reminder" with `notifications` icon (unenrolled only)
  - [x] Divider between first item and remaining items
  - [x] `$event.stopPropagation()` on menu trigger click
- [x] Create "Reset 2FA" confirmation dialog flow (AC: 3)
  - [x] Create inline dialog in `team-roster.component.ts` with `color="warn"` on confirm button (the existing `ConfirmDialogComponent` uses `color="primary"` — do NOT reuse it)
  - [x] Wire confirm to `AdminUserService.resetTwoFactor()`
  - [x] Handle 422 (last Director) and generic errors with `MatSnackBar`
- [x] Create "Bypass Code" display dialog component (AC: 4)
  - [x] Inline dialog in `team-roster.component.ts` matching `user-detail-sheet` inline dialog pattern
  - [x] Monospace code display, Copy button, orange warning banner
  - [x] Handle clipboard API, 429 rate limit, generic errors
- [x] Implement "Send Reminder" direct action (AC: 5)
  - [x] No dialog, direct service call
  - [x] Success/error snackbar messages
- [x] Run `ng build` to verify compilation

## Dev Notes

### Prerequisites (Stories 25-1 through 25-5 — assumed complete)

- `AdminUserSummary.totpEnrolledAt` field available from `GET /api/v1/admin/users` — already exists in the admin.models.ts interface
- `AdminUserService.resetTwoFactor(userId)` — already exists
- `POST /admin/users/{id}/send-2fa-reminder` — defined in Story 25-2 (Admin API)
- `POST /admin/users/{id}/generate-bypass-code` — defined in Story 25-2 (Admin API)
- `TwoFactorSetupGuard` — defined in Story 25-4, applied to vendor routes but NOT needed on the team-roster page (Directors must already have 2FA enrolled per `[Require2FA]` on admin controllers)

### Architecture Compliance

**This story implements:**
- FR-2.1 (Staff Management Table 2FA Column) — AC 1, 2
- FR-2.2 (Reset 2FA) — AC 3
- FR-2.3 (Send Reminder) — AC 5
- FR-2.5 (Bypass Codes) — AC 4
- NFR-4.5 (MatMenu consistent with Material patterns) — AC 2
- UX-DR5 (✓/✗ indicators — color AND icon, never color alone) — AC 1
- UX-DR6 (MatMenu by state) — AC 2
- UX-DR7 (Confirmation dialog with Cancel + Red Reset) — AC 3
- UX-DR8 (Bypass code dialog: monospace, Copy, 30-min warning) — AC 4

**Does NOT implement (deferred):**
- FR-2.4 (Require 2FA Toggle) — Story 25-7
- FR-2.6 (2FA Audit Log) — Story 25-7
- FR-2.7 (Delegate to Coordinators) — Story 25-7
- FR-2.8 (Enrollment notifications) — deferred

### Source Tree Components to Touch

**Modified files:**
```
apps/web/src/app/features/admin/services/admin-user.service.ts    # MODIFY: +generateBypassCode, +sendReminder
apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts  # MODIFY: +2FA column, MatMenu, dialogs
```

**No new files needed** — all dialogs can be implemented inline within `team-roster.component.ts`, matching the existing inline dialog pattern used by `user-detail-sheet.component.ts`.

### Existing Patterns & Conventions (MUST FOLLOW)

**Inline dialog pattern** (from `user-detail-sheet.component.ts`):
- Define dialog component classes as standalone `@Component` classes at the bottom of the same `.ts` file, or import them at the top
- Example pattern:
  ```typescript
  @Component({
    selector: 'app-confirm-reset-2fa-dialog',
    template: `...`,
    standalone: true,
    imports: [MatDialogModule, MatButtonModule],
  })
  export class ConfirmReset2faDialogComponent {
    readonly data = inject(MAT_DIALOG_DATA) as { userName: string; userId: string };
  }
  ```
- Register dialog components in `TeamRosterComponent.imports` and pass them to `MatDialog.open()`

**MatMenu pattern** (from `team-roster.component.ts` existing actions column):
- `<button mat-icon-button [matMenuTriggerFor]="menu">` with `(click)="$event.stopPropagation()"` added
- `<mat-menu #menu="matMenu">` with `<button mat-menu-item>` children
- Use Material icons: `<mat-icon>icon_name</mat-icon>` before `<span>` label
- Menus already used in the actions column — follow exact same template structure

**MatTooltip pattern:**
- Tooltip on the outer container div: `[matTooltip]="getEnrollmentTooltip(u)"`
- Add getter method to the component:
  ```typescript
  getEnrollmentTooltip(user: AdminUserSummary): string {
    if (getUserStatus(user) === 'deleted' || getUserStatus(user) === 'suspended') return '';
    return user.totpEnrolledAt
      ? `Enrolled on ${new Date(user.totpEnrolledAt).toLocaleDateString()}`
      : 'Not enrolled';
  }
  ```
- This avoids Angular `date` pipe issues inside string interpolation and handles deleted/suspended edge case

**AdminUserService pattern** (from existing methods):
- All methods return `Promise<T>`, use `firstValueFrom`, wrap in `ApiEnvelope<T>`, extract `.data`
- Endpoint prefix: `${environment.apiBaseUrl}/api/v1/admin/users/`
- No custom error classes — standard `HttpErrorResponse` handling
- POST with empty body `{}` for trigger-style endpoints

**Dialog opening pattern:**
```typescript
const dialogRef = this.dialog.open(ConfirmReset2faDialogComponent, {
  data: { userName: `${u.firstName} ${u.lastName}`, userId: u.id },
  width: '440px',
});
dialogRef.afterClosed().subscribe(result => {
  if (result) {
    // proceed with action
  }
});
```

**SnackBar pattern** (consistent with existing components):
```typescript
this.snackBar.open(message, 'Close', { duration: 4000 });
```

**Error handling pattern** (consistent with existing components — see `staff-page.component.ts`):
```typescript
try {
  await this.adminUserService.resetTwoFactor(userId);
  this.snackBar.open('Two-factor authentication has been reset.', 'Close', { duration: 4000 });
  await this.loadUsers();
} catch (err) {
  const msg = this.adminUserService.extractErrorMessage(err) || 'Failed to reset 2FA. Please try again.';
  this.snackBar.open(msg, 'Close', { duration: 4000 });
}
```
NOTE: `AdminUserService` does NOT currently have `extractErrorMessage`. Use `HttpErrorResponse` status-based handling:
```typescript
if (err instanceof HttpErrorResponse && err.status === 422) {
  // handle known semantic error
}
```

### 2FA Column Template Structure

Add this after the `role` column container:

```html
<ng-container matColumnDef="2fa">
  <th mat-header-cell *matHeaderCellDef style="text-align:center;width:60px">2FA</th>
  <td mat-cell *matCellDef="let u" style="text-align:center;width:60px">
    @if (getUserStatus(u) === 'deleted') {
      <span style="color:#ccc">—</span>
    } @else {
      <div
        class="fa-cell"
        [matTooltip]="getEnrollmentTooltip(u)"
        [matMenuTriggerFor]="faMenu"
        (click)="$event.stopPropagation()"
      >
        @if (u.totpEnrolledAt) {
          <mat-icon class="fa-icon fa-enrolled">check_circle</mat-icon>
        } @else {
          <mat-icon class="fa-icon fa-not-enrolled">cancel</mat-icon>
        }
      </div>
      <mat-menu #faMenu="matMenu">
        @if (u.totpEnrolledAt) {
          <!-- Enrolled state actions -->
          <button mat-menu-item class="danger-item" (click)="confirmReset2fa(u)">
            <mat-icon class="danger-icon">security</mat-icon>
            <span>Reset 2FA</span>
          </button>
          <mat-divider />
          <button mat-menu-item (click)="generateBypassCode(u)">
            <mat-icon>vpn_key</mat-icon>
            <span>Generate Bypass Code</span>
          </button>
        } @else {
          <!-- Unenrolled state actions -->
          <button mat-menu-item class="danger-item" (click)="confirmReset2fa(u)">
            <mat-icon class="danger-icon">security</mat-icon>
            <span>Reset 2FA</span>
          </button>
          <mat-divider />
          <button mat-menu-item (click)="sendReminder(u)">
            <mat-icon>notifications</mat-icon>
            <span>Send Reminder</span>
          </button>
        }
      </mat-menu>
    }
  </td>
</ng-container>
```

### Icon Styles (add to component `styles`)

```css
.fa-cell {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 8px;
  cursor: pointer;
}
.fa-cell:hover {
  background: #f0f0f0;
}
.fa-icon {
  font-size: 20px;
  width: 20px;
  height: 20px;
}
.fa-enrolled {
  color: #2E7D32;
}
.fa-not-enrolled {
  color: #C62828;
}
.danger-item {
  color: #C62828;
}
.danger-icon {
  color: #C62828;
}
```

### Testing Standards

- Unit tests for new `AdminUserService` methods: verify correct HTTP method, URL, and body
- Component test for `TeamRosterComponent`: verify 2FA column renders correct icon per enrollment state
- Component test: verify MatMenu opens with correct items per state
- Component test: verify Reset 2FA confirmation dialog opens and calls service on confirm
- Component test: verify bypass code dialog shows code and Copy button works
- Component test: verify "Send Reminder" calls service directly
- Component test: verify deleted users show dash `—`
- All existing tests must pass

### Project Structure Notes

- No new files needed — inline dialogs match existing patterns in the codebase
- All changes scoped to `features/admin/pages/team-roster/` and `features/admin/services/`
- Follows the existing pattern of adding columns to the team roster table (the table already has 6 columns with MatMenu in the actions column)

### References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Story 6: "Director Staff Management"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "FR-2 Director 2FA Management", "UX Design", "Implementation Sequence (Step 6)"
- **UX Design:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/.working/key-staff-2fa.html` — Full mockup of 2FA column, icon states, MatMenu items
- **UX Dialog Design:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/.working/key-dialogs.html` — Reset confirmation dialog, bypass code display dialog
- **UX Design Tokens:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/DESIGN.md` — Colors: `enrolled: '#2E7D32'`, `not-enrolled: '#C62828'`
- **Existing component:** `apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts` — Team roster table with filters, pagination, MatMenu, inline dialogs
- **Existing service:** `apps/web/src/app/features/admin/services/admin-user.service.ts` — `resetTwoFactor` already exists
- **Existing models:** `apps/web/src/app/features/admin/models/admin.models.ts` — `AdminUserSummary` with `totpEnrolledAt`
- **Existing inline dialogs:** `apps/web/src/app/features/admin/components/user-detail-sheet/user-detail-sheet.component.ts` — ConfirmDialogComponent and ConfirmDeleteDialogComponent inline pattern
- **Story 25-2 (Admin API):** `_bmad-output/implementation-artifacts/25-2-admin-api.md` — Defines `POST /admin/users/{id}/send-2fa-reminder` and `POST /admin/users/{id}/generate-bypass-code`

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Completion Notes

- Added `generateBypassCode()` and `sendReminder()` methods to `AdminUserService` following existing `ApiEnvelope<T>` pattern
- Added `'2fa'` column to `displayedColumns` between `'role'` and `'status'`
- Implemented 2FA column template with green/red icons, tooltip, and MatMenu per enrollment state
- Created inline `ConfirmReset2faDialogComponent` with `color="warn"` confirm button (not reusing existing `ConfirmDialogComponent`)
- Created inline `BypassCodeDialogComponent` with monospace code display, Copy button with clipboard API, and orange warning banner
- Implemented "Send Reminder" as direct service call without confirmation dialog
- All error handling follows existing patterns (422 for last Director, 429 for rate limit, 404 for user not found)
- Used `MatDividerModule` for `<mat-divider>` in MatMenu
- Used `MatSnackBarModule` for success/error messages
- Build: 0 errors (pre-existing warnings only)

### File List

- `apps/web/src/app/features/admin/services/admin-user.service.ts` — MODIFIED: +generateBypassCode(), +sendReminder()
- `apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts` — MODIFIED: +2FA column, MatMenu, inline dialogs, snackbar

### Change Log

- Implemented Story 25-6: Director Staff Management — 2FA Column, MatMenu Actions, Dialogs (2026-07-02)

### Review Findings

#### decision-needed
- [x] [Review][Decision] ChangePasswordAsync invalidates own session after password change — Decision: keep current logout behavior (option 3). Deferred.

#### patch
- [x] [Review][Patch] Clipboard errors silently swallowed — `BypassCodeDialogComponent.copyCode()` has an empty `catch {}` that discards all clipboard write failures (HTTPS required, restricted contexts). At minimum add a `console.warn` for debugging. [team-roster.component.ts:686-688]
- [x] [Review][Patch] `setTimeout` on signal with no cleanup — `copyCode()` calls `setTimeout(() => this.copied.set(false), 2000)` with no cancellation. If the dialog is closed before 2s, `this.copied.set()` fires on a potentially destroyed component's signal. [team-roster.component.ts:687]
- [x] [Review][Patch] `getEnrollmentTooltip` called with null/undefined user — `getUserStatus(null)` returns `'active'`, then `null.totpEnrolledAt` throws TypeError. Add `if (!user) return '';` guard at top. [team-roster.component.ts:594-597]
- [x] [Review][Patch] Invalid date string in tooltip — `new Date(invalidDate).toLocaleDateString()` produces "Invalid Date". Guard with `isNaN(d.getTime())` check. [team-roster.component.ts:596]
- [x] [Review][Patch] API returns null `bypassCode` — if `result.bypassCode` is null/undefined, the dialog displays "null" as the code. Guard with `if (!result?.bypassCode) { snackBar; return; }`. [team-roster.component.ts:625-628]
- [x] [Review][Patch] Null `user.email` in snackbar — `sendReminder` success message uses `${user.email}` which renders as "Reminder sent to null." if email is null. Use `user.email ?? 'the user'` fallback. [team-roster.component.ts:677]
- [x] [Review][Patch] Close button missing `color="primary"` — AC 4 specifies "Close button (primary color)" but the implementation assigns `color="primary"` to "Copy Code" instead. Move `color="primary"` to the Close button. [team-roster.component.ts BypassCodeDialogComponent]
- [x] [Review][Patch] `isLastDirectorUser` defensive comment removed — The original comment explaining why the method returns `false` on partial data was deleted. Restore the comment to preserve design intent for future maintainers. [team-roster.component.ts:579-582]

#### defer
- [x] [Review][Defer] Inline dialogs not reusable — `ConfirmReset2faDialogComponent` and `BypassCodeDialogComponent` are defined inline; other components needing similar dialogs would duplicate them. Intentional per AC requirement for `color="warn"` on confirm.
- [x] [Review][Defer] ConfirmNewPassword lacks `[Compare]` attribute — server-side validation already checks the match in the service layer, but model-level validation would catch mismatches earlier. Pre-existing pattern.
- [x] [Review][Defer] RegenerateBackupCodes no password/step-up re-verification — any authenticated session can regenerate backup codes. Already deferred in story 25-5 review; architectural decision beyond story scope.
- [x] [Review][Defer] Missing `ProducesResponseType` for 429/422 on new endpoints — pre-existing pattern across the codebase, not introduced by this story.
- [x] [Review][Defer] Same password not rejected in ChangePasswordAsync — user can "change" to the same value (hash is recomputed with new salt). Pre-existing, not scoped to this story.
