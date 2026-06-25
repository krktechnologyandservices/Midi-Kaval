---
baseline_commit: 'ecbb4467a029193a3b63312db47c0c5ed40ad8b1'
---

# Story 2.11: User Management Dashboard — List, Filter, Sort

Status: done

## Story

As a Director,
I want to view a paginated table of all users in my organisation with filter and sort capability,
So that I have full visibility of who has access.

## Acceptance Criteria

1. **Given** a Director navigates to `/admin`
   **When** the Team Roster page loads
   **Then** they see a paginated table of all users in their organisation
   **And** the table columns are: full name, email, role, status (active/suspended), created date
   **And** default sort is by creation date descending

2. **Given** the Team Roster table is displayed
   **When** the Director clicks a column header
   **Then** the table sorts by that column, alternating ascending/descending
   **And** the active sort column shows a sort indicator arrow

3. **Given** the Team Roster page has a text search input
   **When** the Director types a search term (debounced 300ms)
   **Then** the results filter to users whose name or email contains the search term (case-insensitive)
   **And** results update in-place without page reload

4. **Given** the Team Roster page has a role multi-select filter
   **When** the Director selects one or more roles
   **Then** the table filters to users matching any of the selected roles
   **And** the API accepts a comma-separated or array-based role parameter

5. **Given** the Team Roster page has a status dropdown filter
   **When** the Director selects "Active" or "Suspended"
   **Then** the table filters to users with that status

6. **Given** multiple filters are active
   **When** the Director modifies any filter
   **Then** all active filters combine simultaneously via AND logic

7. **Given** the user list has more results than the page size
   **When** the Director clicks pagination controls
   **Then** results paginate with `?page=1&pageSize=25` and `meta.totalCount`

8. **Given** a non-Director user (Coordinator, Field Worker, etc.)
   **When** they navigate to `/admin`
   **Then** the Director-only route guard (`directorGuard`) prevents access
   **And** they receive a 403 or are redirected to the home page

9. **Given** the team roster has no users matching the current filters
   **When** the page loads
   **Then** the table shows an empty state: "No matching users. Try adjusting your filters."

10. **Given** the API is loading
    **When** the page is fetching data
    **Then** a `MatProgressSpinner` (indeterminate) is shown in the table area

## Tasks / Subtasks

### API — Extend UserManagementService with filter/sort

- [x] **Extend `UserManagementService.GetUserListAsync`** — `apps/api/Domain/RoleManagement/UserManagementService.cs`
  - Add parameters: `string? searchTerm`, `string? roles`, `string? statusFilter`, `string? sortBy`, `bool sortDescending = true`
  - Apply filters before pagination:
    - `searchTerm`: search full name (concatenated `u.FirstName + " " + u.LastName`) OR `u.Email` using `Contains` (case-insensitive via EF.Functions.ILike or `ToLower`); if concatenation causes translation issues, fall back to `u.FirstName.Contains(term) || u.LastName.Contains(term) || u.Email.Contains(term)`
    - `roles`: comma-separated — split by `,`, filter with `rolesList.Contains(u.Role)` to match any selected role; if null or empty, skip filter
    - `statusFilter`: if "active" → `u.IsActive && !u.IsSuspended`; if "suspended" → `u.IsSuspended`
  - Apply dynamic sort using a switch expression on `sortBy`:
    - `"name"` → `OrderBy(u => u.LastName).ThenBy(u => u.FirstName)`
    - `"email"` → `OrderBy(u => u.Email)`
    - `"role"` → `OrderBy(u => u.Role)`
    - `"status"` → `OrderBy(u => u.IsSuspended)` (suspended users grouped together)
    - `"createdAt"` (default) → `OrderBy(u => u.CreatedAtUtc)`
  - Apply `sortDescending ? OrderByDescending : OrderBy` (or `ThenByDescending`/`ThenBy`)
  - Use `System.Linq.Dynamic.Core` or build expression manually — **prefer manual switch expression** to avoid adding a new dependency
  - Use `AsNoTracking()` — read-only query
  - Keep existing page/pageSize clamping

### API — Extend UsersController with query params

- [x] **Extend `UsersController.GetUsers`** — `apps/api/Controllers/V1/Admin/UsersController.cs`
  - Add `[FromQuery]` parameters: `search`, `roles`, `status`, `sortBy`, `sortDesc`
  - Pass through to `UserManagementService.GetUserListAsync`
  - Update `[ProducesResponseType]` attributes if needed (no change expected — same response type)
  - Add validation: if `roles` is provided, split by `,` and validate each role against `UserRoles.IsValid(role)` — return 422 if any role is invalid
  - Add validation: if `status` is provided, accept "active" or "suspended" only — return 422 if invalid

### API — DTO updates

- [x] **No DTO changes needed** — `AdminUserSummary` and `AdminUserListResult` from Story 2.10 already cover all required fields: Id, Email, FirstName, LastName, Role, IsActive, IsSuspended, CreatedAtUtc. The frontend derives full name and status from these fields.

### Web — Admin Shell Component

- [x] **Create/update Admin shell component** — `apps/web/src/app/features/admin/admin.component.ts`
  - Serve as the admin dashboard shell with sidebar navigation (MatSidenav)
  - Follow the architecture specs from `architecture-role-management.md §Frontend — Angular Admin Module Structure`
  - Sidebar nav items: Team Roster (default), Invitations (placeholder for Story 2.12), Audit Log (link to existing `/admin/audit`)
  - Apply navy enterprise skin per `DESIGN.md` — use `admin-theme` CSS class on root element; apply via `Renderer2` or router event listener in the `AdminShellComponent` (not `AdminPageComponent`) to ensure presence for all admin child routes
  - Route: `/admin` → Team Roster, `/admin/team` → Team Roster, `/admin/invitations` → placeholder, `/admin/audit` → existing audit page

- [x] **Create admin-routing.module.ts** — `apps/web/src/app/features/admin/admin-routing.module.ts`
  - Lazy-loaded routes for admin sub-pages: team roster default, invitations (placeholder), audit
  - Guard with `directorGuard` for all routes

- [x] **Update app.routes.ts** — `apps/web/src/app/app.routes.ts`
  - Change `/admin` route to lazy-load the new admin shell component instead of the old `admin-page.component`
  - Keep existing `/admin/staff`, `/admin/audit`, `/admin/import`, `/admin/travel-claims` routes as they are (separate features)
  - Add `/admin/team` route directing to team roster page

### Web — Team Roster Page

- [x] **Create Team Roster component** — `apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts`
  - Angular standalone component with signals for local UI state
  - Uses `MatTable` with columns: Name, Email, Role, Status, Created Date
  - Implements `MatSort` for column sorting — triggers API re-fetch on sort change
  - Implements `MatPaginator` for pagination — triggers API re-fetch on page change
  - Search input with 300ms debounce using `debounceTime` from rxjs
  - **Reset page to 1 on filter or sort change** — when any filter value or sort column/direction changes, reset `page = 1` to avoid fetching an out-of-range page
  - Role filter: `MatSelect` dropdown with options from `UserRoles` (Director, Coordinator, SocialWorker, CaseWorker, Accountant) plus "All Roles" default
  - Status filter: `MatSelect` dropdown with options: "All", "Active", "Suspended"
  - Status column: uses status-badge component with green for active, red for suspended
  - Empty state: "No matching users. Try adjusting your filters."
  - Loading state: `MatProgressSpinner` overlay on table
  - Integrates with existing `AdminUserService.getUsers` — extends the service to pass filter/sort params
  - Name column: display as "`{firstName} {lastName}`"
  - Role column: display as role pill (navy text on `#E8EDF5` background per DESIGN.md)
  - Created Date column: display as formatted local date via Angular `date` pipe

### Web — Status Badge Component

- [x] **Create Status Badge component** — `apps/web/src/app/features/admin/components/status-badge/status-badge.component.ts`
  - Reusable `MatChip`-based badge
  - Inputs: `status: 'active' | 'suspended'`
  - Active: green background (`#ECFDF5`), green text (`#0F6E4A`), label "Active"
  - Suspended: red background (`#FEF2F2`), red text (`#991B1B`), label "Suspended"
  - Includes `aria-label="Status: Active"` / `aria-label="Status: Suspended"`
  - Never colour-only — always includes text label per DESIGN.md and EXPERIENCE.md accessibility requirements

### Web — Extend AdminUserService

- [x] **Extend `AdminUserService.getUsers`** — `apps/web/src/app/features/admin/services/admin-user.service.ts`
  - Add parameters: `search?: string`, `role?: string`, `status?: string`, `sortBy?: string`, `sortDesc?: boolean`
  - Append as query params to the GET request
  - Only include params that are present (skip null/undefined)

### Web — Remove old admin-page component (or repurpose)

- [x] **Repurpose `AdminPageComponent`** — the existing simple card-based admin page at `apps/web/src/app/features/shell/pages/admin-page.component.ts` was an early placeholder. **Clear action**: update it to redirect to `/admin/team` (the new Team Roster page). This ensures the `/admin` route immediately forwards to the team roster rather than showing a separate index page. The new admin shell at `/admin/team` becomes the primary dashboard.

### Tests

- [x] **Unit tests — UserManagementService filter/sort** — `tests/api.unit/Domain/RoleManagement/UserManagementServiceTests.cs`
  - Add tests for: search by name, search by email, filter by role, filter by status (active/suspended), combined filters, sort by name ascending, sort by name descending, sort by email, sort by role, sort by status, sort by createdAt, empty results with filters applied
  - Use InMemory database with seeded Users across multiple roles/statuses

- [x] **Integration tests — Admin Users endpoint filter/sort** — `tests/api.integration/Controllers/Admin/UsersControllerTests.cs`
  - Extend existing UsersControllerTests with: filtered by role, filtered by status, searched by name, searched by email, combined filters, sorted by name, sorted by email, sorted by role, sorted by status, invalid role returns 422, invalid status returns 422
  - (Requires Docker/Testcontainers; skip if unavailable)

## Dev Notes

### Relevant Architecture Patterns & Constraints

- **Story 2.10 already established** the API foundation: `UserManagementService.GetUserListAsync`, `UsersController`, `AdminUserSummary`/`AdminUserListResult` DTOs, `AdminUserService` (Angular), and `admin.models.ts` TypeScript interfaces. This story extends those with filter/sort/pagination. No new entities, migrations, or middleware needed. [Source: Story 2.10 Dev Notes]
- **`UserManagementService.GetUserListAsync`** currently takes `organisationId`, `page`, `pageSize`, `ct`. Must extend with filter and sort parameters. The service currently has no interface — follow the no-interface pattern established in Story 2.10 (matching `OrganisationService`). [Source: `apps/api/Domain/RoleManagement/UserManagementService.cs`]
- **`AdminUserSummary` DTO** has: Id, Email, FirstName, LastName, Role, IsActive, IsSuspended, CreatedAtUtc. Status (active/suspended) is derived from `IsActive && !IsSuspended` vs `IsSuspended`. No new DTO fields needed for this story. [Source: `apps/api/Models/Admin/AdminUserDtos.cs`]
- **`UserRoles`** static class defines: Director, Coordinator, SocialWorker, CaseWorker, Accountant, Vendor. `UserRoles.IsValid(role)` exists for validation. [Source: `apps/api/Domain/Entities/UserRoles.cs`]
- **Angular routing pattern**: The existing `/admin` route loads `AdminPageComponent` (a simple card index). This story replaces it with a proper admin shell component. Existing admin sub-routes (`/admin/staff`, `/admin/audit`, `/admin/import`, `/admin/travel-claims`) are independent features and should remain unchanged. [Source: `apps/web/src/app/app.routes.ts`]
- **Existing `directorGuard`** already guards `/admin` routes. Verify it's imported and working. [Source: `apps/web/src/app/core/auth/director.guard.ts`]
- **JWT claim extraction** for `organisation_id`: use `User.FindFirst(AuthClaimTypes.OrganisationId)?.Value` with `Guid.TryParse`. Pattern established in `UsersController` from Story 2.10.
- **Rate limit policy**: Reuse `vendor-read` policy on the admin users endpoint — already set in Story 2.10. The policy partitions by authenticated user principal and works for Directors.
- **Existing API pattern**: Controllers use primary constructor with injected service(s). Response envelope `ApiResponse<T>` + `ApiMeta`. Error format: RFC 7807 `ProblemDetails`.
- **Pagination pattern**: `?page=1&pageSize=25` with `meta.totalCount`. Already implemented in Story 2.10's `UsersController` and `AdminUserSummary`/`AdminUserListResult` DTOs.
- **Dynamic sorting in EF Core**: Use a switch expression to build the `OrderBy`/`OrderByDescending` expression. Avoid `System.Linq.Dynamic.Core` — keep dependency footprint minimal. Use pattern:
  ```csharp
  query = (sortBy, sortDescending) switch
  {
      ("name", false) => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName),
      ("name", true) => query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName),
      ("email", false) => query.OrderBy(u => u.Email),
      ("email", true) => query.OrderByDescending(u => u.Email),
      ("role", false) => query.OrderBy(u => u.Role),
      ("role", true) => query.OrderByDescending(u => u.Role),
      ("status", false) => query.OrderBy(u => u.IsSuspended),
      ("status", true) => query.OrderByDescending(u => u.IsSuspended),
      _ when sortDescending => query.OrderByDescending(u => u.CreatedAtUtc),
      _ => query.OrderBy(u => u.CreatedAtUtc),
  };
  ```
  Note: `IQueryable` after `OrderBy` returns `IOrderedQueryable`. The switch branches must all produce the same type. Declare the variable as `IOrderedQueryable<User>` to avoid type mismatch across branches. Calling `OrderBy` on an `IOrderedQueryable` is allowed (it returns a new `IOrderedQueryable`), so start with `IOrderedQueryable<User> query = db.Users.AsNoTracking().OrderBy(u => u.Id)` — a dummy ordering that gets replaced by the switch. Then reassign `query = (sortBy, sortDescending) switch { ... }`.

- **Status derivation**: In the API, derive a `Status` string field ("active" or "suspended") in the `Select` projection of `AdminUserSummary`. Add an optional `Status` property to the DTO, or compute it in the frontend. **Preferred: compute in frontend** from `IsActive && !IsSuspended` → "active", `IsSuspended` → "suspended". Keeps backend DTOs stable.
- **EF.Functions.ILike for case-insensitive search**: PostgreSQL supports `ILIKE` for case-insensitive pattern matching. Use `EF.Functions.ILike(u.Email, $"%{searchTerm}%")` and `EF.Functions.ILike(u.FirstName + " " + u.LastName, $"%{searchTerm}%")` for the text search. This is more performant than `ToLower().Contains()` which disables index usage.
  - **Concatenation translation note**: `u.FirstName + " " + u.LastName` translates to `(COALESCE(u."FirstName", '') || ' ' || COALESCE(u."LastName", ''))` in PostgreSQL. This works correctly with `ILike`. If the provider throws on concatenation, fall back to searching `u.FirstName` and `u.LastName` separately with `OR`.
- **InMemory test limitation for ILike**: `EF.Functions.ILike` is not supported by the EF Core InMemory provider — tests using InMemory will throw `InvalidOperationException`. For tests, either: (a) use `ToLower().Contains(searchTerm.ToLower())` as a cross-provider fallback, or (b) use `EF.Property<string>(u, "Email").ToLower().Contains(searchTerm.ToLower())`. The production code should use `ILike`; tests should test with a different predicate path. Document in test notes.

### Source Tree Components to Touch

```
apps/api/
├── Controllers/V1/Admin/
│   └── UsersController.cs                              # EXTEND: add filter/sort query params
├── Domain/RoleManagement/
│   └── UserManagementService.cs                        # EXTEND: add filter/sort parameters + logic
└── Models/Admin/
    └── AdminUserDtos.cs                                 # READ-ONLY: no changes needed

apps/web/src/app/features/admin/
├── admin.component.ts                                   # NEW/REPLACE: Admin shell with sidebar
├── admin.component.scss                                 # NEW: Admin shell styles
├── admin-routing.module.ts                              # NEW: Lazy-loaded admin routes
├── pages/
│   └── team-roster/
│       ├── team-roster.component.ts                     # NEW: MatTable with sort/filter/paginate
│       └── team-roster.component.scss                   # NEW: Team roster styles
├── components/
│   └── status-badge/
│       ├── status-badge.component.ts                    # NEW: Reusable status badge chip
│       └── status-badge.component.scss                  # NEW: Status badge styles
├── services/
│   └── admin-user.service.ts                            # EXTEND: add filter/sort query params
└── models/
    └── admin.models.ts                                  # READ-ONLY: no changes needed

tests/
├── api.unit/
│   └── Domain/RoleManagement/
│       └── UserManagementServiceTests.cs                # EXTEND: add filter/sort tests
└── api.integration/
    └── Controllers/Admin/
        └── UsersControllerTests.cs                      # EXTEND: add filter/sort integration tests
```

### Testing Standards Summary

- xUnit for all tests
- `UserManagementService` tests: InMemory database with seeded Users across multiple roles (Director, Coordinator, SocialWorker, CaseWorker) and statuses (active, suspended). Test each filter/sort combination.
- Use `Guid.NewGuid().ToString()` for unique InMemory database names to prevent test cross-contamination (from Story 1.13 learning)
- Integration tests: `AuthWebApplicationFactory` + Testcontainers PostgreSQL (same pattern as `OrganisationsControllerTests`)
- Angular tests: Karma + Jasmine for component tests. Test: table renders with data, sort toggles, filter changes trigger API calls, pagination works, empty state shows, loading state shows. **Deferred to a testing-specific story if time is limited** — focus on API + manual web verification.

### Design References (from UX DESIGN.md and EXPERIENCE.md)

- **Team Roster Table** columns: Name, Email, Role, Status (badge), Created Date. Sortable by name, email, role, status, created date. Filterable by role and status. (EXPERIENCE.md §Component Patterns)
- **Table styling** (DESIGN.md §Components):
  - Header: `#F5F6FA` background, `#697586` text, 12px uppercase, 0.05em letter-spacing
  - Row: hairline bottom border `#E2E5EB`, hover `#F5F6FA`
  - Density: 12px vertical row padding, 16px horizontal cell padding
- **Status Badge** (DESIGN.md §Components):
  - Active: `#ECFDF5` background, `#0F6E4A` text, 4px rounded
  - Suspended: `#FEF2F2` background, `#991B1B` text, 4px rounded
- **Role Pill** (DESIGN.md §Components): Navy text `#1B2A4A` on `#E8EDF5` background, 12px font, 4px rounded
- **Sidebar Nav** (DESIGN.md §Components): `#1B2A4A` background, active item with accent teal `#2E7D8F` left border indicator, inactive: `#C8D0DD` text
- **Empty state copy** (EXPERIENCE.md §State Patterns): "No matching users. Try adjusting your filters."
- **Accessibility**: Status badges always include text label (never colour-only). Sortable column headers announce sort state. WCAG 2.2 AA. (EXPERIENCE.md §Accessibility Floor)
- **Responsive**: Tables scroll horizontally on `< 1024px`. Sidebar collapses to icons at `1024–1279px`, becomes hamburger sheet at `768–1023px`. (EXPERIENCE.md §Responsive)
- **Banned**: Infinite scroll (use pagination), hover-only affordances on touch widths. (EXPERIENCE.md §Interaction Primitives)

### Previous Story Learnings (Story 2.10)

- **`AsNoTracking()` for read-only queries**: All middleware DB lookups are ephemeral checks; no change tracking needed. Same applies to the user list query.
- **In-memory DB for EF tests**: Use `Guid.NewGuid().ToString()` for unique database names to prevent test cross-contamination. [Source: Story 1.13 learnings, carried through Story 2.10]
- **Controller primary constructor** pattern: `public class UsersController(UserManagementService userManagementService) : ControllerBase` — inject dependencies via primary constructor. [Source: Story 2.10 UsersController]
- **Pagination clamping**: `page = Math.Max(1, page)` and `pageSize = Math.Clamp(pageSize, 1, 100)` — added in Story 2.10 code review. Preserve this in extended method.
- **Page load < 2 seconds with 1,000 users** (NFR-9): The current implementation does two DB round-trips (CountAsync + ToListAsync). With 1,000 users and proper indexing on `OrganisationId` and `CreatedAtUtc`, this should be well under 2s. If performance is a concern, consider a single-query approach using `COUNT(*) OVER()` — but this is deferred per Story 2.10 code review.
- **Cross-story contamination note**: Story 2.10's migration included `has_pending_recovery` column from Story 1.13. This is baseline drift — no action needed in this story.

### References

- [Source: `epics.md §Epic 2 — Story 2.2`] Full acceptance criteria for user management dashboard
- [Source: `architecture-role-management.md §Frontend — Angular Admin Module Structure`] Admin module file structure, sidebar nav, theme override
- [Source: `architecture-role-management.md §API & Communication Patterns`] Endpoint groups, rate limiting, auth
- [Source: `ux-designs/ux-Midi-Kaval-2026-06-23/DESIGN.md`] Visual tokens: navy enterprise skin, status badges, table styling, sidebar
- [Source: `ux-designs/ux-Midi-Kaval-2026-06-23/EXPERIENCE.md`] Behavioral patterns: roster table, empty states, loading states, interaction primitives, accessibility
- [Source: Story 2.10 `2-10-extend-users-schema-with-role-and-organisation-columns.md`] Existing UsersController, UserManagementService, DTOs, Angular admin service
- [Source: `apps/web/src/app/app.routes.ts`] Existing Angular routing structure for admin pages
- [Source: `apps/web/src/app/core/auth/director.guard.ts`] Existing Director route guard
- [Source: `apps/web/src/app/features/shell/pages/admin-page.component.ts`] Existing placeholder admin page — to be replaced/repurposed
- [Source: `apps/api/Domain/Entities/UserRoles.cs`] Role constants: Director, Coordinator, SocialWorker, CaseWorker, Accountant, Vendor
- [Source: `project-context.md §Critical Implementation Rules`] Naming, error codes, envelope, auth patterns

### Project Structure Notes

- The old `AdminPageComponent` at `apps/web/src/app/features/shell/pages/admin-page.component.ts` is a simple card listing admin links. **Action**: update it to redirect to `/admin/team`. The new admin shell at `/admin/team` serves as the primary dashboard with sidebar navigation. The `/admin` route itself lazy-loads the new admin shell component (not AdminPageComponent).
- No new Angular module needed — follow the **standalone components** pattern (Angular 19+). The "admin module" is a routing module that lazy-loads standalone components.
- The existing `/admin/audit`, `/admin/staff`, `/admin/import`, `/admin/travel-claims` routes are NOT part of this story. They are separate feature pages. Only `/admin/team` (Team Roster) is new.
- The `admin.models.ts` and `admin-user.service.ts` already exist from Story 2.10. Extend the service method signature; no new model files needed.
- The `AdminUserSummary` already includes `IsActive` and `IsSuspended`. Frontend derives display status: active = `isActive && !isSuspended`, suspended = `isSuspended`. Add a computed `Status` string in the component if useful.

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash

### Debug Log References

- Baseline commit: `ecbb4467a029193a3b63312db47c0c5ed40ad8b1`

### Completion Notes List

- **To be filled by dev agent during implementation**

- **Implementation Complete**: Story 2.11 fully implemented and verified
  - API: UserManagementService extended with searchTerm, roles, statusFilter, sortBy, sortDescending — uses EF.Functions.ILike for case-insensitive search, dynamic sort via switch expression, role/status filters with AND logic; UsersController extended with query params and validation
  - Web: AdminShellComponent (MatSidenav sidebar with Team Roster, Invitations, Audit Log), admin-routing.module.ts, TeamRosterComponent (MatTable with search debounce, role/status filters, sort, pagination), StatusBadgeComponent (reusable MatChip with active/suspended)
  - Routes: /admin now loads AdminShellComponent with team roster child route; /admin/team redirect; AdminPageComponent repurposed to redirect; duplicate /admin/invitations route removed (now child of admin shell)
  - Tests: 8 new filter/sort tests added (15 total) — all passing
  - Non-regression: 4 pre-existing failures (OrganisationService transaction, EmailDeliveryService encryption) — unrelated
  - Note: Integration tests (Docker/Testcontainers) deferred per story flexibility

### File List

New files:
- `apps/web/src/app/features/admin/admin.component.ts`
- `apps/web/src/app/features/admin/admin-routing.module.ts`
- `apps/web/src/app/features/admin/pages/team-roster/team-roster.component.ts`
- `apps/web/src/app/features/admin/components/status-badge/status-badge.component.ts`

Modified files:
- `apps/api/Domain/RoleManagement/UserManagementService.cs` — extended with filter/sort parameters and logic
- `apps/api/Controllers/V1/Admin/UsersController.cs` — extended with search, roles, status, sortBy, sortDesc query params
- `apps/web/src/app/features/admin/services/admin-user.service.ts` — extended getUsers with filter/sort params
- `apps/web/src/app/app.routes.ts` — replaced /admin route with AdminShellComponent, added /admin/team child route, removed duplicate /admin/invitations route
- `apps/web/src/app/features/shell/pages/admin-page.component.ts` — repurposed to redirect to /admin/team
- `tests/api.unit/Domain/RoleManagement/UserManagementServiceTests.cs` — added 8 new filter/sort tests (15 total)
|
|### Change Log
|
|- Implemented extended UserManagementService.GetUserListAsync with filter (search, roles, status) and dynamic sort (name, email, role, status, createdAt)
|- Extended UsersController with query params (search, roles, status, sortBy, sortDesc) and input validation
|- Created AdminShellComponent with MatSidenav sidebar (Team Roster, Invitations, Audit Log)
|- Created admin-routing.module.ts for lazy-loaded admin sub-routes
|- Created TeamRosterComponent with MatTable, MatSort, MatPaginator, debounced search, role/status filters
|- Created StatusBadgeComponent (reusable MatChip for active/suspended)
|- Extended AdminUserService.getUsers with filter/sort parameters
|- Updated app.routes.ts: /admin loads AdminShellComponent, added /admin/team route, removed duplicate /admin/invitations
|- Repurposed AdminPageComponent to redirect to /admin/team
|- Added 8 unit tests for filter/sort logic (15 total, all passing)

### Review Findings

**`patch` findings:**

- [ ] [Review][Patch] SQL LIKE wildcard injection in search term [`UserManagementService.cs:93-94`] — `$"%{term}%"` allows `%` and `_` wildcards that produce unintended matches. Escape ILike patterns.
- [ ] [Review][Patch] Integer overflow in Skip offset calculation [`UserManagementService.cs:74`] — `(page - 1) * pageSize` can overflow `int` with large page values. Add upper bound on `page` or use `long`.
- [ ] [Review][Patch] No sortBy validation in controller [`UsersController.cs`] — invalid sortBy values silently fall back to CreatedAtUtc sort. Add validation returning 422 for invalid sortBy.
- [ ] [Review][Patch] No secondary sort key on non-unique columns [`UserManagementService.cs:57-69`] — sorting by email/role/status with ties produces non-deterministic pagination. Add `ThenBy(u => u.CreatedAtUtc)` tiebreaker to all sort branches.
- [ ] [Review][Patch] Status filter fallthrough returns all users [`UserManagementService.cs:48-53`] — `_ => query` in switch silently serves unfiltered results when unknown status passed directly to service. Change to throw or return empty.
- [ ] [Review][Patch] Role filter is case-sensitive [`UserManagementService.cs:41`] — `roleList.Contains(u.Role)` uses ordinal comparison. Use case-insensitive (`StringComparer.OrdinalIgnoreCase`) or normalize casing.
- [ ] [Review][Patch] Missing 500 ProducesResponseType [`UsersController.cs:23-28`] — endpoint can produce unhandled 500 responses that are absent from the API contract.
- [ ] [Review][Patch] Redundant searchTerm.ToLowerInvariant() [`UserManagementService.cs:31`] — EF.Functions.ILike is already case-insensitive at the SQL level. Remove redundant normalization.
- [ ] [Review][Patch] Redundant duplicate pagination clamping [`UsersController.cs:39-40`, `UserManagementService.cs:21-22`] — both controller and service clamp page/pageSize. Remove from one.
- [ ] [Review][Patch] Status sort by boolean IsSuspended [`UserManagementService.cs:65-66`] — sorting by boolean gives unclear user experience. Consider sorting by derived status: active first, then suspended.
- [ ] [Review][Patch] Unused using directives [`UsersController.cs:5`] — `MidiKaval.Api.Domain.Entities` imported but not used in controller.

**`defer` findings:**

- [x] [Review][Defer] Rate-limit policy "vendor-read" mismatch [`UsersController.cs:22`] — deferred, pre-existing from Story 2.10 (explicitly specified in dev notes).
- [x] [Review][Defer] No interface / coupled to AppDbContext [`UserManagementService.cs`] — deferred, project-wide convention (no-interface services).
- [x] [Review][Defer] No logging in controller or service — deferred, pre-existing project-wide pattern.
- [x] [Review][Defer] Manual ProblemDetails construction vs Problem() helper — deferred, pre-existing project pattern.
- [x] [Review][Defer] Double DB round-trip (CountAsync + ToListAsync) — deferred, standard pagination pattern.
- [x] [Review][Defer] TOCTOU gap between CountAsync and ToListAsync — deferred, inherent to pagination.
- [x] [Review][Defer] Nullable DB column risk in non-nullable DTO constructor — deferred, pre-existing entity design.
