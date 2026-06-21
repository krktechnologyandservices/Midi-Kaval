# Deferred Work

Items deferred during BMad workflows — revisit in future stories or tooling passes.

## Deferred from: code review of 1-1-monorepo-scaffold-and-local-dev-environment.md (2026-06-13)

- **docker-compose has no named volumes** (`infra/docker-compose.yml`) — Postgres data lost on container recreate. Acceptable for local dev scaffold; add volumes in a later infra story.

- **npm workspace Angular hoisting workaround** (`package.json`) — root `@angular/compiler` devDependency fixes Karma resolution. Consider `.npmrc` `public-hoist-pattern` in a future tooling cleanup.

## Deferred from: code review of 1-3-users-schema-and-seed-admin-account.md (2026-06-14)

- **Concurrent seed race on parallel Development startups** (`AdminUserSeeder.cs`) — dev-only edge case; upsert can wait until deploy hardening.

- **`organisation_id` references no organisations table yet** (`AdminUserSeeder.cs`) — organisations schema is a later epic.

- **`TestingWebApplicationFactory` registers no DbContext substitute** (`Program.cs`) — Story 1.4 auth tests will need factory override.

- **No integration test for missing/invalid seed config silent skip** (`AdminUserSeeder.cs`) — logging patch covers operational gap.

- **Custom env names (Staging/Local) require DB connection but skip migrate/seed** (`DatabaseInitializer.cs`) — ops documentation addressed in README patch.

## Deferred from: code review re-review of 1-3-users-schema-and-seed-admin-account.md (2026-06-14)

- **DB unique index is case-sensitive** (`UserConfiguration.cs`) — citext/functional index in later story.

- **README Production seed via temporary Development startup** (`README.md`) — dedicated seed CLI in later infra story.

- **No max-length guard on seed email before insert** (`AdminUserSeeder.cs`) — admin email length edge case.

## Deferred from: code review of 1-4-login-and-email-otp-api.md (2026-06-14)

- **OTP stored as SHA256 without per-challenge salt** (`OtpHasher.cs`) — pilot TTL mitigates; harden in security pass.

- **Refresh token stored as SHA256 digest not slow hash** (`RefreshTokenStore.cs`) — Story 1.5 refresh scope.

- **Timing-based email enumeration via login response latency** (`AuthService.cs`) — broader hardening pass.

- **No per-email OTP send throttle beyond IP rate limit** (`AuthService.cs`) — enhancement for abuse prevention.

- **HS256 signing key rotation not implemented** (`JwtTokenService.cs`) — ops concern for production deploy.

- **Refresh token no rotation-on-use or reuse detection** (`RefreshTokenStore.cs`) — Story 1.5 scope.

- **Rate limit bypass via proxy rotation** (`AuthServiceCollectionExtensions.cs`) — infrastructure/WAF concern.

- **Unbounded refresh tokens per user in Redis** (`RefreshTokenStore.cs`) — cap in Story 1.5.

- **Test config applied in CreateHost not ConfigureWebHost** (`AuthWebApplicationFactory.cs`) — works; style deviation only.

## Deferred from: code review of 2-5-case-merge-workflow.md (2026-06-15)

- **Mobile `buildCreateBody` omits optional `beneficiaryAge`/`beneficiaryContact`** (`CaseCreateScreen.tsx`) — mobile create form never collected these fields (Story 2.4); merge sends partial body; API fill-empty still works when fields absent.

## Deferred from: code review of 2-4-duplicate-match-sheet-on-web-and-mobile-create.md (2026-06-15)

- **Legacy `CasesScreen.tsx` placeholder unused** (`apps/mobile/src/screens/cases/CasesScreen.tsx`) — replaced by `CasesStackNavigator`; safe to delete in a cleanup pass.

## Deferred from: code review of 2-1-case-aggregate-schema-and-create-api.md (2026-06-15)

- **Stale JWT role/org not re-read from DB on each request** (`AuthServiceCollectionExtensions.cs`) — demoted coordinator or moved org can act until token expiry; Epic 1 pattern; harden in security pass.

- **No FK on `cases.organisation_id` / `created_by_user_id`** (`AddCases` migration) — organisations table and cascade rules deferred per architecture.

## Deferred from: code review of 1-5-refresh-logout-and-forced-session-invalidation.md (2026-06-14)

- **`AuditService` auto-commits outside caller transaction** (`AuditService.cs`) — acceptable for auth-only flows with no coupled user mutation today; revisit when audit must share transactions with domain writes.

- **Logout does not bump `token_version` (access JWT valid until expiry)** (`AuthService.cs`) — standard short-lived access-token pattern; 15 min TTL acceptable for pilot.

## Deferred from: code review of 2-6-case-search-filters-and-saved-presets.md (2026-06-15)

- **XML doc comments on new CasesController endpoints** (`apps/api/Controllers/V1/CasesController.cs`) — entire controller lacks `///` summaries; pre-existing pattern from Stories 2.1–2.5; add in OpenAPI/documentation pass.

## Deferred from: code review of 2-7-case-export-to-excel-and-pdf.md (2026-06-16)

- **`CaseExportOptions.MaxRows` not validated for zero/negative** (`CaseExportOptions.cs`) — misconfiguration edge; pilot default 5000 is fine.

## Deferred from: code review of 2-8-case-assignment-transfer-and-handoff-whisper.md (2026-06-16)

- **Mobile pull-to-refresh first use** (`apps/mobile/src/screens/cases/CasesListScreen.tsx`) — AC8 introduces RefreshControl; no existing pull-to-refresh pattern in mobile repo; acceptable greenfield within story scope.

- **Transfer on `TerminationExclusion` cases** (`CaseService.TransferAsync`) — pilot allows transfer at any lifecycle stage; no stage gate in AC1 unless product adds 422 rule later.

- **Web registry assignee filter and preset round-trip for `assignedWorkerUserId`** (`case-registry.component.ts`) — Story 2.9 full registry IA; API + preset JSON contract complete in 2.8.

- **`case_assignments` table lacks FK constraints to `cases` / `users`** (`CaseAssignmentConfiguration.cs`) — pilot data-model pattern; integrity enforced in `CaseService`; align with organisations FK story later.

## Deferred from: code review of 2-9-web-case-registry-and-detail-ui.md (2026-06-16)

- **Sidebar “collapse” at 768–1023px only narrows width** (`supervisor-shell.component.scss`) — labels remain full text; icon-only or drawer pattern deferred to Epic 9 polish.

- **Registry assignee filter / preset `assignedWorkerUserId` UI** (`case-registry.component.ts`) — carried from 2.8 review; API contract complete; filter UI out of shell/stage scope.

- **Shell hardcoded colors vs UX `DESIGN.md` tokens** (`supervisor-shell.component.scss`) — placeholder shell acceptable for Story 2.9; token pass in Epic 9.

- **`rootRedirectGuard` unused after shell refactor** (`auth.guard.ts`) — dead export; safe cleanup in a hygiene pass.

- **Web test suite green task** — deferred per release-hardening policy (tests authored, not run).

## Deferred from: code review of 3-1-visit-scheduler-api.md (2026-06-16)

- **Transfer leaves stale visit assignee** (`VisitService.cs`) — case reassignment does not update or cancel active visits; new assignee blocked from scheduling until follow-up when transfer+visit lifecycle is specified.

- **Concurrent schedule/complete without optimistic concurrency** (`VisitService.cs`) — `AnyAsync` + insert and `visit_count += 1` can race under parallel requests; pilot scale; add row version or partial unique index in hardening pass.

- **Deactivated assignee cannot clear active visit** (`VisitService.cs`) — all visit mutations require active FieldWorker policy; admin/ops path not in 3.1 scope.

- **TerminationExclusion hides visit from lists but not mutations** (`VisitService.cs`) — complete/reschedule still allowed via direct POST if visit ID known; terminal-case visit lifecycle not specified in AC.

- **No DB partial unique index for one active visit per case** (`AddVisits` migration) — enforcement is app-layer only; matches pilot no-FK pattern from Story 2.x.

- **InProgress reschedule leaves StartedAtUtc set** (`VisitService.cs`) — status reset to Scheduled without clearing start timestamp; InProgress not used until Story 3.3.

- **N+1 handoff queries on field-worker list endpoints** (`VisitService.cs`) — `BuildHandoffWhisperAsync` per row; acceptable pilot list sizes.

## Deferred from: code review of 3-2-morning-command-strip-mobile-home.md (2026-06-16)

- **Cached rows show static "Synced" chip before fresh fetch** (`TodayScreen.tsx`) — misleading vs Story 3.7 sync states but matches pilot AC2 for API-success path; deferred to Story 3.7.

- **No automated test for stale banner or skeleton UI** (`TodayScreen.tsx`) — AC5 covered manually; AC12 minimum matrix satisfied; optional hardening pass.

## Deferred from: code review of 3-3-active-visit-flow-with-start-and-complete.md (2026-06-16)

- **N+1 handoff queries on field-worker list endpoints** (`VisitService.cs`) — `BuildHandoffWhisperAsync` per row; acceptable pilot list sizes; carried from 3.1.

- **Transfer leaves stale visit assignee** (`VisitService.cs`) — case reassignment does not update active visits; follow-up when transfer+visit lifecycle specified; carried from 3.1.

- **No DB partial unique index for one active visit per case** (`AddVisits` migration) — app-layer enforcement only; hardening pass; carried from 3.1.

## Deferred from: code review of 3-4-gps-capture-landmark-and-google-maps-navigation.md (2026-06-16)

- **`CaseApiService.extractErrorMessage` network fallback says "Crime/ST"** (`CaseApiService.ts`) — pre-existing shared helper copy; misleading for GPS verify errors but not introduced by this story.

## Deferred from: code review of 3-5-proximity-visit-grouping-suggestion.md (2026-06-17)

- **AC5 three-visit integration scenario not in `VisitGroupingTests.cs`** — unit test `VisitProximityGrouperTests.Group_VisitsEightKmApart` covers A+B cluster vs C singleton; add integration test in a hardening pass if desired.

- **No integration test for `gps_coordinates_missing` exclusion** (`VisitService.BuildGroupingSuggestion`) — exclusion reason implemented; mirror `gps_unverified` integration coverage later.

- **No mobile Jest test for grouping API failure + inline retry (AC12)** (`TodayScreen.tsx`) — retry UI present; test gap only.

## Deferred from: code review of 3-6-offline-visit-storage-and-sync-push-api.md (2026-06-17)

- **Note-merge-on-completed integration test missing** (`SyncPushTests.cs`) — AC11 coverage gap; add when hardening sync conflict paths.

- **Server-ahead `visit.complete` rejection integration test missing** (`SyncPushTests.cs`) — AC3/AC4 non-merge rejection path untested.

- **Partial batch success integration test missing** (`SyncPushTests.cs`) — AC5 per-item outcomes in mixed batch untested.

- **`SyncPushService` idempotency ledger unit tests missing** (`SyncPushService.cs`) — only `VisitSyncNoteMergeTests` covers note merge helper.

- **Mobile offline start/complete screen tests missing** (`TodayScreen.test.tsx`, `ActiveVisitScreen.test.tsx`) — AC11 offline path coverage gap.

- **`mobileSyncPushService` rejected/duplicate removal tests missing** (`mobileSyncPushService.test.ts`) — only `applied` path asserted.

- **`VisitApiService` online-bypass-queue tests missing** (`VisitApiService.ts`) — AC8 direct API path untested.

- **`refreshSession` failure → queue `error` test missing** (`mobileSyncPushService.ts`) — AC7 auth refresh gate untested.

- **AC12 pull-to-refresh preservation test missing** (`TodayScreen.test.tsx`) — queue + grouping survival on refresh untested.

## Deferred from: code review of 3-8-discreet-pocso-capture-mode.md (2026-06-17)

- **GPS/landmark visible on case detail without step-up** (`CaseDetailPlaceholderScreen.tsx`) — out of scope for discreet header ACs; location fields are separate from beneficiary PII reveal.

- **Handoff whisper may contain PII in operational text** (`CaseService.cs`) — acknowledged operational risk; coordinators control whisper content at transfer.

- **No collapse-after-reveal UX** (`DiscreetHeader.tsx`) — not specified in AC4/AC5; expanded state persists for session.

- **`commandStripCache` may retain pre-redaction visit payloads until refresh** (`commandStripCache.ts`) — cache invalidation is 3.6/3.7 concern; AC11 forbids writing revealed PII to cache.

- **OpenAPI/api-client regen requires running API** (`packages/api-client`) — run `npm run generate:api-client` with API up; mobile uses extended local types until then.

## Deferred from: code review of 4-1-case-notes-api-and-timeline (2026-06-19)

- **VisitGroupingTests failures (2/250)** — POCSO seed visit pollutes today visit counts; pre-existing, unrelated to Story 4.1.
- **Unbounded timeline GET without pagination** — v1 scope; revisit if cases accumulate very large note volumes.
- **Concurrent case delete → FK DbUpdateException may 500** — rare race; no v1 requirement for graceful 404 on insert.

## Deferred from: code review of 4-2-attachment-presign-upload-for-notes (2026-06-19)

- **Blob container bootstrap only in Development startup** (`DatabaseInitializer.cs`) — story scoped to local Azurite; production ops must pre-create container or extend bootstrap in infra story.
- **Orphan Pending attachment rows / no presign rate limit** (`AttachmentService.cs`) — v1 out of scope; optional cleanup job in future story.
- **Concurrent double-confirm race (no row version)** (`AttachmentService.cs`) — rare; optimistic concurrency deferred to v1.1 if needed.

## Deferred from: code review of 4-3-notes-timeline-ui-web-and-mobile (2026-06-19)

- **`react-native-document-picker@9.3.1` deprecated on npm** (`apps/mobile/package.json`) — package still works on RN 0.76; migrate to `@react-native-documents/picker` in a future mobile tooling story.
- **Mobile badge colors use hardcoded hex** (`CaseDetailPlaceholderScreen.tsx`) — matches pre-existing screen styling; token migration tracked for Epic 9 theme story.

## Deferred from: code review of 4-4-interventions-crud-api (2026-06-19)

- **`category_name` varchar instead of Legends FK** (`Intervention.cs`) — story documents Epic 9 `legend_intervention_categories` migration; matches offence-type pre-legends pattern from Story 2.1.

## Deferred from: code review of 4-5-interventions-ui-and-overdue-job (2026-06-19)

- **Mobile interventions UI not implemented (AC4)** — story `in-progress`; web + API backbone landed first.
- **Integration/web tests and README** — unchecked story tasks; complete during dev-story finish pass.
- **Notification bell UI (Epic 7.4)** — in-app store API only in this story; push deferred to Story 7.2 per story scope.

## Deferred from: code review of 4-5-interventions-ui-and-overdue-job (2026-06-19, final pass)

- **Notification bell UI (Story 7.4)** — list/mark-read API only; bell chrome deferred.
- **Integration test for terminal status without outcome → 400** — UI validates; explicit API negative test optional.

## Deferred from: code review of 5-2-court-schedule-ui-web-case-detail-and-mobile.md (2026-06-19)

- **API PATCH cannot clear `nextCourtAtUtc` when field omitted or null** (`CourtSittingService.cs` `HasValue` guard) — pre-existing Story 5.1 contract; web `null` send also ineffective; needs API sentinel or explicit clear flag in follow-up.

- **`useCourtCountdown` hides banner on API failure** (`useCourtCountdown.ts`) — AC 5 only requires hidden when no sittings; lightweight banner pattern; no error UI on Today tab.

## Deferred from: code review of 5-3-court-reminder-background-job.md (2026-06-19)

- **`purpose` is user-entered free text in reminder email body** (`CourtReminderBackgroundService.cs`) — could contain beneficiary names if staff enter PII in purpose field; POCSO guardrails cover notes/outcome/beneficiaryName only; data-entry policy / validation follow-up.

## Deferred from: code review of 5-4-court-miss-escalation-and-crisis-queue-feed.md (2026-06-19)

- **`purpose` is user-entered free text in miss-escalation email body** (`CourtMissEscalationBackgroundService.cs`) — same data-entry PII risk as court reminder job; POCSO guardrails cover crime/ST only.

## Deferred from: code review of 6-1-travel-claim-api-with-receipt-validation.md (2026-06-20)

- **`ListMineAsync` N+1 queries per claim** (`TravelClaimService.cs`) — MapToDtoAsync per row; acceptable pilot list sizes; batch-load in hardening pass.

- **`EnsureCanLinkCasesAsync` N+1 per case id** (`TravelClaimService.cs`) — small caseIds arrays in v1; batch query later.

- **TravelClaim integration tests require Docker/Testcontainers** — run `dotnet test tests/api.integration --filter TravelClaim` locally before release.

- **No DB constraint that `travel_claim_cases.organisation_id` matches linked case org** — API always sets from actor context; integrity constraint in organisations hardening story.

- **`claimDate` datetime-with-offset can shift UTC calendar day** — clients send date-only JSON in practice; strict date-only parsing optional follow-up.

## Deferred from: code review of 6-2-mobile-travel-claim-capture.md (2026-06-20)

- **`void flushOfflineQueue()` in `listMine` races with queue read** (`TravelClaimApiService.ts`) — matches existing visit pattern from Story 3.6; await flush in a hardening pass if duplicate local rows after sync become user-visible.

## Deferred from: code review of 6-3-director-claim-approval-on-web.md (2026-06-20)

- **Crisis queue N+1 queries per claim row** (`CrisisQueueService.cs`) — attachment count + case link queried in loop; optimize when pending volume grows (Story 8.1/8.2).

- **Concurrent approve/return race without row version** (`TravelClaimService.cs`) — same optimistic-concurrency gap as other status transitions; defer to hardening pass.

- **Approve/Return inline forms vs MatDialog** (`travel-claim-review.component.html`) — AC7 wording says confirm dialog; inline expand forms acceptable for v1 slice.

- **Receipt thumbnails not implemented** (`travel-claim-review.component.html`) — AC7 mentions thumbnails/names; names-only buttons shipped; preview polish follow-up.

## Deferred from: code review of 6-4-claim-status-notifications.md (2026-06-20)

- **No integration test asserting push defer log line** (`TravelClaimService.cs`) — AC6 marks optional; add when test logger harness exists (Story 7.2).

- **`TravelClaimDirectorApiTests` return body uses Contains-only assertion** — full body shape covered by `TravelClaimNotificationApiTests`; director tests left minimal.

## Deferred from: code review of 7-1-device-token-registration-and-notification-store.md (2026-06-20)

- **Integration tests not executed in dev session (Docker/Testcontainers unavailable)** — run `dotnet test tests/api.integration --filter DeviceRegistration` before marking done.

- **`RemoveAllForUserAsync` unused after inline delete in `UserSessionService`** — dead code on `UserDeviceService`; harmless for v1; remove in cleanup pass.

## Deferred from: code review of 7-2-push-delivery-service.md (2026-06-20)

- **Integration tests (`PushDeliveryApiTests`, notification regression) not executed in dev session** — Docker/Testcontainers unavailable; run before marking done.

- **Visit push producer still absent** — explicit story scope; overdue visit job deferred to future story per README.

## Deferred from: code review of 7-3-email-notification-templates.md (2026-06-20)

- **ShouldSendToUser uses role-default preferences, not per-user DB preferences** — per-user preferences persistence is out of scope (story spec: "no preferences PATCH persistence")
- **Pre-save court email fail-fast skips remaining recipients on coordinator N failure; on retry coordinators 0..N-1 receive duplicates** — spec-intended behavior per AC6 (email-before-save contract)
- **EmailDeliveryException lacks `[Serializable]` attribute** — .NET 8 relaxed this requirement
- **No HTML email body / clickable link in footer** — plain-text only per AC2
- **TOCTOU window: claimant email fetched after SaveChangesAsync** — inherent race, TrySend* swallows exceptions
- **SendToDeskStaffAsync 4-param overload creates isolated HashSet** — not a current bug (all callers share the set correctly)

## Deferred from: code review of 7-4-in-app-notification-bell-web-and-mobile.md (2026-06-20)

- **Wrong module placement — UnreadCountDto/getUnreadCount in cases module** — Pre-existing pattern: NotificationDto and NotificationListResultDto are also in case.models.ts. Not actionable in this story.
- **No pagination on notification list** — all notifications fetched at once. Out of scope; existing notification list has no pagination DTO. Future story consideration.
- **Mobile 60s poll battery drain** — accepted trade-off per "no real-time" design constraint.
- **Mobile hand-rolled type definitions drift hazard** — pre-existing project-wide pattern (all mobile types are hand-rolled). Not specific to this change.
- **No caching/rate-limiting on unread-count endpoint** — pre-existing; none of the notification endpoints use caching.

## Deferred from: code review of 8-1-crisis-queue-prioritized-api.md (2026-06-20)

- **Cache invalidation never called from mutation endpoints** — documented in Dev Notes as future work; outside story scope.
- **PreviousWorkerName stores Email not display name** — pre-existing (`User` entity lacks `Name` field); acknowledged in Dev Notes.
- **Cross-org data leak from unvalidated org claim** — pre-existing project-wide pattern; not introduced by this change.
- **Handoff query returns all past assignments within 7 days** — spec doesn't restrict to latest assignment only.
- **Court-warning uses Notes field as prep proxy** — spec-compliant per AC 3.
- **No deduplication of cases across row types** — by design; same case can have multiple crisis events.
- **Overloaded ScheduledAtUtc semantics** — GetDeadline handles type-specific fields correctly.
- **Assert.Single fragility from shared DB state** — pre-existing integration test pattern.

## Deferred from: code review of 8-2-crisis-queue-web-ui.md (2026-06-20)

- **Session expiry silently swallowed by auto-refresh** (`crisis-queue-page.component.ts:66-67`) — pre-existing API error handling pattern in CrisisQueueApiService; auto-refresh error suppression by design per story spec.
- **Refresh interval drift when API is slow** (`crisis-queue-page.component.ts:58-61`) — inherent to `setInterval`; switching to recursive `setTimeout` is out of story scope.
- **"supervisor" terminology in default subtitle** (`crisis-queue-page.component.ts:32`) — cosmetic only; matches existing UX patterns across the app.
- **Misleading "Invalid email or password" on 401 from crisis queue** (`crisis-queue-api.service.ts`, `auth-session.service.ts`) — pre-existing in auth-session.service.ts error mapping, not introduced by this story.
- **Skeleton flash on retry** (`crisis-queue-page.component.ts:46-56`) — acceptable UX behavior for brief loading transitions; skeleton replaces list momentarily on retry click.
- **No explicit 200% zoom CSS handling** — flexbox layout handles reflow naturally; no horizontal scroll observed.

## Deferred from: code review of 8-3-dashboard-api-and-redis-cached-widgets (2026-06-20)

- **InvalidateCacheAsync never wired** — Story 8.4 (Dashboard Web UI) will wire it into mutation endpoints; out of scope for this API-only story
- **WorkerName populated with user.Email** — consistent with CrisisQueueService pattern; a proper Name/FullName field would require User entity schema change
- **No empty-dashboard (AC12) integration test** — non-trivial test setup requiring a separate organisation; can be addressed in a follow-up

## Deferred from: code review of 8-6-reports-web-ui-with-export-progress.md (2026-06-20)

- **Polling never adds new jobs from other sessions** — `refreshActiveJobs` only patches existing jobs by ID, never inserts new jobs created by other users/sessions
- **`totalCount` pagination issues during polling** — polling fetches page 1 only; totalCount not refreshed during poll cycles
- **Single `exporting` boolean prevents concurrent exports** — user cannot start a second export while any request is in-flight
- **`getStatusLabel` is a pure function inside component class** — should be standalone for better tree-shaking and OnPush compatibility
- **`formatTimestamp` hardcodes `en-IN` locale** — should use Angular `LOCALE_ID` injection token instead
- **`window.open` URL unsanitized / popup blocker risk** — URLs from API; acceptable for first iteration
- **In-flight HTTP not cancelled on destroy** — `ngOnDestroy` only clears poll timer, not active HTTP requests
- **`startExport` URL vulnerable to path traversal via `type`** — service has no validation; constrained by component
- **`loadingGuard` mutation after component destruction** — plain boolean field mutated in `finally` after destroy
- **Page change silently desyncs on rapid click** — `loadingGuard` prevents re-fetch after page state already updated
- **`startExport` makes fragile API ordering assumptions** — prepends job and increments count unconditionally
- **`displayFormat()` mislabels unknown formats as "PDF"** — format field is `string`, not union type

## Deferred from: code review of 9-2-staff-directory-management.md (2026-06-20)

- **No pagination on List endpoint** — valid scale concern, not in ACs; add when needed
- **API accepts Director role but UI excludes it** — deliberate design choice; Directors manage staff, not created by staff management
- **No invite/password-set flow on create** — requires email integration beyond this story's scope
- **Rate limiting on force-reset** — potential DoS vector; security hardening story
- **Role change increments TokenVersion (logout)** — intentional per spec; role changes treated as security events
- **No explicit ForcePasswordReset DB column** — implicit via PasswordHash="" + TokenVersion=1 per spec decision
- **Test coverage** — test creation deferred for a testing-specific story
- **Audit events lack IP/user-agent** — cross-cutting concern affecting all controllers equally

## Deferred from: code review of 9-3-audit-log-api-and-director-ui.md (2026-06-20)

- **ResolveOrganisationId() throws 500 instead of 401/403** — pre-existing pattern across all controllers, not fixable in isolation
- **Skip/Take pagination O(n) on append-only table** — keyset pagination is a future performance story
- **ActorUserId/SubjectUserId filters lack dedicated DB indexes** — performance optimization, valid but not in current scope
- **AuditApiService re-throws raw error** — component's extractErrorMessage handles HttpErrorResponse correctly
- **AuditEventDto 10-parameter positional record fragility** — matches project-wide record pattern; refactor to named properties deferred
- **ResolveRequestId() fallback to TraceIdentifier inconsistent** — pre-existing concern across all controllers
- **Audit log access not itself audited** — read-only endpoint per spec; not an AC requirement
- **Missing actorUserId/subjectUserId UI filter controls** — API supports them; UI needs a user-search infrastructure first

## Deferred from: code review of 9-4-angular-material-theme-from-design-tokens.md (2026-06-20)

- **F1: `IsUniqueConstraintViolation` uses DB-provider-specific string matching** — `IsUniqueConstraintViolation` in `LegendsController` matches on `"UNIQUE"` / `"duplicate"` only; provider-specific check; pre-existing
- **F2: LegendsController uses raw reflection in every endpoint** — `EF.Property<Guid>(e, "OrganisationId")` and `type.GetProperty("Name")!.SetValue(...)` in every endpoint; pre-existing design choice
- **F3: 10 identical entity classes + configs copy-paste debt** — every legend entity is an exact structural copy; shared base class would eliminate; pre-existing pattern
- **F4: ReportGenerationService client-side `.ToString()` evaluation** — EF expression tree → anonymous type + in-memory Select forces client-side evaluation; from prior EF Core fix session
- **F5: LegendsController file minified to single line** — 438-character single line; unreviewable, unblameable; pre-existing formatting
- **F6: Claims resolution throws 500 instead of 401** — `ResolveOrganisationId()` / `ResolveUserId()` throw `InvalidOperationException` on bad claims; pre-existing pattern across all controllers
- **F7: Legend entities lack concurrency tokens** — no row version or `UPDATE ... WHERE UpdatedAtUtc` pattern; low write contention acceptable

## Deferred from: code review of 11-1-gender-family-type-economic-status-on-case.md (2026-06-21)

- **Missing DB indexes on Gender, FamilyType, EconomicStatus** — `ApplySearchFilters` adds exact-match WHERE clauses for these columns. Without indexes, filtering by these fields degrades to sequential scans on large tables. Deferred: performance optimization, not a correctness bug for current data volumes.

- **PII redaction gap: demographic fields not redacted for field workers on POCSO cases** — BeneficiaryName is redacted via BeneficiaryDisplayFormatter for field workers on POCSO cases, but Gender, FamilyType, and EconomicStatus are written unconditionally. Potential privacy policy violation. Deferred: pre-existing POCSO redaction concern, not introduced by this story.

- **Inconsistent null-handling patterns** — Some paths use ternary (`c.Gender != null ? c.Gender.ToString()! : null`) while others use null-conditional (`entity.Gender?.ToString()`). Maintenance hazard for future extensions. Deferred: low severity, both produce correct results.

## Deferred from: code review of 11-2-occupation-and-education-level-on-case.md (2026-06-21)

- **TOCTOU race in legend reference validation** — ValidateIntakeRequestAsync checks IsActive for Occupation/EducationLevel, then SaveChangesAsync commits in a separate implicit transaction. Between check and commit, another session could deactivate the referenced row. Pre-existing across the codebase.
- **Brittle hard-coded Excel column indices** — Cell writes in CaseExcelExporter use magic numbers (11, 12, 13, 14, 15) that must be manually kept in sync with the headers array. A mismatch compiles silently. Pre-existing pattern; global refactor needed.
- **Two different name-resolution strategies (search vs export)** — Search uses explicit ToDictionaryAsync + manual loop, export uses navigation property access directly in Select. Intentional optimization choice.
- **No support for null search filters** — Cannot search for cases where OccupationId is explicitly unset/null. Feature request; not introduced by this story.
- **Duplicated LoadAsync blocks in CreateAsync and MergeAsync** — Identical 7-line navigation-loading block copy-pasted. Minor DRY concern; acceptable for now.

## Deferred from: code review of 12-2-stage-3-inter-sectoral-approach-support-tracking.md (2026-06-21)

- **`DbUpdateException` mapped to 409 without concurrency token** — Adding proper concurrency token/row version is a larger design decision beyond this story's scope. The atomic-replace pattern in `CaseStage3DataService.UpsertAsync` can race under concurrent requests.
- **No index on `OrganisationId` on `case_stage3_supports`** — Pre-existing pattern; not unique to this story. The `OrganisationId` column has no dedicated index.
- **Missing `OrganisationId` FK constraint** — Organisation entity/table doesn't exist yet. Deferred per project-wide pattern ("organisations schema is a later epic"). `CaseStage3SupportConfiguration.cs`

## Deferred from: code review of 11-3-recidivism-and-family-history-tracking.md (2026-06-21)

- **Hardcoded Excel column indices extended from 15 to 18 columns** — Cell writes in CaseExcelExporter use magic numbers (13, 14, 15, 16, 17, 18) that must be manually kept in sync with headers array. Pre-existing pattern; global refactor needed.
- **No DB-level CHECK constraint for non-negative recidivism** — Application-level validation in ValidateIntakeRequestAsync, but no database CHECK constraint. Pre-existing pattern across codebase.
- **No support for null search filters** — Cannot search for cases where OccupationId/EducationLevelId is explicitly unset. Pre-existing limitation noted in previous review.

## Deferred from: code review of 12-1-stage-2-maintain-and-development-sub-step-data.md (2026-06-21)

- **Concurrency unsafety — race condition on upsert** — Read-then-write pattern with no row version or concurrency token. Two concurrent requests can both see `existing is null`. Pre-existing pattern across the codebase.
- **`ValidateFieldLengths` repetitive maintenance liability** — 8 fields checked with copy-paste manual code. Adding a new text field requires edits in 5 places: entity, DTO, request DTO, EF configuration, service validation. Pre-existing pattern.
- **No input sanitization on text fields** — All 8 text fields accept arbitrary string content with no character restrictions, XSS prevention, or structural validation. Cross-cutting concern affecting the entire API.
- **Search filters accept inactive legend IDs but intake endpoint rejects them** — `ApplySearchFilters` doesn't filter by `IsActive`, while `ValidateIntakeRequestAsync` checks `o.IsActive` and `el.IsActive`. Behavioral inconsistency that could confuse users.
- **Duplicate DTO-mapping surface area across 5+ locations** — Same new fields (OccupationId, OccupationName, EducationLevelId, EducationLevelName, FamilyHistoryOfCrime, RecidivismBeforeCount, RecidivismAfterCount) mapped in `CaseDtoMapper`, `ToDto`, `BuildDetailDtoAsync`, `SearchCasesAsync` projection, and `ExportAsync` projection. Adding or renaming a field requires edits in all five places.

## Deferred from: code review of 12-3-stage-4-rehabilitation-placement-records.md (2026-06-21)

- **Organisation-scoped read guard** — `GetAsync` doesn't verify the returned placement's `OrganisationId` matches the caller's organisation. Cross-cutting concern; Organisation entity doesn't exist yet.
- **No concurrency control on placement entity** — Read-then-write without row versioning. Pre-existing pattern across the codebase.

## Deferred from: code review of 12-4-stage-5-reintegration-records.md (2026-06-21)

- **No `None`/`Unset` sentinel in `ReintegrationLevel` enum** — pre-existing design pattern matching `PlacementType`, `SupportType`.
- **Missing `OrganisationId` FK constraint** — Organisation entity/table doesn't exist yet (project-wide deferral: "organisations schema is a later epic").
- **`HasOne<User>()` FK assumption without verifying `User` entity mapping** — pre-existing pattern used across all stages.
- **Audit events record only `caseId` and `actorUserId` metadata, not value deltas** — pre-existing pattern across all stage services.
- **`JsonSerializerDefaults.Web` choice on dictionary serialization** — functionally correct but stylistically questionable; pre-existing pattern.
- **`DbUpdateException` catch → 409 too broad** (FK violations, deadlocks all mapped as "conflict") — pre-existing pattern in all previous stage controllers.
- **No `RowVersion` concurrency token** — pre-existing pattern matching Stage 2 and Stage 4 (only Stage 3 was patched to add one).
- **`OrganisationId` denormalized without cross-reference check against `Case.OrganisationId`** — pre-existing pattern across all stage entities.
- **Integer enum values (e.g., `"0"`, `"1"`) bypass string validation via `Enum.TryParse`** — pre-existing behavior across all stage services.
- **`MaxInstitutionDetailsLength` constant (2000) duplicated across service, DTO annotation, and EF config** — pre-existing pattern.
- **`RequestSizeLimit(16_384)` without `413 PayloadTooLarge` in `ProducesResponseType`** — pre-existing pattern in all stage controllers.
- **`WithMany()` without inverse navigation property on `Case`** — pre-existing pattern matching all previous stage configurations.
- **TOCTOU race: stage check to `SaveChangesAsync` window** — case could transition out of Stage 5 between verification and persistence; pre-existing pattern across all stage services.
- **`[ApiController]` auto-400 short-circuits before service validation can produce the AC-required 422** — pre-existing pattern affecting all stage controllers.

## Deferred from: code review of 12-5-stage-6-termination-and-exclusion-records.md (2026-06-21)

- **No `OrganisationId` FK constraint** — Organisation entity/table doesn't exist yet (project-wide deferral).
- **No `RowVersion` concurrency token** — pre-existing pattern matching Stage 2/4/5.
- **Audit events record only `caseId` and `actorUserId` metadata, not value deltas** — pre-existing pattern across all stage services.
- **`DbUpdateException` catch → 409 too broad** (FK violations, deadlocks all mapped as "conflict") — pre-existing pattern in all stage controllers.
- **401 vs 403 semantics conflated** — `InvalidOperationException` from claim resolution mapped to 401 even for role failures; pre-existing pattern.
- **`[ApiController]` auto-400 short-circuits before service validation can produce the AC-required 422** for field length violations — pre-existing pattern.
- **TOCTOU race: stage check to `SaveChangesAsync` window** — case could transition out of Stage 6 between verification and persistence; pre-existing pattern.
- **`MaxTextFieldLength` constant (2000) duplicated across service, DTO annotation, and EF config** — pre-existing pattern.
- **No input trimming on `JjbDetails`/`ExclusionReason` string fields** — pre-existing pattern matching all stage services.
- **`Enum.ToString()` on response path is brittle** — renaming enum members breaks clients; pre-existing pattern.
- **`OperationCanceledException` catch rethrows immediately** — intentionally prevents other catch blocks from swallowing it; accepted pattern from Story 12.4.
- **Integer enum values (e.g., `"0"`, `"1"`) bypass string validation via `Enum.TryParse`** — pre-existing behavior across all stage services.

## Deferred from: code review of 13-1-related-cases-data-model-and-api.md (2026-06-21)

- **`[Required]` on non-nullable `Guid` struct** — dead metadata on value type; pre-existing project-wide pattern.
- **LINQ ternary in EF Core join** — EF Core 8 translates this correctly; no actionable issue.
- **`InvalidOperationException` used for auth failures (wide catch trap)** — pre-existing pattern across all services.
- **`IHttpContextAccessor` coupling in service** — pre-existing architectural pattern across the codebase.
- **Manual dictionary-based audit event construction** — pre-existing pattern across all service classes.
- **Enum-as-string at API boundary** — pre-existing project convention matching all stage services.
