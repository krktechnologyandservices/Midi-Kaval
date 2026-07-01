# Story 25.7: Organisation Settings + Audit Log — Require 2FA Toggle, Delegate Toggle, 2FA Audit View

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: ready-for-dev

## Story

As a **Director**,
I want **an Organisation Settings page with toggles to mandate 2FA org-wide and delegate 2FA reset authority to Coordinators, plus a filtered 2FA audit view**,
so that **I can enforce security policies across my organisation and monitor 2FA-related activity.**

## Acceptance Criteria

1. **Route `/admin/settings`** added as a child of the `/admin` parent route in `app.routes.ts`:
   - Inherits `authGuard` and `directorGuard` from the parent `/admin` route (existing)
   - Loads `OrganisationSettingsComponent`
   - Navigation link "Settings" added to the `AdminShellComponent` side nav between "Invitations" and "Audit Log" with `mat-icon: settings`

2. **`OrganisationSettingsComponent`** created at `features/admin/pages/settings/organisation-settings.component.ts` as a standalone Angular Material component:
   - Page title: "Organisation Settings"
   - Subtitle: "Manage two-factor authentication policies for your organisation."
   - Two `MatSlideToggle` controls stacked vertically in bordered card sections

3. **Toggle 1 — "Require Two-Factor Authentication"** (FR-2.4):
   - Label: "Require two-factor authentication for all staff"
   - Description: "When enabled, users without two-factor authentication will be prompted to set it up on their next login. They won't be able to access protected features until enrollment is complete."
   - Info tooltip (question-mark icon `?` in circle): "When enabled, users without two-factor authentication will be prompted to set it up on their next login."
   - Fetches current state on init from `AdminUserService.getRequire2faStatus()` (NEW method — `GET /admin/settings/require-2fa`)
   - `MatSlideToggle` writes immediately on change (UX-DR9): calls `PUT /admin/settings/require-2fa` with `{ require2fa: bool }`
   - On success: toggle stays in new position, `MatSnackBar` "Two-factor authentication requirement updated."
   - On failure: toggle reverts to previous state, `MatSnackBar` "Failed to update setting. Please try again."
   - Disabled (loading) state: toggle uses `[disabled]="loadingRequire2fa"` — each toggle has its own loading signal so they operate independently
   - Default (initial load): toggle is disabled until current state is fetched

4. **Toggle 2 — "Delegate 2FA Reset to Coordinators"** (FR-2.7):
   - Label: "Allow Coordinators to reset 2FA for field workers"
   - Description: "Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."
   - Info tooltip: "Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."
   - Fetches current state on init from `AdminUserService.getDelegationStatus()` (NEW method — reads Redis-based delegation flag)
   - `MatSlideToggle` writes immediately on change: calls `PUT /admin/settings/delegate-2fa-reset` with `{ enabled: bool }`
   - On success: `MatSnackBar` "Delegation setting updated."
   - On failure: toggle reverts, `MatSnackBar` "Failed to update delegation setting. Please try again."
   - Each toggle has its own loading signal (`loadingRequire2fa`, `loadingDelegation`) so they can be operated independently without blocking each other

5. **2FA Audit tab added to the existing Audit Log page** (`AuditLogComponent` at `features/admin/pages/audit-log/`):
   - Add a tab bar at the top of the page: "All Events" | "2FA Events"
   - Default tab: "All Events" — existing behavior unchanged (calls `AuditApiService.list()` → `GET /api/v1/admin/audit`)
   - "2FA Events" tab: calls `AdminUserService.get2faAuditLog()` (NEW method — `GET /admin/audit/2fa`) with the same filter/pagination parameters
   - The 2FA audit endpoint already auto-filters to events where `eventType.startsWith("2fa_")` per Story 25-2 endpoint definition
   - Tab switches reset pagination to page 1
   - Event type dropdown in "2FA Events" tab is pre-filtered to show only `2fa_` event types (optional enhancement — can show all types since the server auto-filters)
   - Tab bar uses `MatButtonToggleGroup` or simple button group styled as tabs

6. **New `AdminUserService` methods** added to `features/admin/services/admin-user.service.ts`:
   - `getRequire2faStatus(): Promise<{ require2fa: boolean }>` — `GET /admin/settings/require-2fa`
   - `setRequire2fa(require2fa: boolean): Promise<{ require2fa: boolean }>` — `PUT /admin/settings/require-2fa` with body `{ require2fa }`
   - `getDelegationStatus(): Promise<{ enabled: boolean }>` — reads the Redis-backed delegation flag
   - `setDelegation(enabled: boolean): Promise<{ enabled: boolean }>` — `PUT /admin/settings/delegate-2fa-reset` with body `{ enabled }`
   - `get2faAuditLog(filter: AuditLogFilter): Promise<{ items: AuditEventDto[]; meta: AuditMeta }>` — `GET /admin/audit/2fa` with query params matching `AuditLogFilter` (reuse existing `AuditLogFilter` and `AuditEventDto` types from `audit.models.ts`)
   - All follow the existing `ApiEnvelope<T>` pattern

## Tasks / Subtasks

- [ ] Add route `/admin/settings` in `app.routes.ts` (AC: 1)
  - [ ] Add child route under `/admin` parent: `{ path: 'settings', loadComponent: ... }`
  - [ ] Add "Settings" nav link to `AdminShellComponent` side nav
- [ ] Create `OrganisationSettingsComponent` (AC: 2, 3, 4)
  - [ ] Create `features/admin/pages/settings/organisation-settings.component.ts`
  - [ ] Title and subtitle section
  - [ ] "Require 2FA" `MatSlideToggle` with label, description, info tooltip (AC: 3)
  - [ ] "Delegate 2FA Reset" `MatSlideToggle` with label, description, info tooltip (AC: 4)
  - [ ] Load current states on init from `AdminUserService`
  - [ ] Toggle change handler: write immediately with revert-on-failure
  - [ ] Loading/disabled states during API calls (separate `loadingRequire2fa` / `loadingDelegation` signals so toggles don't block each other)
  - [ ] Success/failure `MatSnackBar` messages
- [ ] Add new methods to `AdminUserService` (AC: 6)
  - [ ] `getRequire2faStatus()` / `setRequire2fa()`
  - [ ] `getDelegationStatus()` / `setDelegation()`
  - [ ] `get2faAuditLog(filter)` — requires importing `AuditLogFilter`, `AuditEventDto` from `audit.models.ts` and `AuditMeta` from `audit.service.ts`
- [ ] Add 2FA audit tab to `AuditLogComponent` (AC: 5)
  - [ ] Add tab/button group at top: "All Events" | "2FA Events"
  - [ ] Wire 2FA tab to call `AdminUserService.get2faAuditLog()` instead of `AuditApiService.list()`
  - [ ] Import and inject `AdminUserService` alongside existing `AuditApiService` (add `import { AdminUserService } from '../../services/admin-user.service';`)
  - [ ] Add signal: `readonly activeAuditTab = signal<'all' | '2fa'>('all');`
  - [ ] Reset pagination on tab switch
  - [ ] Preserve all existing filter behavior for "All Events" tab
- [ ] Run `ng build` to verify compilation

## Dev Notes

### Prerequisites (Stories 25-1 through 25-6 — assumed complete)

- `PUT /admin/settings/require-2fa` — defined in Story 25-2 (Admin API), updates `Organisation.Require2fa`
- `PUT /admin/settings/delegate-2fa-reset` — defined in Story 25-2 (Admin API), stores Redis flag at `delegate_2fa_reset:{organisationId}`
- `GET /admin/audit/2fa` — defined in Story 25-2 (Admin API), queries `AuditEvents` with `EventType.StartsWith("2fa_")`
- `AdminTwoFactorController` with all 6 endpoints — defined in Story 25-2
- `Organisation.Require2fa` property on existing entity — defined in Story 25-1
- `AuditLogComponent` at `features/admin/pages/audit-log/` — exists with filter/pagination/expand infrastructure
- `AuditApiService` at `features/admin/services/audit.service.ts` — exists with `list()` method
- `AdminUserService` at `features/admin/services/admin-user.service.ts` — exists, will be extended

### Architecture Compliance

**This story implements:**
- FR-2.4 (Require 2FA Toggle) — AC 3
- FR-2.6 (2FA Audit Log) — AC 5
- FR-2.7 (Delegate to Coordinators) — AC 4
- UX-DR9 (Toggle immediate save with revert on failure) — AC 3, 4

**Does NOT implement (deferred):**
- FR-2.8 (Enrollment notifications) — deferred
- FR-3 (Proactive Onboarding Emails) — Story 25-8

### Source Tree Components to Touch

**New files:**
```
apps/web/src/app/features/admin/pages/settings/organisation-settings.component.ts
```

**Modified files:**
```
apps/web/src/app/app.routes.ts                                          # MODIFY: + /admin/settings route
apps/web/src/app/features/admin/admin.component.ts                      # MODIFY: + Settings nav link
apps/web/src/app/features/admin/services/admin-user.service.ts           # MODIFY: + 5 new methods
apps/web/src/app/features/admin/pages/audit-log/audit-log.component.ts  # MODIFY: + 2FA tab
```

### Existing Patterns & Conventions (MUST FOLLOW)

**Admin shell nav pattern** (from `admin.component.ts`):
```html
<a mat-list-item routerLink="/admin/settings" routerLinkActive="active-link">
  <mat-icon matListItemIcon>settings</mat-icon>
  <span matListItemTitle>Settings</span>
</a>
```
Add between the Invitations and Audit Log links. Already imports `MatIconModule`, `MatListModule`, `RouterLink`, `RouterLinkActive`.

**Route pattern** (from `app.routes.ts` admin children):
```typescript
{
  path: 'settings',
  loadComponent: () =>
    import('./features/admin/pages/settings/organisation-settings.component').then(
      (m) => m.OrganisationSettingsComponent,
    ),
},
```
No extra guards needed — the parent `/admin` route already has `canActivate: [directorGuard]` and `authGuard` inheritance.

**AdminUserService method pattern** (from existing methods):
```typescript
getRequire2faStatus(): Promise<{ require2fa: boolean }> {
  return firstValueFrom(
    this.http.get<ApiEnvelope<{ require2fa: boolean }>>(
      `${environment.apiBaseUrl}/api/v1/admin/settings/require-2fa`,
    ),
  ).then(e => e.data);
}

setRequire2fa(require2fa: boolean): Promise<{ require2fa: boolean }> {
  return firstValueFrom(
    this.http.put<ApiEnvelope<{ require2fa: boolean }>>(
      `${environment.apiBaseUrl}/api/v1/admin/settings/require-2fa`,
      { require2fa },
    ),
  ).then(e => e.data);
}
```
Note the endpoint base path: `/api/v1/admin/settings/` (per `AdminTwoFactorController` route at `api/v1/admin`).
Note: `GET` endpoints for the settings may or may not exist yet. The Story 25-2 defines `PUT` endpoints for settings but does NOT define `GET` endpoints to read current state. Two approaches:
1. Add `GET` endpoints to the backend as part of this story (preferred — keeps data consistent)
2. Derive initial state from the login response `orgRequires2fa` field for the mandate toggle, and assume delegation is off by default

**Recommended approach: Add read-back endpoints.** The story should include subtasks for:
- `GET /admin/settings/require-2fa` — returns `{ require2fa: bool }` from `db.Organisations.Where(o => o.Id == orgId).Select(o => o.Require2fa).FirstAsync()`
- `GET /admin/settings/delegate-2fa-reset` — returns `{ enabled: bool }` from Redis key `delegate_2fa_reset:{organisationId}`, defaulting to `false`

**MatSlideToggle pattern:**
```html
<mat-slide-toggle
  [checked]="require2fa()"
  [disabled]="loading()"
  (change)="onRequire2faChange($event.checked)"
>
  Require two-factor authentication for all staff
</mat-slide-toggle>
```
Import `MatSlideToggleModule`. Add description text below the toggle in a separate `<p>` element with `class="toggle-desc"`.

**Info tooltip pattern:**
```html
<mat-icon
  class="info-icon"
  matTooltip="When enabled, users without two-factor authentication will be prompted to set it up on their next login."
  aria-label="More information"
>info_outline</mat-icon>
```

**Toggle with revert-on-failure pattern (using independent loading signals):**
```typescript
readonly require2fa = signal(false);
readonly loadingRequire2fa = signal(false);
readonly delegation = signal(false);
readonly loadingDelegation = signal(false);

async onRequire2faChange(newValue: boolean): Promise<void> {
  const previousValue = this.require2fa();
  this.require2fa.set(newValue); // optimistic update
  this.loadingRequire2fa.set(true);
  try {
    await this.adminUserService.setRequire2fa(newValue);
    this.snackBar.open('Two-factor authentication requirement updated.', 'Close', { duration: 4000 });
  } catch {
    this.require2fa.set(previousValue); // revert
    this.snackBar.open('Failed to update setting. Please try again.', 'Close', { duration: 4000 });
  } finally {
    this.loadingRequire2fa.set(false);
  }
}
```

**Tab bar pattern** (for AuditLogComponent 2FA filter):
```html
<mat-button-toggle-group
  [value]="activeAuditTab()"
  (change)="onAuditTabChange($event.value)"
  class="audit-tab-bar"
>
  <mat-button-toggle value="all">All Events</mat-button-toggle>
  <mat-button-toggle value="2fa">2FA Events</mat-button-toggle>
</mat-button-toggle-group>
```
Import `MatButtonToggleModule`. When tab switches from `"all"` to `"2fa"`, call `AdminUserService.get2faAuditLog()` instead of `AuditApiService.list()`. Reset page to 1 on tab switch.

### Setting Up Read-Back Endpoints

Since Story 25-2 only defines `PUT` endpoints for settings (not `GET`), this story needs to add read-back endpoints to the `AdminTwoFactorController` or `AdminTwoFactorService`:

**`GET /admin/settings/require-2fa`** — returns `{ require2fa: bool }`:
- In `AdminTwoFactorService`, add `GetRequire2faStatusAsync(orgId, ct)`:
  ```csharp
  public async Task<Require2faResponse> GetRequire2faStatusAsync(Guid organisationId, CancellationToken ct)
  {
      var require2fa = await db.Organisations
          .Where(o => o.Id == organisationId)
          .Select(o => o.Require2fa)
          .FirstOrDefaultAsync(ct);
      return new Require2faResponse(require2fa);
  }
  ```
- In `AdminTwoFactorController`, add:
  ```csharp
  [HttpGet("settings/require-2fa")]
  [EnableRateLimiting("data-read")]
  public async Task<IActionResult> GetRequire2fa(CancellationToken ct)
  {
      if (!TryResolveOrganisationId(out var orgId, out var actorError))
          return actorError!;
      var result = await adminTwoFactorService.GetRequire2faStatusAsync(orgId!.Value, ct);
      return Ok(new ApiResponse<Require2faResponse>(result, new ApiMeta { RequestId = ResolveRequestId() }));
  }
  ```
- Requires a `Require2faResponse` DTO: `public record Require2faResponse(bool Require2fa);`

**`GET /admin/settings/delegate-2fa-reset`** — returns `{ enabled: bool }`:
- In `AdminTwoFactorService`, add `GetDelegationStatusAsync(orgId, ct)`:
  ```csharp
  public async Task<DelegationResponse> GetDelegationStatusAsync(Guid organisationId, CancellationToken ct)
  {
      var db = redis.GetDatabase();
      var exists = await db.KeyExistsAsync($"delegate_2fa_reset:{organisationId}");
      return new DelegationResponse(exists);
  }
  ```
- In `AdminTwoFactorController`, add `[HttpGet("settings/delegate-2fa-reset")]` with `[EnableRateLimiting("data-read")]`
- Response: `public record DelegationResponse(bool Enabled);`

These endpoints must be added BEFORE the frontend work, since the Angular component depends on them.

### OrganisationSettingsComponent Template Structure

```html
<div class="settings-page">
  <header class="page-header">
    <h1>Organisation Settings</h1>
    <p class="subtitle">Manage two-factor authentication policies for your organisation.</p>
  </header>

  <mat-card class="settings-card">
    <mat-card-content>
      <div class="toggle-row">
        <div class="toggle-content">
          <div class="toggle-label-wrapper">
            <span class="toggle-label">Require two-factor authentication for all staff</span>
            <mat-icon
              class="info-icon"
              matTooltip="When enabled, users without two-factor authentication will be prompted to set it up on their next login."
            >info_outline</mat-icon>
          </div>
          <p class="toggle-desc">When enabled, users without two-factor authentication will be prompted to set it up on their next login. They won't be able to access protected features until enrollment is complete.</p>
        </div>
        <mat-slide-toggle
          [checked]="require2fa()"
          [disabled]="loadingRequire2fa()"
          (change)="onRequire2faChange($event.checked)"
        />
      </div>

      <mat-divider class="toggle-divider" />

      <div class="toggle-row">
        <div class="toggle-content">
          <div class="toggle-label-wrapper">
            <span class="toggle-label">Allow Coordinators to reset 2FA for field workers</span>
            <mat-icon
              class="info-icon"
              matTooltip="Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes."
            >info_outline</mat-icon>
          </div>
          <p class="toggle-desc">Coordinators can reset two-factor authentication for SocialWorkers and CaseWorkers. They will not have access to generate bypass codes.</p>
        </div>
        <mat-slide-toggle
          [checked]="delegation()"
          [disabled]="loadingDelegation()"
          (change)="onDelegationChange($event.checked)"
        />
      </div>
    </mat-card-content>
  </mat-card>
</div>
```

### Styles (add to component `styles`)

```css
.settings-page { max-width: 720px; }
.page-header { margin-bottom: 24px; }
.page-header h1 { margin: 0; font-size: 24px; font-weight: 500; }
.subtitle { margin: 4px 0 0; font-size: 14px; color: #6B7280; }
.settings-card { border-radius: 8px; }
.toggle-row { display: flex; align-items: flex-start; gap: 16px; padding: 8px 0; }
.toggle-content { flex: 1; }
.toggle-label-wrapper { display: flex; align-items: center; gap: 6px; margin-bottom: 4px; }
.toggle-label { font-size: 14px; font-weight: 500; color: #212121; }
.toggle-desc { margin: 0; font-size: 13px; color: #6B7280; line-height: 1.4; }
.info-icon { font-size: 18px; width: 18px; height: 18px; color: #9CA3AF; cursor: help; }
.toggle-divider { margin: 12px 0; }
```

### Audit Log Tab Implementation Details

Add to `AuditLogComponent`:

1. **New signal**: `activeAuditTab = signal<'all' | '2fa'>('all')`
2. **New import**: `MatButtonToggleModule`
3. **Template addition**: Insert before the filter card:
   ```html
   <mat-button-toggle-group
     [value]="activeAuditTab()"
     (change)="onTabChange($event.value)"
     class="audit-tab-bar"
     aria-label="Audit log filter tabs"
   >
     <mat-button-toggle value="all">All Events</mat-button-toggle>
     <mat-button-toggle value="2fa">2FA Events</mat-button-toggle>
   </mat-button-toggle-group>
   ```
4. **Tab change handler**:
   ```typescript
   onTabChange(tab: 'all' | '2fa'): void {
     if (tab === this.activeAuditTab()) return;
     this.activeAuditTab.set(tab);
     this.currentPage.set(1);
     this.loadEvents();
   }
   ```
5. **Modified `loadEvents()`**:
   ```typescript
   private async loadEvents(): Promise<void> {
     this.loading.set(true);
     this.errorMessage.set(null);
     try {
       const filter: AuditLogFilter = { ... };
       if (this.activeAuditTab() === '2fa') {
         const result = await this.adminUserService.get2faAuditLog(filter);
         this.items.set(result.items);
         this.totalCount.set(result.meta.totalCount ?? 0);
       } else {
         const result = await this.api.list(filter);
         this.items.set(result.items);
         this.totalCount.set(result.meta.totalCount ?? 0);
       }
       this.expandedRows.clear();
     } catch (error) {
       this.errorMessage.set(this.api.extractErrorMessage(error));
       this.items.set([]);
     } finally {
       this.loading.set(false);
     }
   }
   ```
6. **Inject `AdminUserService`** alongside existing `AuditApiService`
7. **Styles for tab bar**:
   ```css
   .audit-tab-bar { margin-bottom: 16px; display: block; }
   ```

### Testing Standards

- Unit tests for new `AdminUserService` methods: verify correct HTTP method, URL, query/body params
- Component test for `OrganisationSettingsComponent`: verify toggle renders, calls API on change, reverts on failure
- Component test for `AuditLogComponent`: verify tab switches between all/2fa, calls correct service method
- Integration test: verify toggle call and read-back endpoint consistency
- All existing tests must pass

### Project Structure Notes

- New `OrganisationSettingsComponent` in `features/admin/pages/settings/` — consistent with other admin page locations (`team-roster/`, `audit-log/`, `invitations/`)
- Read-back `GET` endpoints added to existing `AdminTwoFactorController` and `AdminTwoFactorService` in the API — extending existing files from Story 25-2
- Tab integration into `AuditLogComponent` avoids creating a duplicate audit page — follows DRY principle
- `AdminUserService` already has the `resetTwoFactor` method from prior stories; adding settings and 2FA audit methods keeps 2FA admin operations in one place

### References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Story 7: "Organisation Settings + Audit Log"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "FR-2 Director 2FA Management" (FR-2.4, FR-2.6, FR-2.7), "UX Design" (UX-DR9), "Implementation Sequence (Step 7)"
- **UX Design:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/.working/key-org-settings.html` — Full mockup of settings page with both toggles
- **UX Design Tokens:** `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-07-01/DESIGN.md` — Component tokens for toggle rows
- **Story 25-2 (Admin API):** `_bmad-output/implementation-artifacts/25-2-admin-api.md` — Defines `PUT /admin/settings/require-2fa`, `PUT /admin/settings/delegate-2fa-reset`, `GET /admin/audit/2fa`
- **Existing component:** `apps/web/src/app/features/admin/admin.component.ts` — Admin shell with nav links
- **Existing component:** `apps/web/src/app/features/admin/pages/audit-log/audit-log.component.ts` — Audit log with filters, pagination, expand
- **Existing service:** `apps/web/src/app/features/admin/services/audit.service.ts` — `AuditApiService` with `list()` method
- **Existing service:** `apps/web/src/app/features/admin/services/admin-user.service.ts` — Admin user service to extend
- **Existing models:** `apps/web/src/app/features/admin/models/audit.models.ts` — `AuditEventDto`, `AuditLogFilter`, `AuditListResultDto`

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash
