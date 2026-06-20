# Midi-Kaval (Kaval Online)

Monorepo for the Kaval Online case management platform — ASP.NET Core API, Angular PWA (supervisors), React Native (field staff).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (builds with SDK 10 + roll-forward via `global.json`)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (PostgreSQL 16, Redis 7, Azurite blob storage)
- React Native toolchain ([environment setup](https://reactnative.dev/docs/environment-setup)) for mobile dev

## Quick start

### 1. Infrastructure

```bash
docker compose -f infra/docker-compose.yml up -d
```

Services: PostgreSQL `localhost:5432`, Redis `localhost:6379`, Azurite blob `localhost:10000`. Copy `infra/.env.example` to `infra/.env` if you need custom credentials.

### 2. Install JS dependencies

From repo root:

```bash
npm install
npm run build:shared-types
```

### 3. API (port 5049)

**Database migrations** (requires PostgreSQL from step 1):

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationName> --project apps/api --output-dir Migrations
dotnet ef database update --project apps/api
```

On first run in **Development**, the API also applies pending migrations and seeds the Director admin user from configuration.

**Production / staging:** Auto-migrate and seed run only in Development. For other environments:

1. Apply migrations explicitly before or during deploy: `dotnet ef database update --project apps/api`
2. Ensure `Seed:Admin:Email`, `Seed:Admin:Password`, and `Seed:OrganisationId` are set via environment variables or your secret store, then start the API once in Development against that database, **or** run a one-off seed using the same configuration keys (Story 1.4 will add login; the Director row must exist before first login).

Connection string: `ConnectionStrings:DefaultConnection` (see `appsettings.Development.json` or `infra/.env.example`).

**Seed configuration** (set via `apps/api/appsettings.Development.json`, user secrets, or environment variables — never commit real passwords):

| Key | Description |
|-----|-------------|
| `Seed:Admin:Email` | Director login email |
| `Seed:FieldWorker:Email` | Field worker login email (mobile — SocialWorker/CaseWorker) |
| `Seed:FieldWorker:Password` | Field worker password (hashed at insert) |
| `Seed:FieldWorker:Role` | `SocialWorker` or `CaseWorker` |

Example env vars (see `infra/.env.example`): `Seed__Admin__Email`, `Seed__Admin__Password`, `Seed__OrganisationId`.

**Authentication** (Story 1.4 — requires PostgreSQL + Redis):

| Key | Description |
|-----|-------------|
| `Jwt:Issuer` / `Jwt:Audience` | JWT issuer and audience |
| `Jwt:SigningKey` | HMAC signing key (min 32 characters — use user secrets) |
| `Otp:ExpiryMinutes` | OTP challenge TTL (default 5) |
| `Auth:RateLimitPermitLimit` | Max auth requests per IP per window for login, verify-otp, refresh, logout, forgot-password, and reset-password (default 10 each) |
| `Auth:RateLimitWindowSeconds` | Rate limit window in seconds (default 60) |
| `RefreshToken:ExpiryDays` | Refresh token TTL in Redis (default 7) |
| `RefreshToken:MaxActivePerUser` | Max concurrent refresh tokens per user (default 5) |
| `PasswordReset:ExpiryMinutes` | Password reset token TTL in Redis (default 60) |
| `PasswordReset:WebResetUrl` | Base URL for reset links in email (default `http://localhost:4200/reset-password`) |
| `Email:Smtp:Host` | SMTP host for OTP emails (**required** when using `SmtpEmailSender`; integration tests use `FakeEmailSender`) |
| `Email:Smtp:User` | SMTP username (optional) |
| `Email:Smtp:Password` | SMTP password (optional) |
| `Email:Smtp:From` | From address for OTP emails |

Endpoints:

- `POST /api/v1/auth/login` — validate password, email 6-digit OTP
- `POST /api/v1/auth/verify-otp` — issue JWT access token (15 min) + refresh token
- `POST /api/v1/auth/refresh` — rotate refresh token and issue new JWT (cookie or `{ refreshToken }` body)
- `POST /api/v1/auth/logout` — revoke refresh token, clear cookie, optionally remove device row when `deviceInstallId` is sent (**204 No Content**)
- `POST /api/v1/auth/forgot-password` — email a single-use reset link (always **200** with generic message — no account enumeration)
- `POST /api/v1/auth/reset-password` — set new password, invalidate all sessions (user must log in again with OTP)
- `GET /api/v1/auth/me` — current user profile (`Authorization: Bearer` required)
- `POST /api/v1/cases` — create a case (**Coordinator** or **Director** only; returns **201 Created**)

**Case create** (`POST /api/v1/cases`) requires `Authorization: Bearer` with `CoordinatorOrAbove` policy. Required body fields: `crimeNumber`, `stNumber`, `beneficiaryName`, `typeOfOffence`, `offenceClassification` (`Petty` | `Serious` | `Heinous`), `domicile` (`Urban` | `Rural` | `Coastal` | `Tribal` | `Slum`). Optional: `beneficiaryAge` (0–120), `beneficiaryContact`, `isFirstTimeOffender` (defaults `true`). Identifiers are normalized to uppercase on save. Duplicate `crimeNumber` or `stNumber` within the organisation returns **409 Conflict**. Field workers receive **403**; deactivated users receive **403** with "Contact your coordinator". A `case.created` audit event is written in the same transaction as the insert.

- `PATCH /api/v1/cases/{id}/stage` — advance case one stage forward (**Coordinator** or **Director** only; returns **200 OK**)

**Case stage transition** (`PATCH /api/v1/cases/{id}/stage`) requires `targetStage` (next stage in order only). Optional `notes` (max 2000 chars). Stages advance forward one at a time: `ProcessInitiation` → `MaintainAndDevelopment` → `InterSectoralApproach` → `Rehabilitation` → `Reintegration` → `TerminationExclusion`. Skip, backward, or same-stage requests return **422**; `TerminationExclusion` is terminal. A `case_stages` history row and `case.stage.changed` audit event are written in the same transaction.

- `POST /api/v1/cases/check-duplicate` — probe for existing cases by crime/ST (**Coordinator** or **Director** only; returns **200 OK**)

**Duplicate check** (`POST /api/v1/cases/check-duplicate`) requires at least one of `crimeNumber` or `stNumber` (normalized to uppercase for lookup). Returns `{ hasMatch, matches[] }` with `caseId`, identifiers, `beneficiaryName`, `currentStage`, and `matchedOn` (`CrimeNumber` | `StNumber` | `Both`). Read-only — no case or audit rows created. Use before create to surface possible matches; forcing a duplicate save via `POST /api/v1/cases` still returns **409 Conflict**.

- `POST /api/v1/cases/{id}/merge` — merge duplicate intake into an existing case (**Coordinator** or **Director** only; returns **200 OK**)

**Case merge** (`POST /api/v1/cases/{id}/merge`) accepts the same body as create (`CreateCaseRequest`). No new `cases` row is inserted. Draft identifiers must match the target case (at least one of crime/ST); otherwise **422**. If draft crime or ST belongs to a different case, **409 Conflict**. Fill-empty policy: only `beneficiaryAge` (when target is null) and `beneficiaryContact` (when target is empty) may be applied — identifiers and core fields are never overwritten. A `case.merged` audit event is always written; `updatedAtUtc` changes only when fill-empty fields are applied.

- `GET /api/v1/cases/search` — search and filter cases (**Coordinator** or **Director** only; returns **200 OK**)

**Case search** (`GET /api/v1/cases/search`) is read-only (no audit row). Query params (all optional): `q` (free text — OR-match on `crimeNumber`, `stNumber`, `beneficiaryName`, `beneficiaryContact`, and domicile/area string), `currentStage`, `typeOfOffence` (substring), `offenceClassification`, `domicile`, `createdByUserId` (filters `created_by_user_id` when `assignedWorkerUserId` is absent), `assignedWorkerUserId` (filters `assigned_worker_id`; takes precedence over `createdByUserId` when both are present), `overdue` (`true` = `nextVisitDueAtUtc` in the past and stage ≠ `TerminationExclusion`), `page` (default 1), `pageSize` (default 25, max 100). Filters combine with **AND** semantics; `q` uses **OR** within its columns. Crime/ST comparisons uppercase both sides; name/contact/domicile use case-insensitive substring. Response: `{ data: { items, page, pageSize }, meta: { requestId, totalCount } }`. Ordered by `updatedAtUtc` descending. Invalid enum values → **400**. Field workers → **403**.

- `GET /api/v1/cases/search-presets` — list saved filter presets for the current user
- `POST /api/v1/cases/search-presets` — save a named preset (`name` max 64 chars; `filters` mirrors search query shape)
- `DELETE /api/v1/cases/search-presets/{id}` — delete own preset (**404** if not found)

- `GET /api/v1/cases/search/export` — export filtered cases to Excel or PDF (**Coordinator** or **Director** only; returns file download)

**Case export** (`GET /api/v1/cases/search/export`) is a read-only snapshot (no audit row, no case mutations). Required query param: `format` = `xlsx` | `pdf`. Accepts the same optional filter params as search (`q`, `currentStage`, `typeOfOffence`, `offenceClassification`, `domicile`, `createdByUserId`, `assignedWorkerUserId`, `overdue`); `page` / `pageSize` are ignored — export returns **all** matching rows up to `CaseExport:MaxRows` (default **5000**). Over cap → **422** with guidance to narrow filters. Response `Content-Type`: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (xlsx) or `application/pdf` (pdf); `Content-Disposition: attachment` with UTC timestamped filename. Columns: Crime Number, ST Number, Beneficiary Name, Stage, Offence Type, Classification, Area (domicile), Visits, Next Visit Due (UTC), Updated (UTC). Invalid `format` or filter enums → **400**. Field workers → **403**. Web registry uses `HttpClient` blob download (not typed api-client operation). Large async report jobs are Epic 8 — this is synchronous pilot export only.

- `GET /api/v1/cases/{id}` — case detail (**any authenticated active user**; per-resource authorization in service)
- `POST /api/v1/cases/{id}/gps/verify` — verify case GPS + landmark (**assigned field worker** only)
- `POST /api/v1/cases/{id}/transfer` — assign case to field worker with handoff whisper (**Coordinator** or **Director** only)
- `POST /api/v1/cases/{id}/notes` — create typed case note (**any user with case read access**)
- `GET /api/v1/cases/{id}/notes` — chronological case notes timeline (**any user with case read access**)
- `POST /api/v1/cases/{id}/interventions` — create structured intervention (**any user with case read access**)
- `GET /api/v1/cases/{id}/interventions` — list interventions for a case (**any user with case read access**)
- `GET /api/v1/cases/{id}/interventions/{interventionId}` — get single intervention (**any user with case read access**)
- `PATCH /api/v1/cases/{id}/interventions/{interventionId}` — update intervention fields (**any user with case read access**)
- `POST /api/v1/cases/{id}/court-sittings` — create court sitting (**any user with case read access**)
- `GET /api/v1/cases/{id}/court-sittings` — list court sittings for a case (**any user with case read access**)
- `GET /api/v1/cases/{id}/court-sittings/{sittingId}` — get single court sitting (**any user with case read access**)
- `PATCH /api/v1/cases/{id}/court-sittings/{sittingId}` — update court sitting fields (**any user with case read access**)
- `GET /api/v1/court-sittings/upcoming` — upcoming court schedule for assigned field worker (**SocialWorker** / **CaseWorker** only)
- `POST /api/v1/travel-claims` — create draft travel claim (**SocialWorker** / **CaseWorker** only)
- `GET /api/v1/travel-claims` — list own travel claims (**SocialWorker** / **CaseWorker** only)
- `GET /api/v1/travel-claims/{id}` — get own travel claim (**SocialWorker** / **CaseWorker** only)
- `PATCH /api/v1/travel-claims/{id}` — update draft travel claim (**SocialWorker** / **CaseWorker** only)
- `POST /api/v1/travel-claims/{id}/submit` — submit draft claim for review (**SocialWorker** / **CaseWorker** only)
- `POST /api/v1/travel-claims/{id}/approve` — approve submitted claim (**Director** only)
- `POST /api/v1/travel-claims/{id}/return` — return submitted claim with required comment (**Director** only)
- `GET /api/v1/director/travel-claims/pending` — list submitted claims awaiting approval (**Director** only)
- `GET /api/v1/director/travel-claims/{id}` — director review detail (**Director** only)
- `GET /api/v1/supervisor/travel-claims/{id}` — read-only claim review (**Coordinator** / **Director** only)
- `GET /api/v1/supervisor/travel-claims/monthly-totals?year=&month=` — staff travel totals by month (**Coordinator** / **Director** only)
- `GET /api/v1/notifications` — list in-app notifications for the authenticated user
- `GET /api/v1/notifications/unread-count` — returns `{ count: N }` of unread notifications for the authenticated user (used by bell badge polling)
- `PATCH /api/v1/notifications/{id}/read` — mark a notification as read
- `GET /api/v1/notifications/preferences` — role-based notification preference defaults (v1 stub; no persistence)
- `PUT /api/v1/devices/me` — register or update mobile push token for the authenticated user
- `POST /api/v1/attachments/presign` — issue SAS upload URL for a case note or travel claim receipt
- `POST /api/v1/attachments/confirm` — confirm blob upload and receive read SAS
- `GET /api/v1/attachments/{id}/download-url` — fresh read SAS for confirmed attachment
- `GET /api/v1/cases/assigned` — assigned-case list for field workers (**SocialWorker** / **CaseWorker** only)
- `GET /api/v1/users/field-workers` — active SocialWorker/CaseWorker users in org (**Coordinator** or **Director** only)

**Case assignment transfer** (`POST /api/v1/cases/{id}/transfer`) body: `{ assigneeUserId, priorActions, openItems, nextVisitPurpose }` (each required, trimmed, max 500 chars). Updates `cases.assigned_worker_id` / `assigned_at_utc`, inserts `case_assignments` history row, and writes `case.transferred` audit event in one transaction. Assignee must be an active SocialWorker or CaseWorker in the same organisation. Same assignee → **422**. Invalid assignee role → **422**. Returns `{ data: CaseDetailDto }`.

**Case detail** (`GET /api/v1/cases/{id}`) returns summary fields plus optional `handoffWhisper` when the viewer is the **current assignee** and transfer is within the **7-day window** (transfer day = day 1; visible through day 7; hidden from day 8). Whisper text is loaded from the latest `case_assignments` row for the current assignee — never from `cases` columns. Coordinators/directors may read any org case but do not receive whisper content. Response includes GPS fields: `gpsVerified`, `latitude`, `longitude`, `landmark`, and (on detail) `gpsVerifiedAtUtc` / `gpsVerifiedByUserId`. Read is not audited.

**GPS verify** (`POST /api/v1/cases/{id}/gps/verify`) body: `{ latitude, longitude, landmark }` (landmark required, max 500; lat ∈ [-90, 90], lng ∈ [-180, 180]). Assigned field worker only; sets `gps_verified = true` with verifier + timestamp; writes `case.gps.verified` audit in the same transaction. Re-verify updates coordinates/landmark. Coordinators/directors/wrong assignee → **403**.

**Assigned list** (`GET /api/v1/cases/assigned`) returns cases where `assigned_worker_id` equals the caller, ordered by `assigned_at_utc` descending. Supervisors → **403** (use search with `assignedWorkerUserId`).

**Case notes** (`POST` / `GET /api/v1/cases/{id}/notes`) — typed timeline entries on a case (distinct from `visit_notes` captured on visit completion). Note types: `Visit`, `Court`, `Intervention`, `General`. Create body: `{ noteType, bodyText, actionRequired?, actionDueAtUtc? }` (`bodyText` required, max 4000). When `actionDueAtUtc` is set it must be in the future and implicitly sets `actionRequired = true`; when `actionRequired` is true, `actionDueAtUtc` is required. List returns `{ data: { items: CaseNoteDto[] } }` ordered oldest-first. Each item includes author id/email, timestamps, and `attachments[]` summaries for confirmed files (no embedded download URLs). Mutations write `case.note.created` audit (metadata excludes note body). Field workers may add/read notes only on assigned cases; supervisors on any org case; unassigned cases → **403** for field workers. Empty timeline → **200** with `items: []`. Case notes are online-only (not in mobile sync push).

**Interventions** (`POST` / `GET` / `GET/{id}` / `PATCH` on `/api/v1/cases/{id}/interventions`) — structured needed/provided actions per case (distinct from `CaseNoteType.Intervention` timeline notes). Create body: `{ direction, categoryName, description, priority, status?, dueAtUtc?, providedAtUtc?, outcome?, assignedStaffUserId }`. `direction`: `Needed` | `Provided`; `priority`: `High` | `Medium` | `Low`; `status`: `Open` | `InProgress` | `Completed` | `Cancelled` (defaults to `Open`). `categoryName` is free text (max 128) until Epic 9 Legends FK. `Needed` + `Open` requires future `dueAtUtc`; `Provided` requires `providedAtUtc`. `assignedStaffUserId` must be an active user in the org. `Completed` / `Cancelled` updates require non-empty `outcome`. List returns `{ data: { items: InterventionDto[] } }` ordered oldest-first. Mutations write `case.intervention.created` / `case.intervention.updated` audit (metadata excludes description/outcome). RBAC follows case read access. No DELETE in v1.

**Court sittings** (`POST` / `GET` / `GET/{id}` / `PATCH` on `/api/v1/cases/{id}/court-sittings`; distinct from `CaseNoteType.Court` timeline notes) — structured court appearances per case (FR-15). Create body: `{ scheduledAtUtc, courtName, purpose, status?, notes?, outcome? }`. `status`: `Upcoming` | `Attended` | `Postponed` (defaults to `Upcoming`; invalid value → **400**). `Upcoming` create requires future `scheduledAtUtc`; `Postponed` allows past dates for backfill; `Attended` requires non-empty `outcome`. Terminal cases (`TerminationExclusion`) → **422** on create. List returns `{ data: { items: CourtSittingDto[] } }` ordered by `scheduledAtUtc` ascending. PATCH supports `status`, `scheduledAtUtc`, `courtName`, `purpose`, `notes`, `outcome`, `nextCourtAtUtc` (`Attended` requires `outcome`; `nextCourtAtUtc` must be future when set). Mutations write `court.sitting.created` / `court.sitting.updated` audit (metadata excludes notes/outcome). RBAC follows case read access. No DELETE in v1.

**Court sitting schedule** (`GET /api/v1/court-sittings/upcoming`) — field-worker upcoming queue for assigned cases only. Returns `{ data: { items: CourtSittingScheduleItemDto[] }, meta: { requestId, totalCount } }` with nested `CaseSummaryDto` (POCSO redaction), computed `isPastDue` (`Upcoming` + past `scheduledAtUtc`), excludes `TerminationExclusion` cases. Includes past-due `Upcoming` rows. Coordinators/directors → **403** (use case nested list).

**Device registration** (`PUT /api/v1/devices/me`) — upserts FCM/APNs token into `user_devices` for the authenticated user. Body: `{ deviceInstallId, platform: "android"|"ios", pushToken }`. Response `{ data: { id, deviceInstallId, platform, lastRegisteredAtUtc } }` omits the raw token. Re-registering the same `deviceInstallId` updates the token. Registering a token already owned by another user deletes the prior row (device handoff). Logout with optional `deviceInstallId` removes that device row; session invalidation (`token_version` bump) removes all devices for the user. Web does not register device tokens in v1.

**Push delivery (Story 7.2)** — after in-app notifications are persisted, `PushDeliveryService` sends FCM messages to registered devices for field roles when `pushEnabled` and the event channel are enabled. Configure via `PushNotifications:Enabled`, `PushNotifications:ProjectId`, and either `PushNotifications:CredentialsPath` or `PushNotifications:CredentialsJson` (env var `PushNotifications__CredentialsJson`). When disabled or credentials are missing, `FakePushNotificationSender` logs payloads without calling Google (Development default). Tokens prefixed `dev-stub-token-` are skipped. Stale FCM tokens are removed from `user_devices`. **Visit push** is not implemented yet — no overdue visit notification producer exists (future job story).

**Operational email templates (Story 7.3)** — FR-19 desk/field event emails use plain-text templates under `Infrastructure/Email/Templates/` via `EmailDeliveryService`. Auth emails (login OTP, step-up OTP, password reset) remain inline in `AuthService` and use the same `Email:Smtp:*` settings above.

| Event | Recipients | Trigger |
|-------|------------|---------|
| `court.reminder.24h` | Coordinators + assignee | Court reminder job (pre-save; failure blocks dedup) |
| `court.miss.escalated` | Coordinators | Court miss escalation job (pre-save) |
| `travel.claim.submitted` | Coordinators + Directors | Claim submit (post-save, best-effort) |
| `travel.claim.approved` / `returned` | Coordinators + Directors | Director approve/return (post-save) |
| `case.transferred` | Coordinators + Directors + new assignee | Case transfer API (post-save) |
| `report.export.ready` | Requesting user | Hook only — call `EmailDeliveryService.TrySendReportExportReadyAsync(userId, organisationId, reportName, expiresAtUtc)` from Epic 8.5 export job |

Preference gating uses `NotificationPreferencesDefaults` (Story 7.1 stub). Court reminder assignee and case-transfer assignee emails bypass preference checks (operational override). POCSO cases omit free-text sitting `purpose` in court emails. All operational bodies append footer: *Open Kaval Online for details.*

**In-app notifications** (`GET /api/v1/notifications`, `GET /api/v1/notifications/unread-count`, `PATCH /api/v1/notifications/{id}/read`, `GET /api/v1/notifications/preferences`) — per-user notification store (in-app rows from Story 4.5; device tokens from Story 7.1; FCM push from Story 7.2; bell UI from Story 7.4). List returns `{ data: { items: NotificationDto[] } }` for the authenticated user only, newest first. Each item includes `eventType`, `title`, `body`, `caseId`, `resourceType`, `resourceId`, `isRead`, timestamps. Preferences returns role-based stub defaults (`pushEnabled`, `emailEnabled`, `channels`) with no PATCH in v1. `intervention.overdue` events are created by the daily overdue job for assignees when a `Needed` + `Open` intervention passes `dueAtUtc` (deduped via `interventions.overdue_notified_at_utc`). `court.reminder.24h` events are created by the court reminder job ~24h before `Upcoming` sittings (deduped via `court_sittings.reminder_sent_at_utc`). `travel.claim.approved` / `travel.claim.returned` events are created when a Director approves or returns a claim — standardized body: *"Your claim for {destination} (₹{amount}) was approved/returned."* plus optional *"Director note: {comment}"*; approve/return also sends mobile push when a device is registered. Updating an intervention to terminal status with outcome marks related overdue notifications read.

**Intervention overdue job** — `InterventionOverdueBackgroundService` runs on a daily interval (`InterventionOverdueJob:IntervalMinutes`, default 1440) in non-Development environments. Scans `Needed` + `Open` interventions with past `dueAtUtc` and no `overdue_notified_at_utc`, creates in-app notifications, sends push to registered field-worker devices, and sets dedup timestamp. Integration tests invoke `InterventionOverdueJobRunner` directly.

**Court reminder job** — `CourtReminderBackgroundService` runs hourly (`CourtReminderJob:IntervalMinutes`, default 60) in non-Development environments. Scans `Upcoming` court sittings in a ~24h window (`LeadHours` default 24, `WindowMinutes` default 60) with no `reminder_sent_at_utc`, active assignee, and non-terminal case. Sends emails to active Coordinators and the assignee (deduped by address), then persists in-app notification (`court.reminder.24h`), push to assignee devices, audit (`court.sitting.reminder_sent`), and dedup flag. Rescheduling via PATCH clears `reminder_sent_at_utc`. Hosted service disabled in Development; integration tests invoke `CourtReminderJobRunner` directly.

**Court miss escalation job** — `CourtMissEscalationBackgroundService` runs hourly (`CourtMissEscalationJob:IntervalMinutes`, default 60) in non-Development environments. Scans past-due `Upcoming` court sittings with no `miss_escalated_at_utc` and non-terminal cases (including unassigned). Sends coordinator emails first, then persists one in-app notification per active Coordinator (`court.miss.escalated`), case flag (`court_miss_flagged_at_utc`), audit (`court.sitting.miss_escalated`), and dedup flag. Push is attempted but skipped for desk roles (`pushEnabled: false`). PATCH to `Attended`/`Postponed` or reschedule to a future time clears escalation state and recalculates the case flag. Integration tests invoke `CourtMissEscalationJobRunner` directly.

**Crisis queue API** — `GET /api/v1/supervisor/crisis-queue` (Coordinator/Director) returns court-miss rows (`severity=critical`) plus pending travel claim rows (`severity=neutral`, `badgeLabel=Claim`) in v1. Visit/handoff rows and Redis caching arrive in Epic 8.1; full web UI polish in Story 8.2. Web crisis queue and Admin travel approval UI implemented in Story 6.3.

**Travel claims** (`POST` / `GET` / `GET/{id}` / `PATCH` / `POST/{id}/submit` on `/api/v1/travel-claims`; FR-18, CAP-9) — field-worker travel reimbursement requests. Create body: `{ claimDate, startLocation, destination, transportMode, amount, autoNumber?, notes?, caseIds[] }`. `transportMode`: `Bus` | `Auto` | `Petrol` | `Other`; `autoNumber` required when `Auto`. At least one readable linked case required. New claims start as `Draft`; only the claimant may mutate. List returns `{ data: { items: TravelClaimDto[] }, meta: { requestId, totalCount } }` for the caller's claims only (newest `claimDate` first). Non-claimant `GET /{id}` → **404**. Submit moves `Draft` → `Submitted` and sets `submittedAtUtc`. **Receipt rule:** `Bus` / `Auto` / `Petrol` require a confirmed receipt attachment before submit (`Other` exempt) → **422** if missing. Receipts use the attachment flow with `resourceType: "TravelClaim"` while status is `Draft` only (presign/confirm on non-draft → **422**). Coordinators/directors may download receipts on `Submitted`/`Approved`/`Returned` claims for approval prep. **Director approval:** `POST /{id}/approve` (optional comment) and `POST /{id}/return` (required comment) move `Submitted` → `Approved`/`Returned`, set decision fields, write audit, and create in-app notification for claimant (`travel.claim.approved` / `travel.claim.returned`; push deferred Story 7.2). `GET /api/v1/director/travel-claims/pending` lists org `Submitted` claims (oldest first). Claimant `GET /{id}` includes `decisionComment` when approved/returned. Audits: `travel.claim.created` / `travel.claim.updated` / `travel.claim.submitted` / `travel.claim.approved` / `travel.claim.returned`.

**Travel claim monthly totals** (`GET /api/v1/supervisor/travel-claims/monthly-totals?year=YYYY&month=M`) — aggregates `Submitted` and `Approved` claims by `claimDate` calendar month (UTC). Returns `{ data: { items: TravelClaimMonthlyTotalDto[] }, meta: { requestId, totalCount } }` grouped by claimant (`staffUserId`, `staffEmail`, `claimCount`, `totalAmount`). Invalid month or year &lt; 2000 → **400**. Field workers → **403**.

**Note attachments** (presign → PUT blob → confirm): create a note or draft travel claim first, then `POST /api/v1/attachments/presign` with `{ resourceType: "CaseNote" | "TravelClaim", resourceId: <noteId|claimId>, fileName, contentType, fileSizeBytes }`. Allowed types: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`; max size **10 MiB** (`BlobStorage:MaxUploadBytes`). Response includes `uploadUrl`, `requiredHeaders` (`x-ms-blob-type: BlockBlob`, `Content-Type`), and `expiresAtUtc` (SAS **15 min**). Client PUTs bytes to `uploadUrl`, then `POST /api/v1/attachments/confirm` with `{ attachmentId }`. Confirm verifies blob exists and returns `downloadUrl` (read SAS, 15 min). Timeline list shows attachment metadata only; call `GET /api/v1/attachments/{id}/download-url` for preview. Local dev uses Azurite (`infra/docker-compose.yml`, port **10000**). RBAC follows case-note read access. Audits: `attachment.presign.issued`, `attachment.confirmed` (no file bytes or SAS in metadata).

**POCSO discreet mode** (Story 3.8): cases may set `sensitivityLevel` = `Standard` | `POCSO` on create (optional; default `Standard`). For field-worker mobile paths (`GET /visits/today`, `GET /cases/assigned`, `GET /cases/{id}`, sync push visit payloads), `beneficiaryName` is **initials only** when `POCSO`. Full PII requires `POST /api/v1/cases/{id}/reveal-pii` after recent login or step-up OTP (`POST /auth/step-up`, `POST /auth/verify-step-up`); writes `case.pii.revealed` audit. Development seeds a POCSO case **POCSO-DEV-001** / **ST-POCSO-001** (beneficiary **Ravi Kumar** → displays as **R. K.**) assigned to the field worker seed account.

**Field-worker case access** (Story 2.8 — first per-resource case RBAC):

| Endpoint | Coordinator/Director | Field worker |
|----------|---------------------|--------------|
| `GET /cases/{id}` | Any org case | Assigned only |
| `GET /cases/{id}/notes` | Any org case | Assigned only |
| `POST /cases/{id}/notes` | Any org case | Assigned only |
| `GET /cases/{id}/interventions` | Any org case | Assigned only |
| `POST /cases/{id}/interventions` | Any org case | Assigned only |
| `PATCH /cases/{id}/interventions/{id}` | Any org case | Assigned only |
| `GET /cases/{id}/court-sittings` | Any org case | Assigned only |
| `POST /cases/{id}/court-sittings` | Any org case | Assigned only |
| `PATCH /cases/{id}/court-sittings/{id}` | Any org case | Assigned only |
| `GET /court-sittings/upcoming` | **403** | Own assignments |
| `GET /notifications` | Own notifications | Own notifications |
| `PATCH /notifications/{id}/read` | Own notifications | Own notifications |
| `GET /cases/assigned` | **403** | Own assignments |
| `POST /cases/{id}/transfer` | Yes | **403** |
| `GET /cases/search` | Yes | **403** (unchanged) |

**Search presets** are scoped per user per organisation. Duplicate preset name → **409 Conflict**. Empty `filters` object is allowed. Preset mutations require `CoordinatorOrAbove`.

**Pilot filter semantics:** `domicile` stands in for district/area until a dedicated field exists; `assignedWorkerUserId` filters on true assignee (`cases.assigned_worker_id`); `createdByUserId` filters intake author when assignee filter is absent; `nextVisitDueAtUtc` is set/cleared by the visit scheduler (`POST/complete/reschedule` on visits).

### Visit scheduler (Story 3.1 — field worker API)

Coordinators schedule visits; field workers consume dedicated list endpoints (do **not** compose from `GET /cases/assigned`).

| Endpoint | Role | Purpose |
|----------|------|---------|
| `POST /api/v1/cases/{id}/visits` | Coordinator/Director | Schedule visit (`scheduledAtUtc`, optional `assigneeUserId`; defaults to case assignee) |
| `GET /api/v1/cases/{id}/visits` | Coordinator/Director | Case visit history incl. `lastRescheduleReason` |
| `GET /api/v1/visits/today` | Field worker | Today's queue (UTC date) |
| `GET /api/v1/visits/today/grouping-suggestion` | Field worker | Proximity clusters + suggested route for today's verified-GPS visits |
| `POST /api/v1/sync/push` | Field worker | Idempotent replay of offline visit mutations (`visit.start`, `visit.complete`) and draft travel claims (`travel.claim.create`) |
| `GET /api/v1/visits/weekly` | Field worker | Current UTC week (Mon 00:00 – Sun 23:59:59.999) |
| `GET /api/v1/visits/overdue` | Field worker | Past-due scheduled/in-progress visits |
| `POST /api/v1/visits/{id}/start` | Field worker | Start visit (`Scheduled` → `InProgress`; sets `started_at_utc`) |
| `POST /api/v1/visits/{id}/complete` | Field worker | Complete visit with required `{ note }` body; inserts `visit_notes`; increments `cases.visit_count`; clears `next_visit_due_at_utc` |
| `POST /api/v1/visits/{id}/reschedule` | Field worker | Reschedule with required `reason` (max 500) |

List responses use `{ data: VisitListResultDto, meta: { requestId, totalCount } }`. Completing a visit writes `visit.completed` audit; scheduling/rescheduling write `visit.scheduled` / `visit.rescheduled`. Pilot rule: one active (`Scheduled`/`InProgress`) visit per case.

**Session contract:** verify-otp and refresh return `refreshToken` in the JSON body (mobile) and set an httpOnly `refresh_token` cookie on path `/api/v1/auth` with `SameSite=Strict` and `Secure` (except in Development). Refresh **rotates** the token: the previous refresh token is invalidated; reuse of a rotated token revokes all sessions for that user. JWT access tokens embed `token_version`; incrementing `users.token_version` (via `IUserSessionService.InvalidateUserSessionsAsync`) forces re-login. Deactivated users receive **403** with "Contact your coordinator". Auth events are appended to `audit_events` (`auth.login.success`, `auth.logout`, `auth.password_reset.requested`, `auth.password_reset.completed`, etc.). **Forgot-password** always returns the same generic success message whether the email exists, is deactivated, or SMTP fails (no enumeration). Admin force-reset is deferred to Story 9.2.

**RBAC** (Story 1.8 — server-enforced on every protected endpoint):

| Policy | Roles |
|--------|-------|
| `DirectorOnly` | Director |
| `CoordinatorOrAbove` | Director, Coordinator |
| `FieldWorker` | SocialWorker, CaseWorker |
| `Director` / `Coordinator` / `SocialWorker` / `CaseWorker` | Single role each |

Apply on controllers:

```csharp
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[HttpPost]
public IActionResult CreateCase(...) { ... }
```

Wrong role → **403** Problem Details (`"You do not have permission to perform this action."`). Deactivated users still get `"Contact your coordinator"`. Never `[AllowAnonymous]` on data mutations.

Temporary probe routes (hidden from Swagger; removed when Epic 2 ships real endpoints):

- `POST /api/v1/rbac-probe/coordinator-mutation`
- `GET /api/v1/rbac-probe/director-only`
- `GET /api/v1/rbac-probe/field-action`

```bash
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project apps/api
```

Health check: [http://localhost:5049/health](http://localhost:5049/health)

Swagger UI: [http://localhost:5049/swagger](http://localhost:5049/swagger)  
OpenAPI JSON: [http://localhost:5049/swagger/v1/swagger.json](http://localhost:5049/swagger/v1/swagger.json)

Generate TypeScript API client (API must be running):

```bash
npm run generate:api-client
npm run build -w @midi-kaval/api-client
```

### 4. Web (Angular PWA)

Build shared packages first if types are stale:

```bash
npm run build -w @midi-kaval/shared-types
npm run build -w @midi-kaval/api-client
```

Start the API (with CORS allowing `http://localhost:4200`) and the web app:

```bash
npm run start:api
npm run start:web
```

Open [http://localhost:4200](http://localhost:4200) — unauthenticated users land on **Sign in**. Supervisor flow: email/password → OTP → `/home`. Field roles (`SocialWorker`, `CaseWorker`) see **Use the mobile app for your role**.

**Auth notes (web):**

- `POST /api/v1/auth/verify-otp` and `POST /api/v1/auth/refresh` use **httpOnly cookie** refresh tokens (`withCredentials: true`); do not send `refreshToken` in the JSON body.
- Access token is stored in `sessionStorage` and sent as `Authorization: Bearer` on API calls.
- Development CORS: `Cors:AllowedOrigins` defaults to `http://localhost:4200` in `appsettings.Development.json` (override via `Cors__AllowedOrigins__0` in `infra/.env`).

Seed login: `director@pilot.example` (password from `Seed__Admin__Password` / user secrets). **Director tools:** **Admin → Travel claims** lists pending submissions for approve/return; **Crisis queue** shows court-miss and pending claim rows (claim rows open review; coordinators see read-only detail).

### 5. Mobile (React Native)

Build shared packages first if types are stale:

```bash
npm run build -w @midi-kaval/shared-types
npm run build -w @midi-kaval/api-client
```

Start the API and Metro bundler:

```bash
npm run start:api
npm run start:mobile
```

**Run on Android** (requires [Android Studio / SDK](https://reactnative.dev/docs/environment-setup) and an emulator or USB device):

```bash
# Terminal 1 — API + Metro (if not already running)
npm run start:api
npm run start:mobile

# Terminal 2 — build and install the debug APK
npm run android:mobile
```

Open the project in Android Studio via `apps/mobile/android` if you need to manage SDKs or run Gradle directly (`./gradlew assembleDebug` from that folder).

**API URL for devices/emulators:**

| Target | `apiBaseUrl` |
|--------|----------------|
| iOS Simulator | `http://localhost:5049` |
| Android Emulator | `http://10.0.2.2:5049` |
| Physical device | Your machine's LAN IP (e.g. `http://192.168.1.x:5049`) |

Edit `apps/mobile/src/config/environment.ts` for non-default hosts.

**Auth flow (mobile):**

- Login → OTP → bottom tabs **Today · Cases · More** for `SocialWorker` / `CaseWorker`
- `Director` / `Coordinator` see **Use the web app for your role**
- **Refresh/logout send `{ refreshToken, deviceInstallId? }` in JSON body** — tokens stored in Keychain (not AsyncStorage); stable `deviceInstallId` also in Keychain for push unregister on logout
- OTP challenge may use AsyncStorage (non-secret); access/refresh tokens use Keychain only

**Push device registration (mobile, Story 7.1):** After login/bootstrap the app calls `PUT /api/v1/devices/me` with `{ deviceInstallId, platform, pushToken }`. Registration retries on app foreground; failures never block auth. **Firebase setup (production):** add `@react-native-firebase/app` + `@react-native-firebase/messaging`, place `google-services.json` under `apps/mobile/android/app/`, enable Push Notifications on iOS, and configure APNs in Firebase — do not commit secrets. Without Firebase configured, dev builds use a stub token (`dev-stub-token-{platform}`) so the API path remains testable.

**Push notification tap handling (mobile, Story 7.2):** When authenticated, the app registers FCM open/foreground handlers. Tapping a travel-claim push marks the notification read and navigates to **More → Travel Claim** in view mode. Other event types log only (full deep links in Story 7.4).

**Notification bell badge (web + mobile, Story 7.4):** A bell icon in the web sidebar and a badge on the mobile **More** tab show the unread notification count. The count is polled every 60 seconds via `GET /api/v1/notifications/unread-count`. On mobile the poll also fires on app foreground. On web the poll resets when navigating to `/notifications`. The badge hides when the count reaches 0. The `/notifications` lazy route shows the full list with read/unread state and tap-to-navigate deeplinking (cases, travel claims, court sittings).

**GPS navigation (field app):** Navigate on Today / Active Visit opens Google Maps when `gpsVerified` is true; otherwise prompts **Capture landmark before navigate** and calls `POST /api/v1/cases/{id}/gps/verify`. Android manifest includes `ACCESS_FINE_LOCATION` / `ACCESS_COARSE_LOCATION`; iOS location usage string is deferred until an `ios/` project is added.

**Offline visit sync (field app):** When the device is offline, start/complete actions enqueue mutations in AsyncStorage (`midi-kaval:offline-sync-queue:v1`) and update the Today list optimistically. On reconnect or app foreground, the app calls `POST /api/v1/sync/push` with idempotent `clientMutationId` values. Each visit shows a **sync chip** on the Command Strip and Active Visit (`Synced`, `Saved on this device`, `Uploading`, or `Sync failed`); tap a failed chip or open **More → Sync queue** to inspect queued mutations and **Retry sync**.

**Travel claims (mobile, FR-18):** Field workers open **More → Travel** to list and create draft claims with receipt photos. **More → Notifications** lists in-app notifications (`GET/PATCH /api/v1/notifications`); tapping `travel.claim.approved` / `travel.claim.returned` opens the read-only claim with Director feedback. Online path uses `/api/v1/travel-claims` (create/update/submit). Offline v1 supports **create draft only** via `travel.claim.create` on the same sync queue; receipt images captured offline upload after the server assigns a `claimId`. Bus/Auto/Petrol require a confirmed receipt before submit (see API travel-claims section). Non-field roles see a permission message on the Travel and Notifications screens.

Manual smoke: field worker seed `karthik.k.82@outlook.com` → OTP → **Today · Cases · More** tabs. Director seed `director@pilot.example` → web-only on mobile.

## Repository layout

```
apps/api/          ASP.NET Core 8 REST API
apps/web/          Angular 19 PWA + Material
apps/mobile/       React Native field app
packages/api-client/   OpenAPI-generated TypeScript client
packages/shared-types/ Shared TypeScript enums
tests/             xUnit, Playwright stubs
infra/             docker-compose.yml
```

## Tests

**Full regression** (story completion / before PR):

```bash
npm run test:all
```

Equivalent to `test:web` + `test:mobile` + `test:api`.

**During development** (faster, run only what you changed):

| Command | What it runs |
|---------|----------------|
| `npm run test:api:unit` | API unit tests only (~seconds) |
| `npm run test:api:integration` | All API integration tests (Testcontainers) |
| `npm run test:api:cases` | Case-related integration tests only (`CaseCreate`, `CaseMerge`, etc.) |
| `npm run test:api:auth` | Auth/session integration tests only |
| `npm run test:clients` | Web + mobile unit tests (no .NET) |
| `npm run test:web` | Angular/Karma specs |
| `npm run test:mobile` | React Native/Jest specs |

Filter a single .NET test class (example):

```bash
dotnet test tests/api.integration --filter "FullyQualifiedName~CaseMergeTests"
```

Add `--no-build` after the first run if the solution is already compiled.

Integration tests use **Testcontainers** for PostgreSQL/Redis (schema + auth tests); HTTP regression tests run with `ASPNETCORE_ENVIRONMENT=Testing` and do not require local database or Redis. Docker must be available for containerized tests.

## Planning artifacts

Product and technical contracts live in `_bmad-output/`. AI agents should read `_bmad-output/project-context.md` before implementing stories.
