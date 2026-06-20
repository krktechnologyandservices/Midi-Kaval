---
baseline_commit: NO_VCS
---

# Story 7.4: In-App Notification Bell (Web and Mobile)

Status: done

## Story

As a **user**,
I want a notification centre with read state,
so that I review alerts in one place (FR-19, UX-DR13).

*Scope: **Angular PWA notification bell** in supervisor shell header with **read/unread list page**, **unread count badge**, and **tap-through deep links** to case and claim; **React Native More tab bell badge** and **push notification tap handling** integration. **API**: add `GET /api/v1/notifications/unread-count` endpoint. **No** push delivery changes (Stories **7.2**, **7.3** handle dispatch); **no** email template work; **no** preferences PATCH persistence; **no** SMS/WhatsApp; **no** SignalR/WebSocket real-time (polling-based badge refresh).*

## Acceptance Criteria

1. **Given** the API is running
   **When** a user has unread notifications
   **Then** `GET /api/v1/notifications/unread-count` returns `{ "count": N }` scoped to the authenticated user's organisation
   **And** `GET /api/v1/notifications` lists notifications ordered by `CreatedAtUtc DESC` (existing — reused)
   **And** `PATCH /api/v1/notifications/{id}/read` marks a single notification as read and returns the updated DTO (existing — reused)

2. **Given** a logged-in supervisor (Coordinator/Director) on the web
   **When** the shell renders
   **Then** a **bell icon** with unread count badge appears in the top header area (next to nav items or user info)
   **And** bell icon uses Angular Material `mat-badge` or custom badge overlay
   **And** clicking the bell navigates to the `/notifications` route (list page)
   **And** unread count refreshes on page navigation and on a 60-second timer (no WebSocket)
   **And** the count does not appear for non-supervisor roles (excluded by existing `supervisorGuard`)

3. **Given** the web `/notifications` list page
   **When** the page loads
   **Then** notifications display in a list/table sorted newest-first
   **And** each row shows: title, body preview, relative timestamp (e.g., "5m ago"), and read/unread indicator (bold + coloured left border for unread)
   **And** tapping/clicking an unread notification marks it as read (calls `PATCH /.../read`) and navigates to the linked resource:
     - `resourceType == "CourtSitting"` → navigates to `/cases/{caseId}` (case detail)
     - `resourceType == "TravelClaim"` → navigates to `/admin/travel-claims/{resourceId}` (Director) or annotated claim page
     - `resourceType == "Intervention"` → navigates to `/cases/{caseId}` (case detail, interventions tab)
     - fallback → navigates to `/cases/{caseId}` if `caseId` is present, else stays on notifications page
   **And** tapping an already-read notification navigates without calling mark-read again
   **And** loading state shows a spinner
   **And** error state shows error message with "Retry" button
   **And** empty state shows text: "You're up to date."

4. **Given** logged-in field worker (SocialWorker/CaseWorker) on mobile
   **When** the More tab is active
   **Then** a bell badge icon appears for the "Notifications" row or tab with unread count
   **And** tapping Notifications opens the existing `NotificationsListScreen` (already built in Story 7.2)
   **And** unread count refreshes when the screen is focused (`useFocusEffect`)
   **And** tap-to-navigate uses existing `handleNotificationNavigation` (court → case detail, travel claim → claim form with `mode: 'view'`)

5. **Given** push notifications arrive on mobile (Story **7.2**)
   **When** the user taps the push notification from outside the app
   **Then** existing `pushNotificationHandlers.ts` navigates to the correct screen based on event type (already built — reuse, verify integration)
   **And** in-app notification is marked as read via `PATCH /.../read` when tapped from push (aligns with existing `handleNotificationPress`)

6. **Given** an authenticated session
   **When** the user logs out or the session expires
   **Then** badge count and notification list clear on logout (mirrors existing `auth-session.service` clear pattern)

7. **Given** regression safety
   **When** story ships
   **Then** existing notification unit tests pass unchanged
   **And** API integration tests cover: unread count returns correct value, mark-read changes count, list returns user-scoped results
   **And** existing `NotificationsAndOverdueJobTests`, `TravelClaimNotificationApiTests`, `CourtReminderJobTests` still pass
   **And** web component tests cover: badge render with count, notification list render, empty state, error state
   **And** mobile existing screens + navigation unchanged

## Tasks / Subtasks

- [ ] **API — unread count endpoint** (AC: 1)
  - [ ] Add `GetUnreadCountAsync()` to `NotificationService` — `await db.InAppNotifications.CountAsync(n => n.OrganisationId == orgId && n.UserId == userId && n.ReadAtUtc == null)`
  - [ ] Add `GET /api/v1/notifications/unread-count` to `NotificationsController` — returns `{ "count": int }`
  - [ ] Integration test for unread count

- [ ] **Web — notification API and deeplink services** (AC: 2, 3)
  - [ ] Add `UnreadCountDto = components['schemas']['UnreadCountDto']` type to `apps/web/src/app/features/cases/models/case.models.ts`
  - [ ] Add `getUnreadCount()` method to existing `CaseApiService` — pattern: `inject(HttpClient)` + `AuthSessionService` + `${environment.apiBaseUrl}/api/v1/notifications/unread-count`
  - [ ] Create `apps/web/src/app/features/notifications/notification-deeplink.service.ts` — `navigate(notification: NotificationDto)` routing logic per AC3 deep-link mapping

- [ ] **Web — notification bell component** (AC: 2)
  - [ ] Create `src/app/features/notifications/notification-bell/notification-bell.component.ts` — standalone component with Angular Material `mat-icon` (notifications icon) + badge overlay
  - [ ] Poll unread count on init, on navigation end, and on 60s interval
  - [ ] Click navigates to `/notifications` route
  - [ ] Import in `supervisor-shell.component.ts` (header area, e.g., after nav items)
  - [ ] Add `MatIconModule` and `MatBadgeModule` to `supervisor-shell.component.ts` `imports` array (needed for bell icon + badge overlay)

- [ ] **Web — notification list page** (AC: 3)
  - [ ] Create `src/app/features/notifications/notification-list/notification-list.component.ts` — standalone component
  - [ ] Fetch notifications on init, pull-to-refresh (or refresh button)
  - [ ] Render rows with: title (bold if unread), body preview, relative timestamp, read/unread left border accent
  - [ ] Tap handler: markRead + navigate via `notification-deeplink.service`
  - [ ] Loading / error / empty ("You're up to date.") states
  - [ ] Register route `/notifications` in `app.routes.ts` (inside supervisor shell children, before `cases` wildcard)

- [ ] **Mobile — notification badge on More tab** (AC: 4)
  - [ ] Add `unreadCount` state and polling to `MoreScreen` or `MainTabNavigator`
  - [ ] Display badge count next to "Notifications" row or as tab icon badge
  - [ ] Refresh count on `useFocusEffect`
  - [ ] Existing `NotificationsListScreen` already handles list, mark-read, and navigation — no changes needed to the screen itself

- [ ] **README update**
  - [ ] Document in-app notification endpoints
  - [ ] Document bell badge polling behaviour

## Dev Notes

### READ FIRST — do not reinvent

1. **API notifications endpoints EXIST** — `NotificationsController` already has `GET /notifications`, `PATCH /{id}/read`, and `GET /preferences`. Only add `GET /unread-count`.

2. **Mobile NotificationsListScreen EXIST** — fully built with pull-to-refresh, read/unread styling, empty state, `handleNotificationNavigation` for court sitting → case detail and travel claim → claim form. Do **not** rebuild.

3. **Mobile NotificationApiService EXIST** — `list()` and `markRead(id)` methods. Add `getUnreadCount()`.

4. **Web notification types and API methods ALREADY EXIST** — `NotificationDto`, `NotificationListResultDto` are defined in `case.models.ts` (via `@midi-kaval/api-client`). `listNotifications()` and `markNotificationRead()` exist in `CaseApiService`. Add `UnreadCountDto` type and `getUnreadCount()` method there — **do not** create a separate notification API service.

5. **Web supervisor shell is Angular standalone** — uses `RouterOutlet`, `RouterLink`, `RouterLinkActive` for nav. Notification bell component must be standalone and imported into the shell component's `imports` array alongside `MatIconModule` and `MatBadgeModule`.

5. **Web uses Angular Material** — use `MatIconModule`, `MatBadgeModule` for bell icon and badge. Existing theme primary is `#0D6E6E`.

6. **Web API call pattern** — use `inject(HttpClient)` + `AuthSessionService` + `${environment.apiBaseUrl}`. See `case-api.service.ts` for the established pattern.

7. **Deep link navigation on web:**
   - `resourceType === "CourtSitting"` → `router.navigate(['/cases', caseId])`
   - `resourceType === "TravelClaim"` → `router.navigate(['/admin/travel-claims', resourceId])`
   - `resourceType === "Intervention"` → `router.navigate(['/cases', caseId])`
   - Fallback: if `caseId` is present → `router.navigate(['/cases', caseId])`; else stay put

8. **No real-time push** — badge count refreshes via 60s `setInterval` + on navigation end (`Router.events` filter). This matches pilot v1 polling approach.

9. **Badge clears on logout** — `AuthSessionService.clear()` already clears session state; the notification bell component should reset count on destroy or on auth state change.

10. **Existing tests must pass** — `NotificationsAndOverdueJobTests`, `TravelClaimNotificationApiTests`, `CourtReminderJobTests` assert existing notification list/mark-read/unread behavior. New `GET /unread-count` must not break them.

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `NotificationsController.cs` | `GET /notifications`, `PATCH /{id}/read`, `GET /preferences` | Add `GET /notifications/unread-count` |
| `NotificationService.cs` | `ListForCurrentUserAsync`, `MarkReadAsync`, notification helpers | Add `GetUnreadCountAsync` |
| `Apps.Web` (none exist) | — | New: notification models, API service, deeplink service, bell component, list page + route |
| `MainTabNavigator.tsx` | Today, Cases, More tabs | Optional: add badge to More tab tabBarBadge |
| `MoreScreen.tsx` | List of links (Notifications, Sync, Travel, etc.) | Add unread count badge next to Notifications row |
| `NotificationApiService.ts` (mobile) | `list()`, `markRead()` | Add `getUnreadCount()` |
| `notificationNavigation.ts` (mobile) | Court → case, travel claim → claim form | **No change** unless new event types need handling |
| `pushNotificationHandlers.ts` (mobile) | FCM tap → navigate | **No change** — verify existing flows |
| `SupervisorShellComponent` | Nav items, user info | Import notification bell component in header |
| `AppRoutes` | All existing routes | Add `/notifications` child route |
| `AuthSessionService` (Angular) | Auth state, token management | **No change** — bell component hooks into existing clear pattern |

### File structure

| Action | Path |
|--------|------|
| UPDATE | `apps/api/Controllers/V1/NotificationsController.cs` |
| UPDATE | `apps/api/Infrastructure/Notifications/NotificationService.cs` |
| NEW | `apps/api/Models/Notifications/UnreadCountDto.cs` |
| UPDATE | `apps/web/src/app/features/cases/models/case.models.ts` (add UnreadCountDto type) |
| UPDATE | `apps/web/src/app/features/cases/services/case-api.service.ts` (add getUnreadCount method) |
| NEW | `apps/web/src/app/features/notifications/notification-deeplink.service.ts` |
| NEW | `apps/web/src/app/features/notifications/notification-bell/notification-bell.component.ts` |
| NEW | `apps/web/src/app/features/notifications/notification-list/notification-list.component.ts` |
| UPDATE | `apps/web/src/app/features/shell/supervisor-shell.component.ts` |
| UPDATE | `apps/web/src/app/features/shell/supervisor-shell.component.html` |
| UPDATE | `apps/web/src/app/app.routes.ts` |
| UPDATE | `apps/mobile/src/services/notifications/NotificationApiService.ts` |
| UPDATE | `apps/mobile/src/screens/more/MoreScreen.tsx` (badge count) |

### Testing requirements

**Integration (`tests/api.integration/`):**
- `NotificationsControllerTests` or extend existing `NotificationsAndOverdueJobTests` — assert `GET /unread-count` returns correct count, count decreases after mark-read, count is user-scoped

**Web (Jasmine + Angular Testing Library):**
- `NotificationBellComponent` — renders bell icon, shows badge with count, hides badge at zero
- `NotificationListComponent` — renders rows, loading state, error state, empty state, tap calls markRead + navigates

**Mobile (Jest + RN Testing Library):**
- `NotificationApiService.getUnreadCount` — returns parsed response
- `MoreScreen` — shows badge count (integration with existing screen)

### Previous story intelligence (7.3)

- EmailDeliveryService uses scoped DI registration pattern — notifications follow same pattern.
- FakeEmailSender pattern — no equivalent needed for bell (it's read-only API access).
- `NotificationService.cs` already handles `ListForCurrentUserAsync` and `MarkReadAsync` — these are the APIs the bell UI consumes.

### Previous story intelligence (7.2)

- `PushDeliveryService.TrySendAsync` sends FCM push after in-app notification creation.
- Create*ForSave helpers return the persisted `InAppNotification` entity.
- Mobile `NotificationsListScreen` already renders the list with read/unread, empty state, and tap-to-navigate via `handleNotificationNavigation`.
- Mobile `handleNotificationPress` marks read before navigating.
- No changes needed to push delivery or notification creation.

### Previous story intelligence (7.1)

- `InAppNotification` entity schema: `Id`, `OrganisationId`, `UserId`, `EventType`, `Title`, `Body`, `CaseId`, `ResourceType`, `ResourceId`, `ReadAtUtc`, `CreatedAtUtc`.
- `GET /api/v1/notifications` returns notifications ordered by `CreatedAtUtc DESC` for the authenticated user.
- `PATCH /api/v1/notifications/{id}/read` sets `ReadAtUtc` and returns the updated DTO.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 7.4, FR-19, UX-DR13]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §5.3 `GET /notifications`, `PATCH /notifications/{id}/read`; §8 FR→Bell mapping]
- [Source: `_bmad-output/project-context.md` — Angular standalone components + signals; PWA patterns]
- [Source: `apps/api/Controllers/V1/NotificationsController.cs` — existing endpoints]
- [Source: `apps/api/Infrastructure/Notifications/NotificationService.cs` — existing service with List/MarkRead]
- [Source: `apps/api/Domain/Entities/InAppNotification.cs` — entity schema]
- [Source: `apps/mobile/src/screens/notifications/NotificationsListScreen.tsx` — existing mobile implementation (pattern reference)]
- [Source: `apps/mobile/src/services/notifications/NotificationApiService.ts` — existing mobile API service]
- [Source: `apps/web/src/app/features/shell/supervisor-shell.component.ts` — shell component to modify]
- [Source: `apps/web/src/app/app.routes.ts` — route configuration]

## Dev Agent Record

### Agent Model Used

Composer

### Completion Notes List

- Story created from Epic 7 specifications and current codebase analysis.
- Existing mobile NotificationsListScreen + NotificationApiService fully built.
- Web side needs: notification API service, bell component with badge, notification list page, deep-link navigation.
- API needs: unread-count endpoint.
- Mobile needs: unread badge on More tab.

### File List

- apps/api/Controllers/V1/NotificationsController.cs (modified)
- apps/api/Infrastructure/Notifications/NotificationService.cs (modified)
- apps/api/Models/Notifications/UnreadCountDto.cs (new)
- apps/web/src/app/features/cases/models/case.models.ts (modified)
- apps/web/src/app/features/cases/services/case-api.service.ts (modified)
- apps/web/src/app/features/notifications/notification-deeplink.service.ts (new)
- apps/web/src/app/features/notifications/notification-bell/notification-bell.component.ts (new)
- apps/web/src/app/features/notifications/notification-list/notification-list.component.ts (new)
- apps/web/src/app/features/shell/supervisor-shell.component.ts (modified)
- apps/web/src/app/app.routes.ts (modified)
- apps/mobile/src/services/notifications/NotificationApiService.ts (modified)
- apps/mobile/src/screens/more/MoreScreen.tsx (modified)

## Change Log

- 2026-06-20: Story created — notification bell web + mobile, API unread count endpoint.

## Story Completion Status

Ultimate context engine analysis completed — comprehensive developer guide created.

### Review Findings

- [x] [Review][Defer] Wrong module placement — UnreadCountDto/getUnreadCount in cases module. Pre-existing pattern: NotificationDto and NotificationListResultDto are also in case.models.ts. Not actionable in this story.
- [x] [Review][Defer] No pagination on notification list — all notifications fetched at once. Out of scope; existing notification list has no pagination DTO. Future story consideration.
- [x] [Review][Defer] Mobile 60s poll battery drain — accepted trade-off per "no real-time" design constraint.
- [x] [Review][Defer] Mobile hand-rolled type definitions drift hazard — pre-existing project-wide pattern (all mobile types are hand-rolled). Not specific to this change.
- [x] [Review][Defer] No caching/rate-limiting on unread-count endpoint — pre-existing; none of the notification endpoints use caching.

- [x] [Review][Patch] Bell subscribe missing error handler — HTTP error kills the entire observable chain; badge freezes permanently [notification-bell.component.ts:63-65]
- [x] [Review][Patch] Deeplink silent fallthrough — unknown resource types produce no feedback [notification-deeplink.service.ts:9-32]
- [x] [Review][Patch] Mobile badge renders "null"/"undefined" when unreadCount is null/undefined [MoreScreen.tsx:31]
- [x] [Review][Patch] Missing 403 Forbidden in API endpoint contract [NotificationsController.cs:26-34]
- [x] [Review][Patch] Missing error/retry state in notification list page (AC3 violation) [notification-list-page.component.ts:144-156]
- [x] [Review][Patch] Mobile screen-focus refresh incomplete — tab switches within app don't trigger refresh (AC4) [UnreadCountContext.tsx]
- [x] [Review][Patch] Badge does not clear on logout (AC6 violation) [notification-bell.component.ts, UnreadCountContext.tsx] — handled by component lifecycle (destroy on logout, recreate with initial 0)
