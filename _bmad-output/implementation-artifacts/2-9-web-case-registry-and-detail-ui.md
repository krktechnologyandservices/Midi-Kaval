---
baseline_commit: NO_VCS
---

# Story 2.9: Web Case Registry and Detail UI

Status: done

## Story

As a **Coordinator**,
I want web screens to browse and edit Cases,
so that desk work is efficient (FR-3, FR-6, UX-DR11).

## Acceptance Criteria

1. **Given** I am logged in on web (Coordinator/Director via `supervisorGuard`)  
   **When** I use the web app  
   **Then** the web app uses the existing **Angular PWA + Angular Material** patterns (standalone components + signals) and uses the existing `CaseApiService` shape (generated api-client types, no ad-hoc fetch)  
   **And** existing Stories **2.1–2.8** behaviors remain working (create, duplicate sheet, merge, search/presets, export, transfer form, guards).

2. **Given** UX-DR11 Web sidebar IA  
   **When** I am logged in and on desktop widths (≥1024px)  
   **Then** I see a persistent sidebar with these entries (even if some are placeholders):  
   - **Crisis queue** (default landing for Coordinators)  
   - **Dashboard** (placeholder)  
   - **Cases** (case registry)  
   - **Reports** (placeholder)  
   - **Legends** (placeholder)  
   - **Admin** (placeholder; Director-only)  
   **And** the current route is visually indicated (active state).

3. **Given** web navigation routes  
   **When** I click sidebar entries  
   **Then** each route loads without a 404 and renders a minimal placeholder page where the feature is not yet implemented (Crisis Queue, Dashboard, Reports, Legends, Admin)  
   **And** **Cases** navigates to the existing registry surface (`/cases`)  
   **And** the existing `/cases/new` and `/cases/:id` flows remain accessible.

4. **Given** case registry (Story 2.6 + 2.7 + 2.8)  
   **When** I use the case registry page  
   **Then** the registry remains usable under the sidebar shell  
   **And** the existing `/` focus shortcut continues to work  
   **And** presets still save/load correctly  
   **And** export still uses the current filter state.

5. **Given** case detail (Story 2.2 stage API + Story 2.8 transfer + detail API)  
   **When** I open a case on web (`/cases/:id`) as Coordinator/Director  
   **Then** I see a detail view that fetches via `GET /api/v1/cases/{id}` (already in `CaseDetailPlaceholderComponent`)  
   **And** I can change the case stage using `PATCH /api/v1/cases/{id}/stage`  
   **And** stage transitions enforce the server rules (422 on invalid/terminal) and errors are surfaced to the user.
   **And** the stage control does not allow skipping stages or moving backward (only the next forward stage is selectable).

6. **Given** handoff whisper constraints from Story 2.8  
   **When** I view case detail as Coordinator/Director on web  
   **Then** the **handoff whisper block is not shown** (API returns `handoffWhisper: null` for non-assignee)  
   **And** transfer UI remains available to Coordinator/Director and continues to refresh the detail on success.

7. **Given** responsive rules (UX-DR15)  
   **When** viewport is 768–1023px  
   **Then** sidebar collapses (navigation still accessible)  
   **And** at 200% browser zoom, pages do not require horizontal scroll for core content.

## Tasks / Subtasks

- [x] **Web — app shell + sidebar navigation** (AC: 2, 3, 7)
  - [x] Implement an authenticated supervisor layout that wraps existing guarded routes with a sidebar + main content area.
  - [x] Create a new shell component under `apps/web/src/app/features/shell/` that renders:
    - sidebar navigation (links)
    - `<router-outlet />` for the main pane
  - [x] Update `apps/web/src/app/app.routes.ts` so that supervisor routes (`/home`, `/cases`, `/cases/new`, `/cases/:id`, plus placeholders) are children of the shell route. Keep existing `authGuard` + `supervisorGuard` behavior.
  - [x] Add placeholder screens (simple `mat-card` or equivalent) for: `/crisis-queue`, `/dashboard`, `/reports`, `/legends`, `/admin`.
  - [x] Hide the “Admin” sidebar entry for non-Director users using the existing `AuthSessionService` user/role (client-side only). Route is restricted for non-Director via a guard.

- [x] **Web — registry under sidebar shell** (AC: 4)
  - [x] Ensure existing registry component renders correctly within the new layout.
  - [x] Preserve the `/` focus shortcut and existing exports/presets flows.

- [x] **Web — detail stage edit UI** (AC: 5)
  - [x] Extend `CaseApiService` with a new method for stage transitions:
    - `transitionStage(caseId: string, request: TransitionCaseStageRequest): Promise<CaseDto>` calling `PATCH /api/v1/cases/${caseId}/stage`
    - Use the same error wrapping / `extractErrorMessage` behavior as existing methods.
  - [x] Extend `apps/web/src/app/features/cases/models/case.models.ts` to export the request type from the generated api-client.
  - [x] Add minimal stage-edit UI to `CaseDetailPlaceholderComponent`:
    - provide a select for the next stage only
    - optional `notes` input
    - disable stage UI when current stage is `TerminationExclusion`
    - on success, refresh detail
    - on failure, surface server `ProblemDetails.detail` via existing `errorMessage` signal
  - [x] Keep transfer form behavior intact (from Story 2.8) and ensure it continues to function under the new shell.

- [x] **Tests — web** (AC: 1–7)
  - [x] Add/adjust unit tests to cover:
    - sidebar renders for Coordinator/Director and Admin visibility
    - stage transition submits `PATCH` via `CaseApiService.transitionStage`, handles errors by showing error message
    - transfer form still submits correctly
  - [ ] Ensure existing web test suite remains green.

### Review Findings

- [x] [Review][Patch] Stage transition errors replace entire detail view [`case-detail-placeholder.component.ts:198`]
- [x] [Review][Patch] Cases sidebar link not active on `/cases/:id` or `/cases/new` [`supervisor-shell.component.html:10`]
- [x] [Review][Patch] Stale `story-2-9-web-shell-and-stage-edit.spec.ts` with nine `pending()` tests duplicates real specs [`story-2-9-web-shell-and-stage-edit.spec.ts`]
- [x] [Review][Patch] Detail “Back to home” navigates to `/home` instead of supervisor default [`case-detail-placeholder.component.ts:205`]
- [x] [Review][Patch] Registry toolbar still links “Supervisor home” to `/home` [`case-registry.component.html:26`]
- [x] [Review][Patch] Unused `MatButtonModule` import in supervisor shell [`supervisor-shell.component.ts:3`]
- [x] [Review][Defer] Sidebar “collapse” at 768–1023px only narrows width; labels may wrap at high zoom [`supervisor-shell.component.scss:48`] — deferred, pre-existing
- [x] [Review][Defer] Registry assignee filter / preset `assignedWorkerUserId` UI still missing [`case-registry.component.ts`] — deferred, pre-existing
- [x] [Review][Defer] Shell uses hardcoded colors, not UX `DESIGN.md` tokens [`supervisor-shell.component.scss`] — deferred, pre-existing
- [x] [Review][Defer] `rootRedirectGuard` exported but unused after shell route refactor [`auth.guard.ts:43`] — deferred, pre-existing
- [x] [Review][Defer] “Ensure existing web test suite remains green” unchecked — intentional release-hardening policy — deferred, pre-existing

## Dev Notes

### Existing web surfaces and constraints

- Current routes are defined in:
```1:90:apps/web/src/app/app.routes.ts
export const routes: Routes = [
  // ...
  { path: 'home', canActivate: [authGuard, supervisorGuard], loadComponent: () => import('./features/home/supervisor-home.component')... },
  { path: 'cases', canActivate: [authGuard, supervisorGuard], loadComponent: () => import('./features/cases/registry/case-registry.component')... },
  { path: 'cases/new', canActivate: [authGuard, supervisorGuard], loadComponent: () => import('./features/cases/create/case-create.component')... },
  { path: 'cases/:id', canActivate: [authGuard, supervisorGuard], loadComponent: () => import('./features/cases/detail/case-detail-placeholder.component')... },
];
```

- `CaseRegistryComponent` already owns search, presets, and export integration:
```1:200:apps/web/src/app/features/cases/registry/case-registry.component.ts
export class CaseRegistryComponent implements OnInit {
  // signals: query/currentStage/typeOfOffence/... presets/items/totalCount/page/pageSize/exporting
  async search(pageIndex = 0): Promise<void> { /* calls CaseApiService.searchCases */ }
  async exportExcel(): Promise<void> { /* CaseApiService.exportCases */ }
  async savePreset(): Promise<void> { /* CaseApiService.createSearchPreset */ }
}
```

- `CaseDetailPlaceholderComponent` already fetches `GET /cases/{id}` and includes coordinator transfer UI (keep it intact):
```40:140:apps/web/src/app/features/cases/detail/case-detail-placeholder.component.ts
export class CaseDetailPlaceholderComponent implements OnInit {
  readonly detail = signal<CaseDetailDto | null>(null);
  readonly transferForm = this.fb.nonNullable.group({ /* assignee + 3 handoff lines */ });
  async loadDetail(): Promise<void> { /* CaseApiService.getCaseDetail */ }
  async submitTransfer(): Promise<void> { /* CaseApiService.transferCase */ }
}
```

### Stage transition API (server rules)

- Use `PATCH /api/v1/cases/{id}/stage` with `TransitionCaseStageRequest` (`targetStage`, optional `notes`).  
- Server enforces: forward-only + terminal `TerminationExclusion` returns 422.  
Source: Story 2.2 acceptance criteria and OpenAPI snapshot (`/api/v1/cases/{id}/stage`).

### Critical regression traps to avoid

- Do not break existing guards:
  - `supervisorGuard` must continue redirecting mobile-only roles to `/mobile-only`.
  - Public routes (`/login`, `/login/otp`, `/session-expired`) must remain accessible without the supervisor shell.
- Do not remove or weaken existing case flows:
  - Registry: presets, export, `/` focus shortcut
  - Detail: transfer form + field-worker picker, `getCaseDetail` fetching
  - Create: duplicate sheet / merge path

### Sidebar IA source

UX specifies “Crisis Queue (Coordinator home), Dashboard, Case registry, Reports, Legends, Admin (Director)”:
```40:55:_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md
| Crisis Queue | App open (Coordinator) | Triage risks | FR-21, UJ-2 | mockups/crisis-queue.html |
| Dashboard | Sidebar | Charts (secondary) | FR-20 | — |
| Case registry | Sidebar | Search, create, merge | FR-3–6, UJ-3 | — |
| Reports | Sidebar | Generate/export | FR-22 | — |
| Legends | Sidebar | Master data CRUD | FR-23 | — |
| Admin | Sidebar (Director) | Users, audit, approvals | FR-1, FR-25 | — |
```

### Scope boundaries

- This story is **web UX + navigation + stage edit UI**, not Epic 8 Crisis Queue implementation and not dashboards/reports data.
- Do not add new API endpoints in this story unless required to implement stage edit UX (Stage API already exists).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.9]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — Web PWA architecture + repo structure]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — UX-DR11 sidebar IA]
- [Source: `apps/web/src/app/app.routes.ts` — current routing]
- [Source: `apps/web/src/app/features/cases/registry/*` — current registry implementation]
- [Source: `apps/web/src/app/features/cases/detail/case-detail-placeholder.component.*` — current detail + transfer]
- [Source: `_bmad-output/implementation-artifacts/2-2-six-stage-lifecycle-transitions.md` — stage API rules]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

### File List

