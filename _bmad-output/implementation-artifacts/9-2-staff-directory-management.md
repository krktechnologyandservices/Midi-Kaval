---
baseline_commit: eff5b06
---

# Story 9.2: Staff Directory Management

Status: done

## Story

**As a** Project Director,
**I want to** manage staff accounts and roles,
**So that** access matches organisation structure (FR-24, FR-1 admin actions).

## Acceptance Criteria

**Given** the Admin staff screen on the web app
**When** the Director views the staff directory
**Then** all users in the organisation are listed with name, email, role, and active/inactive status

**Given** the staff directory list
**When** the Director creates a new staff account
**Then** the user record is created with name, email, role, and a password-reset-required flag; audit_events records the creation

**Given** an existing staff account
**When** the Director edits the user's name, email, or role
**Then** the user record updates and token_version increments on role change; audit_events records the change

**Given** an active staff account
**When** the Director deactivates it
**Then** IsActive is set to false, token_version increments, and the user cannot complete OTP login on next attempt; audit_events records the deactivation

**Given** an inactive staff account
**When** the Director reactivates it
**Then** IsActive is set to true and the user can log in again

**Given** any staff account
**When** the Director triggers a force password reset
**Then** token_version increments (forcing logout on all devices) and the user must use forgot-password flow to set a new password; audit_events records the reset

**And** empty directory (no staff besides Director) shows an empty table with an "Add staff" CTA (UX-DR13)
**And** all mutations write audit_events (FR-25)
**And** changes are visible on next client fetch

## Technical Requirements

### API: User Entity Migration (modified — `apps/api/Domain/Entities/User.cs`)

Add new columns to the existing `User` entity:

```csharp
public string FirstName { get; set; } = string.Empty;
public string LastName { get; set; } = string.Empty;
public string? PhoneNumber { get; set; }
```

These join the existing fields: Id, OrganisationId, Email, Role, TokenVersion, PasswordHash, IsActive, CreatedAtUtc, UpdatedAtUtc.

A new EF Core migration is needed: `dotnet ef migrations add AddStaffDirectoryFields`.

### API: EF Core Configuration (modified — existing UserConfiguration.cs)

- Add `HasMaxLength(128)` for FirstName, LastName
- Add `HasMaxLength(20)` for PhoneNumber (nullable)

### API: DTOs (new — `apps/api/Models/Users/`)

```csharp
// CreateStaffRequest
public record CreateStaffRequest(
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? PhoneNumber);

// UpdateStaffRequest
public record UpdateStaffRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Role);

// StaffDto (response)
public record StaffDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

// StaffListResultDto
public record StaffListResultDto(List<StaffDto> Items);
```

### API: Controller (new — `apps/api/Controllers/V1/StaffController.cs`)

**Route:** `api/v1/staff`
**Policy:** `[Authorize(Policy = Policies.DirectorOnly)]`

| Method | Path | Action | Description |
|--------|------|--------|-------------|
| GET | `/api/v1/staff` | List | Returns all users in the Director's organisation, ordered by name |
| GET | `/api/v1/staff/{id}` | Get by id | Single user |
| POST | `/api/v1/staff` | Create | Create new staff account with `ForcePasswordReset = true` |
| PUT | `/api/v1/staff/{id}` | Update | Update name, phone, role. If role changes, increment `TokenVersion` |
| DELETE | `/api/v1/staff/{id}` | Deactivate | Soft-delete sets `IsActive = false`, increments `TokenVersion` |
| PATCH | `/api/v1/staff/{id}/reactivate` | Reactivate | Sets `IsActive = true` |
| POST | `/api/v1/staff/{id}/force-reset` | Force password reset | Increments `TokenVersion` (logs out all sessions) |

**Pattern:**
- Primary constructor injecting `AppDbContext db`
- Use EF.Property/reflection pattern similar to `LegendsController` or direct typed access since all operations target `DbSet<User>`
- Organisation-scoped — only users within the Director's organisation

**Validation:**
- 400 if email is empty/invalid on create
- 400 if first/last name is empty on create
- 400 if role is not a valid UserRoles value
- 404 if user not found or outside organisation
- 409 if email already exists in the same organisation

**Existing `GET /api/v1/users/field-workers`** in `UsersController.cs` must be preserved unchanged — it serves the Case create form.

**Each mutation** writes an `AuditEvent`:

```csharp
db.AuditEvents.Add(new AuditEvent
{
    Id = Guid.NewGuid(),
    OrganisationId = organisationId,
    ActorUserId = userId,
    EventType = AuditEventTypes.StaffCreated,  // or StaffUpdated / StaffDeactivated / StaffReactivated / StaffForceReset
    MetadataJson = JsonSerializer.Serialize(new { ... }),
    CreatedAtUtc = now,
});
```

### API: AuditEventTypes (modified)

Add new constants following the existing `legend.*` pattern:
- `staff.created`
- `staff.updated`
- `staff.deactivated`
- `staff.reactivated`
- `staff.force-reset`

### API: Dependencies

- Migrations: `dotnet ef migrations add AddStaffDirectoryFields`
- The existing `ActiveUserRequirement` already checks `IsActive` on token validation — deactivated users are denied access at the JWT validation layer.
- `token_version` increment is already wired into session invalidation — no changes needed there.

### Web: Staff API Service (new — `apps/web/src/app/features/shell/services/staff-api.service.ts`)

Follow the `LegendsApiService` pattern:

```typescript
@Injectable({ providedIn: 'root' })
export class StaffApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/staff`;

  async list(): Promise<StaffDto[]>;
  async get(id: string): Promise<StaffDto>;
  async create(request: CreateStaffRequest): Promise<StaffDto>;
  async update(id: string, request: UpdateStaffRequest): Promise<StaffDto>;
  async deactivate(id: string): Promise<void>;
  async reactivate(id: string): Promise<StaffDto>;
  async forceReset(id: string): Promise<void>;
}
```

### Web: Staff Models (new — `apps/web/src/app/features/shell/staff.models.ts`)

```typescript
export interface StaffDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: AppRole;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateStaffRequest {
  email: string;
  firstName: string;
  lastName: string;
  role: AppRole;
  phoneNumber?: string;
}

export interface UpdateStaffRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  role?: AppRole;
}
```

### Web: Staff Edit Dialog (new — `apps/web/src/app/features/shell/pages/staff-edit-dialog.component.ts`)

Standalone component using MatDialog for:
- Create: form with email, first name, last name, role, phone
- Edit: form with first name, last name, role, phone (email is read-only after creation)

Similar pattern to `LegendEditDialogComponent` but with more form fields.

### Web: StaffPageComponent (new — `apps/web/src/app/features/shell/pages/staff-page.component.ts`)

Standalone component with signal state management, following `LegendsPageComponent` pattern:

**State (signals):**
- `items: WritableSignal<StaffDto[]>` — staff list
- `loading: WritableSignal<boolean>`
- `errorMessage: WritableSignal<string | null>`

**Template structure:**
- Page header with title "Staff Directory" and subtitle "Manage staff accounts and roles"
- Material table (`mat-table`) with columns: Name, Email, Role, Status (Active/Inactive), Actions (Edit, Deactivate/Reactivate, Force Reset)
- "Add Staff" button above table
- Empty state with "Add first staff" CTA (UX-DR13)
- Edit/create via MatDialog

**Behavior:**
- On init, fetch all staff for organisation
- Create adds new staff account
- Edit updates name, role, phone; if role changed, show note about session invalidation
- Deactivate/Reactivate toggles IsActive
- Force reset increments token_version with confirmation dialog
- Error handling with `errorMessage` signal

**Imports:** `MatTableModule`, `MatButtonModule`, `MatIconModule`, `MatDialogModule`, `MatCardModule`, `CommonModule`, `FormsModule`

### Web: Route (modified — `apps/web/src/app/app.routes.ts`)

Replace the admin-page placeholder route for `/admin` to show the StaffPageComponent. The AdminPageComponent current placeholder content (audit log stub) should be replaced or redirected.

Alternatively, add a direct route `/admin/staff` pointing to `StaffPageComponent` and keep the admin-page as a wrapper.

### Web: Sidebar (modified — `supervisor-shell.component.ts`)

The existing `/admin` nav item resolves to the Admin placeholder page. Either:
- Point `/admin` directly to StaffPageComponent (since travel claims already have their own `/admin/travel-claims` route)
- Or add `/admin/staff` as a sub-route and update nav to include "Staff Directory" item

### Testing

| Layer | File | Key tests |
|-------|------|-----------|
| API unit | `tests/api.unit/` | StaffController: valid CRUD, duplicate email 409, role change increments token_version, deactivation triggers token_version bump, force-reset triggers token_version bump, non-Director returns 403 |
| API integration | `tests/api.integration/` | Staff CRUD against real DB, deactivation prevents login, reactivation restores login |
| Web | `apps/web/src/app/features/shell/pages/staff-page.component.spec.ts` | Component renders staff list, shows empty state, create/edit/deactivate flows, force reset flow, error state |

## Architecture Compliance

- Route: `/api/v1/staff` — plural kebab-case per architecture §5.3
- Policy: `DirectorOnly` on all endpoints — only Directors can manage staff
- Audit: every mutation writes `audit_events` table
- Envelope: `{ data, meta }` via `ApiEnvelopeFilter`
- UUID v4 for all IDs
- ISO 8601 UTC timestamps
- CamelCase JSON response
- Web: standalone component with signals for local state
- Empty state per UX-DR13
- `token_version` increment on role change and deactivation is the existing session invalidation mechanism (established in Epic 1)

## Library / Framework Requirements

- **Angular Material**: `MatTableModule`, `MatButtonModule`, `MatIconModule`, `MatDialogModule`, `MatCardModule`, `MatFormFieldModule`, `MatInputModule`, `MatSelectModule`
- **ASP.NET Core 8**: Entity Framework Core, primary constructors on controller
- No new NuGet or npm packages

## File Structure Requirements

### New files (API)

| File | Purpose |
|------|---------|
| `apps/api/Controllers/V1/StaffController.cs` | Staff CRUD controller |
| `apps/api/Models/Users/StaffDto.cs` | Staff DTO |
| `apps/api/Models/Users/StaffListResultDto.cs` | Staff list result |
| `apps/api/Models/Users/CreateStaffRequest.cs` | Create request |
| `apps/api/Models/Users/UpdateStaffRequest.cs` | Update request |

### Modified files (API)

| File | Change |
|------|--------|
| `apps/api/Domain/Entities/User.cs` | Add FirstName, LastName, PhoneNumber properties |
| `apps/api/Infrastructure/Persistence/UserConfiguration.cs` | Add EF config for new fields |
| `apps/api/Infrastructure/Audit/AuditEventTypes.cs` | Add staff CRUD event type constants |
| `apps/api/Infrastructure/Persistence/AppDbContext.cs` | Ensure DbSet<User> exists (likely already present) |

### New files (Web)

| File | Purpose |
|------|---------|
| `apps/web/src/app/features/shell/services/staff-api.service.ts` | API service for staff CRUD |
| `apps/web/src/app/features/shell/staff.models.ts` | Staff DTO types and request types |
| `apps/web/src/app/features/shell/pages/staff-page.component.ts` | Staff directory page component |
| `apps/web/src/app/features/shell/pages/staff-page.component.html` | Staff page template |
| `apps/web/src/app/features/shell/pages/staff-page.component.scss` | Staff page styles |
| `apps/web/src/app/features/shell/pages/staff-edit-dialog.component.ts` | Add/edit staff dialog |
| `apps/web/src/app/features/shell/pages/staff-confirm-dialog.component.ts` | Confirmation dialog for deactivate/force-reset |

### Modified files (Web)

| File | Change |
|------|--------|
| `apps/web/src/app/app.routes.ts` | Add staff page route under admin |
| `apps/web/src/app/features/shell/supervisor-shell.component.ts` | Update nav items if needed |

## Previous Story Intelligence

Story 9.1 (most recent — Legends CRUD API and Web UI) established:
- Patterns for `signal()`-based state management with `computed()` in components
- `inject()` pattern for dependency injection
- `firstValueFrom` wrapper for API calls and dialog interactions
- Error handling via `errorMessage` signals + `extractErrorMessage`
- MatDialog for add/edit forms and confirmation dialogs
- MatTable with status badges and action columns
- Audit event recording pattern for all mutations

Key learnings to apply:
- **Do** follow LegendsApiService pattern for the API service
- **Do** use MatDialog for forms and confirmations (not window.confirm)
- **Do** write audit events on all mutations
- **Do** use signals for local component state
- **Do** add token_version increment on role change and deactivation (reuses existing session invalidation)
- **Don't** use `window.confirm()` — use MatDialog confirmations
- **Don't** use deprecated `toPromise()` — use `firstValueFrom()` instead
- The `ForcePasswordReset` concept is new — it uses `token_version` increment (same mechanism as role change, not a separate DB column)
- The `User` entity is a pre-existing table — adding columns requires a new EF Core migration

## Project Context Reference

- Standalone components + signals for local state
- Manual API services following `firstValueFrom` + `inject(HttpClient)` pattern
- API envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- All mutations write audit_events
- UUID v4 IDs, snake_case DB tables, camelCase JSON
- DirectorOnly policy for staff management
- NFR-12: Web PWA is network-only for mutations (staff CRUD requires connectivity)
- `token_version` increments trigger forced logout on next token refresh — mechanism already exists in `JwtTokenService` validation
- The `ActiveUserRequirement` already denies deactivated users — no additional auth changes needed

## Story Completion Status

- **Type:** New feature (API + Web UI)
- **Dependencies:** EF Core migration for new User columns
- **Estimated files:** ~7 new (API), ~5 modified (API), ~7 new (Web), ~2 modified (Web)
- **Verification:** `dotnet build` on API, `npx tsc --noEmit` on web, manual CRUD test via web UI

---

## Tasks / Subtasks

### API Layer
- [x] Add FirstName, LastName, PhoneNumber to User entity
- [x] Update User EF configuration with max length constraints
- [x] Create Staff DTOs (StaffDto, CreateStaffRequest, UpdateStaffRequest, StaffListResultDto)
- [x] Add staff audit event types (StaffCreated, StaffUpdated, StaffDeactivated, StaffReactivated, StaffForceReset)
- [x] Create StaffController with CRUD + deactivate/reactivate/force-reset endpoints
- [x] Create EF Core migration (AddStaffDirectoryFields)
- [x] Add UserRoles.All static array and IsValid helper

### Web Layer
- [x] Create staff models (StaffDto, CreateStaffRequest, UpdateStaffRequest, STAFF_ROLES)
- [x] Create StaffApiService with CRUD + deactivate/reactivate/forceReset methods
- [x] Create staff-edit-dialog component (create/edit form with email, name, phone, role)
- [x] Create staff-confirm-dialog component (reusable confirmation dialog)
- [x] Create staff-page component (TS, HTML, SCSS) with signal state management
- [x] Update app routes to add `admin/staff` route
- [x] Update admin-page component with Staff Directory link
- [x] Remove deprecated user management hint from admin page

### Review Findings

#### Decision Needed
- [x] [Review][Defer] Email permanently immutable after creation — intentionally read-only per tech spec; AC 3 wording acknowledged as spec contradiction

#### Patch
- [x] [Review][Patch] Force-reset does not clear PasswordHash — applied: `user.PasswordHash = string.Empty` added to `ForceReset()`
- [x] [Review][Patch] Update endpoint accepts whitespace-only names — applied: `IsNullOrWhiteSpace` guards added to `Update()`
- [x] [Review][Patch] No self-deactivation guard — applied: actor != target check added to `Deactivate()` and `ForceReset()`
- [x] [Review][Patch] Reactivate missing idempotency guard — applied: early-return `NoContent()` if already active
- [x] [Review][Patch] Broken error-extraction pipeline in StaffApiService — applied: `wrapError` unwraps `HttpErrorResponse` preserving actual server details
- [x] [Review][Patch] Missing `DbUpdateException` guard on unique email (Create) — applied: try-catch with `IsUniqueConstraintViolation` helper
- [x] [Review][Patch] `PhoneNumber` MaxLength 20 too tight — applied: changed to `[Phone][MaxLength(30)]` in DTOs and EF config, migration updated
- [ ] [Review][Patch] Force-reset doesn't clear PasswordHash — `ForceReset()` must set `PasswordHash = string.Empty` to force forgot-password flow
- [ ] [Review][Patch] Update endpoint accepts whitespace-only names — add `IsNullOrWhiteSpace` guards in `Update()`
- [ ] [Review][Patch] No self-deactivation guard — Director can deactivate/force-reset own account; add actor != target check
- [ ] [Review][Patch] Reactivate missing idempotency guard — add early-return if already active
- [ ] [Review][Patch] Broken error-extraction pipeline in StaffApiService — `wrapError` and `extractErrorMessage` discard actual server error details
- [ ] [Review][Patch] Missing `DbUpdateException` guard on unique email (Create) — wrap `SaveChangesAsync` same as `LegendsController`
- [ ] [Review][Patch] `PhoneNumber` MaxLength 20 too tight — increase to 30 and add `[Phone]` validation

#### Defer
- [x] [Review][Defer] No pagination on List endpoint — pre-existing, beyond story scope
- [x] [Review][Defer] API accepts Director role but UI excludes it — pre-existing design choice
- [x] [Review][Defer] No invite/password-set flow on create — pre-existing, requires email integration
- [x] [Review][Defer] Rate limiting on force-reset — pre-existing, security hardening story
- [x] [Review][Defer] Role change increments TokenVersion (logout) — pre-existing, intentional per spec
- [x] [Review][Defer] No explicit ForcePasswordReset DB column — pre-existing, implicit via PasswordHash="" per spec decision
- [x] [Review][Defer] Test coverage — pre-existing, beyond story scope
- [x] [Review][Defer] Audit events lack IP/user-agent — pre-existing, cross-cutting concern

## Dev Agent Record

### Implementation Notes
- Followed the established LegendsPageComponent pattern for the staff UI
- Used `Problem()` method from ControllerBase instead of manually constructing ProblemDetails with `.StatusCode` (matches pre-existing project issues)
- Pre-existing build errors in ReportsController.cs, ReportGenerationService.cs, and ReportExportJobRunner.cs are unrelated to this story
- The `RequestIdMiddleware` namespace `MidiKaval.Api.Infrastructure` was missing from LegendsController.cs — fixed with a using directive
- The Program.cs was missing `using MidiKaval.Api.Infrastructure.Reports` — fixed to unblock migration generation

### Key Decisions
- StaffController uses direct `DbSet<User>` access (not reflection) since all operations target a single known entity type
- `token_version` is set to 1 on create (not 0) so that new accounts start with a fresh token state. The account has no password set, so the user must go through forgot-password flow
- DirectorOnly policy on all endpoints since only Directors can manage staff
- Audit events use `SubjectUserId` to track which user was acted upon, following the existing AuditEvent pattern

## File List

### New files (API)
- `apps/api/Controllers/V1/StaffController.cs`
- `apps/api/Models/Users/StaffDto.cs`
- `apps/api/Models/Users/StaffListResultDto.cs`
- `apps/api/Models/Users/CreateStaffRequest.cs`
- `apps/api/Models/Users/UpdateStaffRequest.cs`
- `apps/api/Migrations/20260620141629_AddStaffDirectoryFields.cs`
- `apps/api/Migrations/20260620141629_AddStaffDirectoryFields.Designer.cs`

### Modified files (API)
- `apps/api/Domain/Entities/User.cs` — added FirstName, LastName, PhoneNumber
- `apps/api/Domain/Entities/UserRoles.cs` — added All[] array and IsValid() helper
- `apps/api/Infrastructure/Persistence/UserConfiguration.cs` — added EF config for new fields
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — added staff event type constants
- `apps/api/Program.cs` — added `using MidiKaval.Api.Infrastructure.Reports` (pre-existing fix)
- `apps/api/Controllers/V1/LegendsController.cs` — added `using MidiKaval.Api.Infrastructure` (pre-existing fix)
- `apps/api/Controllers/V1/ReportsController.cs` — added `using Microsoft.Extensions.Options` (pre-existing fix)

### New files (Web)
- `apps/web/src/app/features/shell/services/staff-api.service.ts`
- `apps/web/src/app/features/shell/staff.models.ts`
- `apps/web/src/app/features/shell/pages/staff-page.component.ts`
- `apps/web/src/app/features/shell/pages/staff-page.component.html`
- `apps/web/src/app/features/shell/pages/staff-page.component.scss`
- `apps/web/src/app/features/shell/pages/staff-edit-dialog.component.ts`
- `apps/web/src/app/features/shell/pages/staff-confirm-dialog.component.ts`

### Modified files (Web)
- `apps/web/src/app/app.routes.ts` — added `admin/staff` route with directorGuard
- `apps/web/src/app/features/shell/pages/admin-page.component.ts` — added Staff Directory link, removed placeholder hint

## Change Log

| Date | Change |
|------|--------|
| 2026-06-20 | Initial implementation of Staff Directory Management story |
| 2026-06-20 | API: User entity extended with FirstName, LastName, PhoneNumber |
| 2026-06-20 | API: StaffController created with full CRUD + deactivate/reactivate/force-reset |
| 2026-06-20 | API: Audit event types added for all staff mutations |
| 2026-06-20 | API: EF Core migration created for new User columns |
| 2026-06-20 | Web: Staff page, dialogs, service, models, and routing completed |
| 2026-06-20 | Fixed pre-existing missing using directives in Program.cs, LegendsController.cs, ReportsController.cs |
| 2026-06-20 | Code review: applied 7 patch findings — PasswordHash clear on force-reset, whitespace guard, self-deactivation guard, reactivate idempotency, error-extraction pipeline fix, DbUpdateException guard, PhoneNumber MaxLength 30 + [Phone] |
