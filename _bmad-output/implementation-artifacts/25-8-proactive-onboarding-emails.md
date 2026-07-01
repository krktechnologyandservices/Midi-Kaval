# Story 25.8: Proactive Onboarding Emails — Welcome Email with 2FA Setup Link, OTP Footer, Invite Checkbox, Legacy Migration Job

**Epic:** Epic 25 — 2FA Universal Enrollment & Administration

Status: ready-for-dev

## Story

As a **Director**,
I want **new users to receive proactive 2FA setup guidance in their welcome and invitation emails, have a 2FA enrollment link in the OTP message footer, and existing unenrolled users to be migrated via a batched email campaign**,
so that **all users are aware of 2FA requirements from day one and can take action without manual intervention from the Director.**

## Acceptance Criteria

1. **Welcome email on account creation (FR-3.1)** — When a new user confirms their email (via `RegistrationService.ExecuteConfirmEmailAsync`), the existing confirmation email template is extended to include:
   - A sentence after the welcome text: "After logging in, set up two-factor authentication (2FA) to add an extra layer of security to your account."
   - If the user's organisation has `require_2fa = true`, add an additional sentence: "Your organisation requires two-factor authentication. You will be prompted to set it up on your next login."
   - The setup URL is role-aware: Vendors get `/vendor/settings`, all other roles get `/settings/2fa`
   - No new email template classes — extend the existing `ConfirmationEmailTemplate.cs` to accept an optional `include2faInfo` parameter and `organisationName` context

2. **2FA enrollment link in email OTP footer (FR-3.2)** — The OTP email sent during login (in `AuthService.LoginAsync`) is extended:
   - Add a footer paragraph to the email body: "Did you know? Two-factor authentication provides an extra layer of security to protect your account. Set it up in your account settings."
   - Only shown when the user has `totp_enrolled_at IS NULL` (not already enrolled)
   - Only shown when the organisation has `require_2fa = true` (org mandate is on) — to avoid spamming users in orgs that haven't mandated 2FA
   - The user's enrollment status and org's `require_2fa` flag are already available during login context (user entity loaded in `LoginAsync`)
   - Append the text to the existing email body string — do NOT create a new email template class

3. **Invitation dialog checkbox for 2FA instructions (FR-3.3)** — The `InviteDialogComponent` at `features/admin/components/invite-dialog/invite-dialog.component.ts` gains:
   - A `MatCheckbox` in the confirmation step (second screen): "Include 2FA setup instructions" (default: checked)
   - The checkbox state is passed to the API as part of the `SendInvitationRequest` DTO (add optional `bool? Include2faInstructions` field)
   - The backend `InvitationService.SendInvitationAsync` passes this flag to the `InvitationEmailDeliveryJob`
   - When `include2faInstructions` is true, the invitation email body (in `InvitationEmailDeliveryJob.ExecuteAsync`) gains an additional paragraph: "After logging in, set up two-factor authentication (2FA) to add an extra layer of security. Find this option in your account settings."
   - When the organisation has `require_2fa = true`, the 2FA paragraph is always included regardless of the checkbox (mandate overrides the checkbox)
   - The checkbox is added to the confirmation card section, below the existing text

4. **Legacy migration Hangfire job (FR-3.4)** — New `Legacy2faMigrationJob` at `apps/api/Jobs/Legacy2faMigrationJob.cs`:
   - Scans all users where `totp_secret IS NULL AND is_active = true` (unenrolled active users)
   - Sends a migration email: subject "Action Required: Set Up Two-Factor Authentication", body includes org name, first name, setup URL based on role (Vendor → `/vendor/settings`, other → `/settings/2fa`), and note: "Your organisation is adopting two-factor authentication to improve security."
   - Throttled: processes max 100 users per hour. Tracks progress using a Redis key `legacy_2fa_migration_cursor` storing the last processed user `CreatedAtUtc` as ISO string. On each run, loads the cursor, processes up to 100 users ordered by `CreatedAtUtc`, sends emails, updates cursor. If fewer than 100 users remain (cursor catches up to the newest unenrolled user), the job logs completion.
   - Uses `IConnectionMultiplexer` for Redis cursor storage (already registered in DI)
   - Each email sends individually via `IEmailSender.SendAsync` — no batching
   - Registered as a recurring Hangfire job (hourly cron). The cursor-based idempotency ensures no duplicate sends — the cursor tracks which users have been processed, so the same user is never emailed twice even if the job fires multiple times.
   - Records audit event `2fa_migration_email_sent` for each email sent (add `TwoFactorMigrationEmailSent = "2fa_migration_email_sent"` to `AuditEventTypes`)
   - On completion (all eligible users processed): logs info message "Legacy2faMigrationJob completed. Processed {count} users."
   - Skip users who are not active (`is_active = false` or `is_suspended = true`)
   - Logs warning for failed email sends but continues processing

5. **New audit event type**: `TwoFactorMigrationEmailSent = "2fa_migration_email_sent"` added to `AuditEventTypes.cs`

6. **New `SendInvitationRequest` field**: Add `bool Include2faInstructions = false` as a trailing optional positional parameter to the C# `SendInvitationRequest` record at `Models/Admin/InvitationDtos.cs`. Since it's a positional record, use a default value: `bool Include2faInstructions = false`. In Angular (`features/admin/models/admin.models.ts`), add `include2faInstructions?: boolean` alongside the existing `message?: string`.

## Tasks / Subtasks

### API Changes

- [ ] Extend `ConfirmationEmailTemplate.cs` to accept 2FA context (AC: 1)
  - [ ] Add optional parameter `bool include2faInfo` and `bool orgRequires2fa` to render context
  - [ ] Role-aware setup URL logic (Vendor → `/vendor/settings`, other → `/settings/2fa`)
  - [ ] Only render 2FA paragraph when `include2faInfo` is true
  - [ ] Add "Your organisation requires two-factor authentication." sentence when `orgRequires2fa` is true
- [ ] Modify confirmation email sending path to pass 2FA info (AC: 1)
  - [ ] Update `ConfirmationEmailDeliveryJob.ExecuteAsync` to pass `Include2faInfo: true` and `OrgRequires2fa: token.Invitation?.Organisation?.Require2fa ?? false` to the `ConfirmationEmailContext` constructor. The job already loads `token.Invitation.Organisation` via `.Include(t => t.Invitation).ThenInclude(i => i!.Organisation)`.
  - [ ] Load `Organisation.Require2fa` and pass to template
- [ ] Extend OTP email footer in `AuthService.LoginAsync` (AC: 2)
  - [ ] Check `user.TotpEnrolledAt is null` (not enrolled)
  - [ ] Check `user.Organisation.Require2fa` (org mandate is on)
  - [ ] Append 2FA setup hint to email body string
- [ ] Add `Include2faInstructions` to `SendInvitationRequest` DTO in C# (AC: 3)
- [ ] Modify `InvitationService.SendInvitationAsync` to pass 2FA flag to job (AC: 3)
- [ ] Modify `InvitationEmailDeliveryJob.ExecuteAsync` to render 2FA paragraph when applicable (AC: 3)
  - [ ] Add `bool include2faInfo` and `bool orgRequires2fa` parameters to `ExecuteAsync` method signature
  - [ ] Load org's `require_2fa` flag from invitation's organisation
  - [ ] If `orgRequires2fa` is true, always include 2FA paragraph regardless of checkbox
- [ ] Modify `InvitationService.ResendInvitationAsync` to pass `include2faInfo: true` to `EnqueueEmailJob` (AC: 3, resend path)
- [ ] Create `Legacy2faMigrationJob` (AC: 4)
  - [ ] Scan users: `totp_secret IS NULL AND is_active = true AND is_suspended = false`
  - [ ] Redis cursor at key `legacy_2fa_migration_cursor` (ISO date string of last processed `CreatedAtUtc`)
  - [ ] Process up to 100 per run, ordered by `CreatedAtUtc`
  - [ ] Send email via `IEmailSender`, log audit event `2fa_migration_email_sent`
  - [ ] Update cursor after each batch
  - [ ] Error handling: log warning, continue processing
  - [ ] Registration in `Program.cs` — add `builder.Services.AddScoped<Legacy2faMigrationJob>()` alongside other jobs and `RecurringJob.AddOrUpdate<Legacy2faMigrationJob>` with hourly cron
- [ ] Add `TwoFactorMigrationEmailSent` to `AuditEventTypes.cs` (AC: 5)

### Angular Changes

- [ ] Add `Include2faInstructions` to `SendInvitationRequest` in `admin.models.ts` (AC: 3)
- [ ] Extend `InviteDialogComponent` with MatCheckbox in confirmation step (AC: 3)
  - [ ] Import `MatCheckboxModule`
  - [ ] Add checkbox below confirmation text: "Include 2FA setup instructions" (default: checked)
  - [ ] Pass checkbox value as `include2faInstructions` in the request
- [ ] Run `ng build` and `dotnet build` to verify compilation

## Dev Notes

### Prerequisites (Stories 25-1 through 25-7 — assumed complete)

- `Organisation.Require2fa` property exists on entity — from Story 25-1
- `User.TotpSecret` / `User.TotpEnrolledAt` properties exist — existing
- `IEmailSender` with both `SmtpEmailSender` and `FakeEmailSender` — existing
- Hangfire already configured with Redis storage — existing (see `Program.cs` lines 174-250)
- `InvitationService` with `SendInvitationAsync` — existing
- `InvitationEmailDeliveryJob` — existing job with HTML email body
- `ConfirmationEmailTemplate` — existing template at `Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs`
- `RegistrationService` — existing with `ExecuteConfirmEmailAsync`
- `AuditEventTypes` — existing with all 2fa_ prefixed events from Story 25-2
- `InviteDialogComponent` — existing at `features/admin/components/invite-dialog/`
- `IConnectionMultiplexer` — registered in DI, used by `OtpChallengeStore`

### Architecture Compliance

**This story implements:**
- FR-3.1 (Welcome email with 2FA setup link) — AC 1
- FR-3.2 (OTP footer with 2FA link) — AC 2
- FR-3.3 (Invitation checkbox) — AC 3
- FR-3.4 (Legacy migration campaign) — AC 4
- NFR-1.2 (SHA-256 hashing for bypass codes) — N/A
- NFR-3.4 (Legacy migration throttled 100/hr) — AC 4

**Does NOT implement (deferred):**
- FR-2.8 (Enrollment notifications to Director) — deferred to future story

### Source Tree Components to Touch

**New files:**
```
apps/api/Jobs/Legacy2faMigrationJob.cs
```

**Modified files:**
```
apps/api/Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs    # MODIFY: +2FA info paragraph
apps/api/Infrastructure/Auth/AuthService.cs                             # MODIFY: +OTP footer text
apps/api/Domain/RoleManagement/InvitationService.cs                     # MODIFY: +pass 2FA flag to job
apps/api/Jobs/InvitationEmailDeliveryJob.cs                             # MODIFY: +2FA paragraph
apps/api/Models/Admin/InvitationDtos.cs                                    # MODIFY: +Include2faInstructions
apps/api/Infrastructure/Audit/AuditEventTypes.cs                        # MODIFY: +TwoFactorMigrationEmailSent
apps/api/Program.cs                                                     # MODIFY: register Legacy2faMigrationJob
apps/web/src/app/features/admin/models/admin.models.ts                  # MODIFY: +Include2faInstructions
apps/web/src/app/features/admin/components/invite-dialog/invite-dialog.component.ts  # MODIFY: +checkbox
```

### Existing Patterns & Conventions (MUST FOLLOW)

**ConfirmationEmailTemplate pattern** (from existing file at `Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs`):

The **actual** existing template uses **plain text** (not HTML). Fields in the current record are in this exact order:

```csharp
public sealed record ConfirmationEmailContext(
    string UserName,
    string OrganisationName,
    string Role,
    string ConfirmationUrl,
    int TokenTtlHours,
    string? DirectorName
);
```

Extended record — new fields appended at the end with default values to preserve backward compatibility:

```csharp
public sealed record ConfirmationEmailContext(
    string UserName,
    string OrganisationName,
    string Role,
    string ConfirmationUrl,
    int TokenTtlHours,
    string? DirectorName,
    bool Include2faInfo = false,    // NEW
    bool OrgRequires2fa = false     // NEW
);
```

Extended template — 2FA paragraphs use plain text to match existing style:

```csharp
public static string RenderBody(ConfirmationEmailContext context)
{
    var welcome = $"Welcome, {HttpUtility.HtmlEncode(context.UserName)}!";
    var invitationLine = context.DirectorName is not null
        ? $"You've been invited by {HttpUtility.HtmlEncode(context.DirectorName)} to join {HttpUtility.HtmlEncode(context.OrganisationName)} as a {HttpUtility.HtmlEncode(context.Role)}."
        : $"You've been invited to join {HttpUtility.HtmlEncode(context.OrganisationName)} as a {HttpUtility.HtmlEncode(context.Role)}.";

    var body = $"""
        {welcome}

        {invitationLine}

        Click the link below to confirm your account:

        {context.ConfirmationUrl}

        This link expires in {context.TokenTtlHours} hours.

        If you did not expect this invitation, please ignore this email.
        """;

    if (context.Include2faInfo)
    {
        var setupUrl = context.Role == "Vendor" ? "/vendor/settings" : "/settings/2fa";
        body += $"""


        After logging in, set up two-factor authentication (2FA) to add an extra layer of security to your account. You can set this up at {setupUrl}.
        """;
        if (context.OrgRequires2fa)
        {
            body += """

            Your organisation requires two-factor authentication. You will be prompted to set it up on your next login.
            """;
        }
    }

    return body;
}
```

**OTP email footer pattern** (in `AuthService.LoginAsync`, around line 136-141):
```csharp
var subject = "Your Kaval Online verification code";
var body = $"Your verification code is: {otpCode}\n\nEnter the 6-digit code from your email.";

// FR-3.2: Append 2FA enrollment hint for unenrolled users in mandated orgs
if (user.TotpEnrolledAt is null && user.Organisation.Require2fa)
{
    body += "\n\nDid you know? Two-factor authentication provides an extra layer of security to protect your account. Set it up in your account settings.";
}
```
Since `user` is loaded with `AsNoTracking()`, you may need to explicitly load the organisation reference:
```csharp
var orgRequires2fa = await db.Organisations
    .Where(o => o.Id == user.OrganisationId)
    .Select(o => o.Require2fa)
    .FirstOrDefaultAsync(cancellationToken);
```

**InvitationService pattern** — pass the flag through the existing `EnqueueEmailJob` virtual method:
```csharp
protected virtual void EnqueueEmailJob(Guid invitationId, string rawToken, string signature, string targetEmail, string role)
{
    Hangfire.BackgroundJob.Enqueue<InvitationEmailDeliveryJob>(
        j => j.ExecuteAsync(invitationId, rawToken, signature, targetEmail, role, CancellationToken.None));
}
```
Add the new parameter to the Hangfire job's `ExecuteAsync` signature. Since this changes the method signature, all enqueue calls must be updated to pass the new parameter.

**InvitationEmailDeliveryJob pattern** — extend the email body. The condition must check both the checkbox AND the org mandate override:
```csharp
var body = $"""
    <p>You have been invited to join <strong>{orgName}</strong> as a <strong>{role}</strong>.</p>
    <p>Click the link below to accept your invitation:</p>
    <p><a href="{invitationUrl}">{invitationUrl}</a></p>
    <p>This link expires in 24 hours.</p>
    """;

// orgRequires2fa is loaded from invitation.Organisation.Require2fa
if (include2faInfo || orgRequires2fa)
{
    var setupUrl = role == "Vendor" ? "/vendor/settings" : "/settings/2fa";
    body += $"""
        <p>After logging in, set up two-factor authentication (2FA) to add an extra layer of security. Find this option in your account settings.</p>
        """;
}
```
Note: The job currently uses `AutomaticRetry(Attempts = 3)`. The new parameter must be added to both the `ExecuteAsync` method signature and the Hangfire job enqueue call in `InvitationService`.

**Legacy2faMigrationJob pattern**:
```csharp
using System.Globalization;  // Required for DateTime.Parse with invariant culture
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using StackExchange.Redis;

[AutomaticRetry(Attempts = 0)] // No automatic retry — cursor-based idempotency
public sealed class Legacy2faMigrationJob(
    AppDbContext db,
    IEmailSender emailSender,
    IAuditService auditService,
    IConnectionMultiplexer redis,
    ILogger<Legacy2faMigrationJob> logger)
{
    private const int BatchSize = 100;
    private const string CursorKey = "legacy_2fa_migration_cursor";

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var redisDb = redis.GetDatabase();

        // Read cursor — last processed CreatedAtUtc
        var cursorStr = await redisDb.StringGetAsync(CursorKey);
        var cursor = cursorStr.HasValue
            ? DateTime.Parse(cursorStr!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            : DateTime.MinValue;

        // Load next batch of unenrolled active users
        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Organisation)
            .Where(u => u.TotpSecret == null && u.IsActive && !u.IsSuspended && u.CreatedAtUtc > cursor)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            logger.LogInformation("Legacy2faMigrationJob: No unenrolled users found. Completed.");
            return;
        }

        var processedCount = 0;
        DateTime? lastCreatedAt = null;

        foreach (var user in users)
        {
            try
            {
                var setupUrl = user.Role == UserRoles.Vendor ? "/vendor/settings" : "/settings/2fa";
                var subject = "Action Required: Set Up Two-Factor Authentication";
                var body = $"""
                    Hi {user.FirstName},

                    Your organisation "{user.Organisation.Name}" is adopting two-factor authentication to improve security.

                    Please set up two-factor authentication by visiting:
                    {setupUrl}

                    If you have any questions, please contact your Director.
                    """;

                await emailSender.SendAsync(
                    new EmailMessage(user.Email, subject, body),
                    cancellationToken);

                await auditService.RecordAsync(
                    AuditEventTypes.TwoFactorMigrationEmailSent,
                    user.OrganisationId,
                    subjectUserId: user.Id,
                    metadata: new Dictionary<string, object?> { ["email"] = user.Email },
                    cancellationToken: cancellationToken);

                processedCount++;
                lastCreatedAt = user.CreatedAtUtc;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Legacy2faMigrationJob: Failed to send migration email to {Email}. Continuing.", user.Email);
                // Continue processing — don't stop the batch on one failure
            }
        }

        // Update cursor to last processed CreatedAtUtc
        if (lastCreatedAt.HasValue)
        {
            await redisDb.StringSetAsync(CursorKey, lastCreatedAt.Value.ToString("O"));
        }

        logger.LogInformation(
            "Legacy2faMigrationJob: Processed {Count} users in this batch. Cursor at {Cursor}.",
            processedCount, lastCreatedAt?.ToString("O") ?? "none");
    }
}
```

**Hangfire registration in `Program.cs`** — register as a recurring job (every hour) alongside the existing recurring jobs. The cursor-based idempotency makes this pattern safe — each run processes up to 100 users and advances the cursor:

```csharp
// Legacy 2FA migration — hourly batch processing, cursor-based idempotency (max 100/hr)
RecurringJob.AddOrUpdate<Legacy2faMigrationJob>(
    "legacy-2fa-migration",
    j => j.ExecuteAsync(CancellationToken.None),
    "0 * * * *"); // Every hour
```
This is more resilient than a one-time `Schedule` call — if the app restarts, the job picks up where it left off via the Redis cursor.

### InviteDialogComponent Checkbox Template

Add to the confirmation card section of `invite-dialog.component.ts`, below the existing `<p>` tag:

```html
<mat-checkbox
  [(ngModel)]="include2faInstructions"
  [disabled]="submitting()"
  class="checkbox-2fa"
>
  Include 2FA setup instructions
</mat-checkbox>
```

And pass it in `confirmAndSend()`:
```typescript
const request: SendInvitationRequest = {
  email: this.email.trim(),
  role: this.role,
  include2faInstructions: this.include2faInstructions,
};
```

Add `include2faInstructions = signal(true);` to the component class (NOT `readonly` — `[(ngModel)]` requires a writable target).
Import `MatCheckboxModule` in the component's `imports` array.

### Confirmation Email Sending Location

The confirmation email is sent by `ConfirmationEmailDeliveryJob` (`Jobs/ConfirmationEmailDeliveryJob.cs`). It is enqueued by `RegistrationService.ExecuteConfirmEmailAsync`. The job loads the `ConfirmationToken` with its related `User` and `Organisation` to render the email. This is where the 2FA info should be added.

The `ConfirmationEmailDeliveryJob` uses `ConfirmationEmailTemplate.RenderSubject()` and `RenderBody()` to create the email. The template context already includes `OrganisationName`, `UserName`, `Role`, etc. When building the context, set `Include2faInfo` to `true` (this is the welcome email — FR-3.1 mandates 2FA info for all new users) and `OrgRequires2fa` from `token.Invitation?.Organisation?.Require2fa ?? false`. The job already loads `token.Invitation.Organisation` via the existing Include chain.

### OTP Email Footer — Important Note

The existing `AuthService.LoginAsync` loads the user with `AsNoTracking()` on line 59. The `user.Organisation` navigation property is NOT eagerly loaded. To check `orgRequires2fa`, use a separate query as shown above in the OTP footer pattern. Do NOT remove `AsNoTracking()`.

### Resend Invitation — Handling `include2faInstructions`

When a Director resends an invitation (`InvitationService.ResendInvitationAsync`), the original `include2faInstructions` flag is not stored on the `Invitation` entity. On resend, default `include2faInstructions` to `true` (the checkbox default). The `ResendInvitationAsync` method should call `EnqueueEmailJob` with `include2faInfo: true`.

### Invitation Job Signature Change

Changing `InvitationEmailDeliveryJob.ExecuteAsync` signature to accept `bool include2faInfo` is a breaking change for Hangfire enqueuing. All places that enqueue this job must be updated:
1. `InvitationService.EnqueueEmailJob()` — the protected virtual method
2. `ConfirmationEmailDeliveryJob` does NOT enqueue invitation jobs — it's a separate job

The `BackgroundJob.Enqueue<T>` call uses expression trees, so the new parameter must have a default value or all call sites must be updated. Since `InvitationService.SendInvitationAsync` is the only caller, update both:
```csharp
protected virtual void EnqueueEmailJob(
    Guid invitationId, string rawToken, string signature,
    string targetEmail, string role, bool include2faInfo)
{
    BackgroundJob.Enqueue<InvitationEmailDeliveryJob>(
        j => j.ExecuteAsync(invitationId, rawToken, signature, targetEmail, role, include2faInfo, CancellationToken.None));
}
```

### Testing Standards

- Unit test for `ConfirmationEmailTemplate`: verify 2FA paragraph rendered when `include2faInfo` is true, not rendered when false
- Unit test for `AuthService.LoginAsync`: verify OTP email body includes 2FA footer when user unenrolled + org mandates 2FA
- Unit test for `InvitationEmailDeliveryJob`: verify 2FA paragraph in email body when enabled
- Unit test for `InvitationEmailDeliveryJob`: when `orgRequires2fa` is true, the invitation email body includes the 2FA paragraph even when `include2faInfo` is false (mandate override test)
- Unit test for `InvitationService`: verify `include2faInstructions` flag passed to Hangfire job
- Unit test for `Legacy2faMigrationJob`: verify users scanned correctly, cursor updated, email sent, audit event recorded
- Component test for `InviteDialogComponent`: verify checkbox renders, checked by default, value passed in request
- Integration test: verify `Legacy2faMigrationJob` correctly handles an empty batch (no unenrolled users left)
- All existing tests must pass

### Project Structure Notes

- `Legacy2faMigrationJob` sits in `Jobs/` alongside existing jobs like `InvitationEmailDeliveryJob`, `ConfirmationEmailDeliveryJob`, `ZeroDirectorMonitorJob` — consistent location
- All email template changes are additive to existing templates — no new template files needed
- The Hangfire job signature change is backward-incompatible for the queue but since no jobs are in-flight at deployment (assuming clean deploy), no migration is needed
- Angular changes are minimal (one checkbox, one model field) — limited to `InviteDialogComponent`

### References

- **Epics:** `_bmad-output/planning-artifacts/epics.md` — Story 8: "Proactive Onboarding Emails"
- **Architecture:** `_bmad-output/planning-artifacts/architecture-2fa.md` — Sections "FR-3 Proactive Onboarding Emails", "Implementation Sequence (Step 8)", "Service Layering" diagram showing `Legacy2faMigrationJob → TwoFactorService → EmailSender`
- **Existing email template pattern:** `apps/api/Infrastructure/Email/Templates/ConfirmationEmailTemplate.cs` — Confirmation email template to extend
- **Existing email template pattern:** `apps/api/Infrastructure/Email/Templates/ActivationEmailTemplate.cs` — Activation template pattern reference
- **Existing OTP email:** `apps/api/Infrastructure/Auth/AuthService.cs` lines 136-141 — OTP email body to modify
- **Existing invitation flow:** `apps/api/Domain/RoleManagement/InvitationService.cs` — Send invitation with Hangfire enqueue
- **Existing invitation job:** `apps/api/Jobs/InvitationEmailDeliveryJob.cs` — Invitation email delivery to modify
- **Existing confirmation job:** `apps/api/Jobs/ConfirmationEmailDeliveryJob.cs` — Confirmation email delivery (2FA info source)
- **Existing Hangfire registration:** `apps/api/Program.cs` lines 174-250 — Hangfire setup and recurring jobs
- **Existing zero-Director job pattern:** `apps/api/Jobs/ZeroDirectorMonitorJob.cs` — Cursor-based batch processing pattern reference
- **Existing audit event types:** `apps/api/Infrastructure/Audit/AuditEventTypes.cs` — Add `TwoFactorMigrationEmailSent`
- **Existing Angular invite dialog:** `apps/web/src/app/features/admin/components/invite-dialog/invite-dialog.component.ts` — Add checkbox
- **Existing Angular invite models:** `apps/web/src/app/features/admin/models/admin.models.ts` — `SendInvitationRequest` to extend
- **Story 25-1 (Data Model):** `_bmad-output/implementation-artifacts/25-1-data-model-api-foundation.md` — `Organisation.Require2fa`
- **Story 25-2 (Admin API):** `_bmad-output/implementation-artifacts/25-2-admin-api.md` — Audit event type pattern

## Dev Agent Record

### Agent Model Used

deepseek-v4-flash
