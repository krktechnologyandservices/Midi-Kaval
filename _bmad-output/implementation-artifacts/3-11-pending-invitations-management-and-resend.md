---
baseline_commit: 689f5b4
---

# Story 3.11: Pending Invitations Management & Resend

Status: done

> **TL;DR for dev — concrete actions:**
> 1. Extend `InvitationSummary` DTO to include `InvitedByUserEmail` and `InvitedByUserName` — the existing DTO omits the inviter's identity
> 2. Update `InvitationService.GetInvitationListAsync` to `.Include(i => i.InvitedByUser)` and project the inviter's email and name into the DTO
> 3. Add email notification to the original inviting Director when an invitation is resent — extend `ResendInvitationAsync` to enqueue a notification job (or send inline)
> 4. Create `InvitationResendNotificationJob` (Hangfire) that emails the original inviter: "{ResendingDirector} resent the invitation for {targetEmail}."
> 5. Add `InvitationResentNotified` audit event constant — record when the original inviter is notified
> 6. Update frontend `InvitationsComponent` to display "Invited By" column (email of the person who sent the invitation)
> 7. Add a confirmation dialog before resend — current implementation resends immediately on button click
> 8. Unit tests for resend notification logic
>
> **Brownfield reality:** The invitations data model, `InvitationService`, `InvitationsController`, `InvitationCleanupJob` (daily 2am), and the Angular `InvitationsComponent` at `/admin/invitations` all already exist from previous Epic 2/3 stories. The resend endpoint (`POST /admin/invitations/{id}/resend`) works but does NOT notify the original inviting Director — this is the primary gap. The cleanup job runs correctly but marks expired as status rather than deleting (which is the correct design — keeping audit trail). See "Existing Context" below for full details on what's already built.

## Story

As a **Director**,
I want to view pending invitations, resend them when needed, and be notified when my invitation is superseded,
so that I can manage the invitation lifecycle and coordinate with other Directors.

## Acceptance Criteria

1. **Given** the User Management Dashboard
   **When** I navigate to the Invitations section
   **Then** I see a paginated table of all sent invitations with columns: Email, Role, Status (Pending/Confirmed/Expired), Invited By, Sent Date, Expires, Actions
   **And** status badges use the established palette: Pending = amber, Confirmed = green, Expired = gray
   **And** the table is sortable by Email, Role, Status, Sent Date

2. **Given** a pending or expired invitation
   **When** I click the "Resend" button
   **Then** the system generates a new invitation link (with fresh HMAC signature and new expiry)
   **And** the previous link is invalidated (token_hash updated on the same invitation row)
   **And** a new invitation email is sent to the target recipient
   **And** the original inviting Director receives an email notification: "A new invitation was sent to {targetEmail}, replacing the one you sent on {originalDate}."
   **And** the table refreshes with the updated expiry date
   **And** a snackbar confirms the action: "New invitation sent to {email}."

3. **Given** a confirmed invitation
   **When** I click the "Resend" button
   **Then** the system returns a 409 Conflict with message "Cannot resend a confirmed invitation."
   **And** the Resend button is disabled for confirmed invitations

4. **Given** the daily cleanup job
   **When** it runs at 02:00
   **Then** all invitations with `status = 'pending'` and `expires_at_utc < NOW()` are marked as `status = 'expired'`
   **And** expired invitations show "Expired {N} days ago" in the UI
   **And** expired invitations display a gray status badge and cannot be accepted (registration endpoint rejects expired tokens)

5. **Given** an invitation is resent
   **When** the resend action completes
   **Then** an audit event is recorded with event type `invitation.resent`, including metadata: `target_email`, `role`, `original_invited_by_user_id`, `resent_by_user_id`
   **And** a separate audit event is recorded for the notification sent to the original inviter: `invitation.resend_notified`

## Tasks / Subtasks

### 1. Backend — Extend InvitationSummary DTO (AC 1)

- [x] Add `InvitedByUserEmail` (string) and `InvitedByUserName` (string, nullable) to the `InvitationSummary` record in `apps/api/Models/Admin/InvitationDtos.cs`
  ```csharp
  public sealed record InvitationSummary(
      Guid Id,
      string TargetEmail,
      string Role,
      string Status,
      DateTime CreatedAtUtc,
      DateTime ExpiresAtUtc,
      DateTime? ConfirmedAtUtc,
      string InvitedByUserEmail,
      string? InvitedByUserName
  );
  ```
- [x] Do NOT change parameter order — append new fields at the end to maintain compatibility with existing callers that use named arguments
- [x] Update `InvitationService.GetInvitationListAsync` — add `.Include(i => i.InvitedByUser)` before the `Where` clause and project `InvitedByUser.Email` and `InvitedByUser.FirstName + " " + InvitedByUser.LastName` into the DTO
- [x] Verify existing `InvitationsController.GetInvitations` endpoint returns the new fields in the JSON response (camelCase: `invitedByUserEmail`, `invitedByUserName`)

### 2. Backend — Resend notification to original inviting Director (AC 2, 5)

- [x] Add audit event constant to `AuditEventTypes.cs`:
  ```csharp
  public const string InvitationResendNotified = "invitation.resend_notified";
  ```
- [x] Create `InvitationResendNotificationJob` in `apps/api/Jobs/`:
  - Parameters: `originalInviterEmail`, `originalInviterName`, `targetEmail`, `resentByUserName`
  - Sends email via `IEmailSender` with subject: "Invitation resent for {targetEmail}"
  - Body (plain text + HTML): "Hi {originalInviterName}, a new invitation was sent to {targetEmail} by {resentByUserName}, replacing the one you sent."
  - Register in `Program.cs` with other Hangfire jobs: `builder.Services.AddScoped<InvitationResendNotificationJob>();`
  - Decorate with `[AutomaticRetry(Attempts = 3)]`
- [x] Extend `InvitationService.ResendInvitationAsync`:
  - Add `.Include(i => i.InvitedByUser)` to the invitation lookup
  - After committing the transaction (so the audit event is inside the transaction and the email is outside), enqueue the notification job:
    ```csharp
    BackgroundJob.Enqueue<InvitationResendNotificationJob>(j =>
        j.ExecuteAsync(
            invitation.InvitedByUser.Email,
            $"{invitation.InvitedByUser.FirstName} {invitation.InvitedByUser.LastName}",
            invitation.TargetEmail,
            resendByUserName, // need to resolve this
            CancellationToken.None));
    ```
  - Record the `InvitationResendNotified` audit event inside the transaction alongside the existing `InvitationResent` event
  - Resolve `resendByUserName` from the `User` entity (lookup `resendByUserId`)
  - The `ResendInvitationAsync` method signature changes to accept `resendByUserName: string` — or resolve it internally from `db.Users`

### 3. Backend — Confirmed invitation guard (AC 3)

- [x] This is already implemented in `InvitationService.ResendInvitationAsync` — it throws `InvalidOperationException("Cannot resend a confirmed invitation.")` when `invitation.Status == InvitationStatus.Confirmed`
- [x] Verify the controller returns 409 Conflict for this case — already implemented in `InvitationsController.ResendInvitation`
- [x] **No action needed** — document as verified

### 4. Backend — Cleanup job (AC 4)

- [x] This is already implemented in `InvitationCleanupJob` (runs daily at 2am, registered in `Program.cs`)
- [x] Verify the cleanup job batches expired invitations and marks them as `Expired` — working correctly
- [x] **No action needed** — document as verified
- [x] **Optional enhancement:** add logging for zero-batch case (already logs only when count > 0; consider info-level log on each run for monitoring)

### 5. Frontend — Add "Invited By" column (AC 1)

- [x] Update `InvitationSummary` interface in `admin.models.ts`:
  ```typescript
  export interface InvitationSummary {
    id: string;
    targetEmail: string;
    role: string;
    status: 'pending' | 'confirmed' | 'expired';
    createdAtUtc: string;
    expiresAtUtc: string;
    confirmedAtUtc?: string;
    invitedByUserEmail: string;
    invitedByUserName?: string;
  }
  ```
- [x] Update `ResendInvitationResponse` in `admin.models.ts` to add `invitedByUserEmail` field if needed (not strictly required since the list DTO carries it)
- [x] Add `invitedByUserEmail` column to the `InvitationsComponent` table:
  - Add column definition: `'invitedByUserEmail'` to `displayedColumns` array
  - Add `<ng-container matColumnDef="invitedByUserEmail">` with header "Invited By" showing `i.invitedByUserName ?? i.invitedByUserEmail`
  - Position after `role` column, before `status`

### 6. Frontend — Add resend confirmation dialog (AC 2)

- [x] Before calling `resend()`, open a `MatDialog` confirmation dialog:
  - Title: "Resend invitation?"
  - Content: "A new invitation will be sent to {targetEmail}. The previous invitation link will no longer work."
  - Buttons: Cancel / Send
  - Only proceed to `invitationService.resendInvitation()` on "Send"

### 7. Testing

- [x] **Unit test — `InvitationSummary` DTO includes inviter fields:** Verify `GetInvitationListAsync` returns `InvitedByUserEmail` and `InvitedByUserName`
- [x] **Unit test — Resend notification:** Verify `ResendInvitationAsync` enqueues `InvitationResendNotificationJob` with correct inviter email
- [x] **Unit test — Resend audit event:** Verify both `InvitationResent` and `InvitationResendNotified` audit events are recorded in the same transaction
- [x] **Unit test — Cleanup job:** Verify expired pending invitations are marked as expired (already exists; run to confirm still passes)
- [x] **Unit test — Confirmed resend guard:** Verify resending confirmed invitation still returns 409 (already exists; run to confirm)

## Existing Context

### Already implemented (brownfield — do NOT recreate)

| Component | File | Status |
|-----------|------|--------|
| Invitations table | EF migration `20260624152225_AddInvitations` ✅ | Done |
| Invitation entity | `Domain/Entities/Invitation.cs` ✅ | Done |
| EF config | `Infrastructure/Persistence/InvitationConfiguration.cs` ✅ | Done |
| Invitation service | `Domain/RoleManagement/InvitationService.cs` ✅ | Done |
| Invitations controller | `Controllers/V1/Admin/InvitationsController.cs` ✅ | Done |
| DTOs | `Models/Admin/InvitationDtos.cs` ✅ | Done |
| Audit event types | `InvitationSent`, `InvitationResent` in `AuditEventTypes.cs` ✅ | Done |
| Invitation email job | `Jobs/InvitationEmailDeliveryJob.cs` ✅ | Done |
| Cleanup job | `Jobs/InvitationCleanupJob.cs` (daily 2am) ✅ | Done |
| Frontend invitations page | `features/admin/pages/invitations/invitations.component.ts` ✅ | Done |
| Frontend invitation service | `features/admin/services/invitation.service.ts` ✅ | Done |
| Frontend invite dialog | `features/admin/components/invite-dialog/` ✅ | Done |
| Route | `/admin/invitations` in `app.routes.ts` ✅ | Done |
| Angular models | `InvitationSummary`, `ResendInvitationResponse` in `admin.models.ts` ✅ | Done |

### Key design notes

- **Invitation lifecycle:** `pending` → `confirmed` (on email confirmation) or `expired` (via cleanup job). Expired tokens cannot be accepted.
- **Resend mechanism:** Updates `token_hash` and `expires_at_utc` on the existing invitation row — does NOT create a new row. This preserves the audit trail.
- **Unique constraint:** Partial unique index on `(organisation_id, target_email, status)` where `status = 'pending'` prevents duplicate pending invitations.
- **Token generation:** Uses `TokenService.GenerateActivationToken()` (shared with activation tokens despite the name — do NOT refactor).
- **Cleanup job:** BATCHES expired invitations in groups of 100. Marks as `Expired` rather than deleting (data retention for audit).
- **Notification pattern:** Follow `UserStatusEmailJob` pattern — enqueue a Hangfire job for email delivery (fire-and-forget, outside the DB transaction).

### Previous story learnings (from 3-10)

- Audit events MUST be recorded inside the DB transaction (fail-closed). Email delivery goes outside the transaction (acceptable async delivery).
- Use dedicated audit event constants — do NOT repurpose existing constants with metadata flags.
- All new endpoints need `[EnableRateLimiting]`, proper `[ProducesResponseType]` attributes, and RFC 7807 `ProblemDetails` error responses.
- Frontend error classification should use HTTP status codes and `extensions['code']` from problem details — not substring matching on error messages.
- HMAC signature verification always happens before DB lookup to avoid timing side-channels.

## Architecture Compliance

### API Pattern

| Decision | Value |
|----------|-------|
| Base path | `POST /api/v1/admin/invitations/{id}/resend` — already exists |
| Auth | `[Authorize(Policy = Policies.DirectorOnly)]` + `[Require2FA]` — already in place |
| Rate limiting | `[EnableRateLimiting("data-write")]` — already in place |
| Error codes | 404 (not found), 409 (already confirmed) — already in place |
| Response envelope | `ApiResponse<T>` with `ApiMeta { requestId }` — already in place |
| Audit | Same-transaction audit events — follow existing pattern |
| Notifications | Hangfire background job (outside transaction) — follow `UserStatusEmailJob` pattern |

### Files to modify (UPDATE)

| File | What to change |
|------|----------------|
| `apps/api/Models/Admin/InvitationDtos.cs` | Add `InvitedByUserEmail` and `InvitedByUserName` to `InvitationSummary` |
| `apps/api/Domain/RoleManagement/InvitationService.cs` | Include `InvitedByUser` in list query; extend `ResendInvitationAsync` to notify original inviter |
| `apps/api/Infrastructure/Audit/AuditEventTypes.cs` | Add `InvitationResendNotified` constant |
| `apps/api/Program.cs` | Register `InvitationResendNotificationJob` as scoped |
| `apps/web/src/app/features/admin/models/admin.models.ts` | Add `invitedByUserEmail` and `invitedByUserName` to `InvitationSummary` |
| `apps/web/src/app/features/admin/pages/invitations/invitations.component.ts` | Add "Invited By" column; add resend confirmation dialog |

### Files to create (NEW)

| File | Purpose |
|------|---------|
| `apps/api/Jobs/InvitationResendNotificationJob.cs` | Hangfire job to email original inviter about resend |

### Files that need NO changes (verified)

| File | Why |
|------|-----|
| `apps/api/Controllers/V1/Admin/InvitationsController.cs` | List endpoint auto-includes new DTO fields; resend endpoint delegates to service |
| `apps/api/Jobs/InvitationCleanupJob.cs` | Already handles expired cleanup correctly (batched, marks expired) |
| `apps/api/Jobs/InvitationEmailDeliveryJob.cs` | Not affected — sends invitation emails as before |

## Library / Framework Requirements

- Hangfire `[AutomaticRetry(Attempts = 3)]` on notification job (matching existing pattern)
- `IEmailSender` via constructor injection
- No new NuGet or npm packages

## Dev Agent Record

### Implementation Plan

**Task 1 — Extend InvitationSummary DTO (AC 1)**
- Added `InvitedByUserEmail` (string, default "") and `InvitedByUserName` (string?, default null) to `InvitationSummary` record in `InvitationDtos.cs`
- Appended new fields at the end to preserve named-argument callers

**Task 2 — Update GetInvitationListAsync**
- Added `.Include(i => i.InvitedByUser)` and projected `i.InvitedByUser.Email` / `i.InvitedByUser.FirstName + " " + i.InvitedByUser.LastName` into the DTO

**Task 3 — Audit event constant**
- Added `InvitationResendNotified = "invitation.resend_notified"` to `AuditEventTypes.cs`

**Task 4 — InvitationResendNotificationJob**
- Created `Jobs/InvitationResendNotificationJob.cs` with `[AutomaticRetry(Attempts = 3)]`
- Sends email via `IEmailSender` with HTML-encoded parameters
- Registered in `Program.cs` as scoped service

**Task 5 — Extend ResendInvitationAsync**
- Added `.Include(i => i.InvitedByUser)` to invitation lookup
- Added `db.Users` lookup for `resendByUserName` resolution
- Records both `InvitationResent` and `InvitationResendNotified` audit events inside the transaction
- Enqueues `InvitationResendNotificationJob` after commit via protected virtual method
- Added `ILogger<InvitationService>` to constructor for logging
- Added `EnqueueResendNotificationJob()` protected virtual method for testability

**Task 6 — Frontend model**
- Updated `InvitationSummary` interface with `invitedByUserEmail` and `invitedByUserName` fields

**Task 7 — "Invited By" column**
- Added `invitedByUserEmail` to `displayedColumns` array
- Added column template between `role` and `status` columns
- Shows `invitedByUserName ?? invitedByUserEmail`

**Task 8 — Resend confirmation dialog**
- Created `ConfirmDialogComponent` with title, content, confirm/cancel buttons
- Updated `resend()` method to open dialog before proceeding

**Task 9 — Unit tests**
- Added `IncludesInvitedByUserFields` test for DTO inviter projection
- Added `RecordsBothAuditEventsAndNotifiesOriginalInviter` test for full resend audit/notification verification
- Updated `SeedInvitation` helper to create real User for `InvitedByUserId`
- Updated `TestableInvitationService` with `NullLogger` and notification capture
- Fixed existing `SendsSuccessfully` test to expect 2 audit events

### Key Decisions
- Used `NullLogger<InvitationService>.Instance` in tests via `Microsoft.Extensions.Logging.Abstractions`
- Resolved `resendByUserName` internally from `db.Users` rather than changing method signature
- HTML-encoded email body parameters (matching `UserStatusEmailJob` pattern)
- Added protected virtual `EnqueueResendNotificationJob` for testability (matching `EnqueueEmailJob` pattern)

### Completion Notes
- ✅ All 181 unit tests pass (0 failures, 0 skipped)
- ✅ Backend compiles with 0 errors
- ✅ All 5 acceptance criteria satisfied
- ✅ Audit events recorded inside transaction (fail-closed)
- ✅ Notification sent outside transaction (fire-and-forget)

## File List

### Modified
- `apps/api/Models/Admin/InvitationDtos.cs` — Added `InvitedByUserEmail` and `InvitedByUserName` to `InvitationSummary`
- `apps/api/Domain/RoleManagement/InvitationService.cs` — Extended `GetInvitationListAsync` and `ResendInvitationAsync` with inviter fields, notification, audit
- `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — Added `InvitationResendNotified` constant
- `apps/api/Program.cs` — Registered `InvitationResendNotificationJob`
- `apps/web/src/app/features/admin/models/admin.models.ts` — Added `invitedByUserEmail` and `invitedByUserName`
- `apps/web/src/app/features/admin/pages/invitations/invitations.component.ts` — Added column + resend dialog
- `tests/api.unit/Domain/RoleManagement/InvitationServiceTests.cs` — Added tests for DTO fields, resend notification, audit events

### Created
- `apps/api/Jobs/InvitationResendNotificationJob.cs` — Hangfire job to email original inviter
- `apps/web/src/app/features/admin/components/confirm-dialog/confirm-dialog.component.ts` — Reusable confirmation dialog

## Change Log

| Date | Change |
|------|--------|
| 2026-06-28 | Created comprehensive story file for 3-11 pending invitations management and resend |
| 2026-06-28 | Implemented back-end DTO extension, audit event, notification job, and service changes |
| 2026-06-28 | Implemented front-end "Invited By" column and resend confirmation dialog |
| 2026-06-28 | Added unit tests; all 181 tests passing |
| 2026-06-28 | Status updated to "review" |
| 2026-06-28 | **Code Review Fixes** — 11 patches applied: moved post-commit logic outside try-catch; added null guard on InvitedByUser in resend path and list projection; renamed audit constant to `InvitationResentNotified`; fixed SQL null-propagation in LINQ Select; made `InvitedByUserEmail` nullable for consistent defaults; removed side-effect user creation from test helper; added `resentByUserName` assertions in tests; added `?? false` to dialog handling; moved `EnqueueEmailJob` after transaction commit; show Resend button as disabled (not hidden) for confirmed invitations |
| 2026-06-28 | Round 2 code review complete. Status updated to "done" |
