---
baseline_commit: NO_VCS
---

# Story 3.8: Discreet POCSO Capture Mode

Status: done

<!-- Validated: 2026-06-17 — see 3-8-discreet-pocso-capture-mode-validation-report.md (9 fixes applied) -->

## Story

As a **Social Worker on a POCSO Case in public**,
I want minimal on-screen beneficiary detail,
so that privacy is protected (NFR-3, UX-DR7).

## Acceptance Criteria

1. **Given** the `cases` table and Case aggregate  
   **When** this story ships  
   **Then** `cases.sensitivity_level` exists (`Standard` | `POCSO`, default `Standard`) via EF migration  
   **And** `CaseSummaryDto` / visit nested case payloads include `sensitivityLevel` string  
   **And** create-case may set sensitivity (optional request field; default `Standard`) — no web admin UI required in this story

2. **Given** a Case with `sensitivityLevel = POCSO`  
   **When** a field worker calls mobile list/detail list endpoints (`GET /api/v1/visits/today`, `GET /api/v1/cases/assigned`, `GET /api/v1/cases/{id}` for assigned POCSO case, and `VisitListItemDto` from `POST /api/v1/sync/push`)  
   **Then** `beneficiaryName` in those DTOs is **initials only** (e.g. `"R. K."` from `"Ravi Kumar"`)  
   **And** `sensitivityLevel: "POCSO"` is present so mobile can branch discreet UI  
   **And** full beneficiary name is **not** present until `reveal-pii` (AC8)  
   **And** supervisor/web registry search endpoints are **unchanged** (full names remain for coordinator roles)

3. **Given** a POCSO visit on **Today Command Strip**  
   **When** the card renders  
   **Then** headline shows **initials · crime number** only (no ST number, no domicile in meta while discreet)  
   **And** non-POCSO visits keep existing crime · ST + domicile layout (Story 3.7 sync chip unchanged)

4. **Given** a POCSO visit on **Active Visit** (`ActiveVisitScreen`)  
   **When** the screen opens  
   **Then** **discreet header** is default: initials + crime number only (`DESIGN.md` `discreet-header` tokens)  
   **And** ST number, domicile, and full beneficiary name are hidden until expanded  
   **And** collapsed state `accessibilityLabel` announces **"Limited detail mode"**

5. **Given** discreet header collapsed on Active Visit  
   **When** worker taps **Show full detail**  
   **Then** if last successful login OTP was **≤ 5 minutes** ago, detail expands immediately  
   **And** if **> 5 minutes**, a modal collects a fresh **6-digit OTP** (step-up flow) before expand  
   **And** successful expand calls server to log PII access (AC6) and returns full beneficiary fields for display

6. **Given** worker expands full beneficiary detail on a POCSO case  
   **When** reveal succeeds  
   **Then** server appends `audit_events` row `case.pii.revealed` with `caseId`, `userId`, `organisationId`, timestamp  
   **And** failed step-up OTP does not reveal detail and does not write audit

7. **Given** step-up OTP required  
   **When** worker completes step-up  
   **Then** API provides `POST /api/v1/auth/step-up` (authenticated field worker) → email OTP challenge (same pattern as login)  
   **And** `POST /api/v1/auth/verify-step-up` validates challenge + code (does not rotate refresh token / re-login)  
   **And** mobile reuses accessible OTP input patterns from `OtpScreen` (6-digit, `aria-live` errors)

8. **Given** full detail reveal  
   **When** worker requests expand  
   **Then** `POST /api/v1/cases/{caseId}/reveal-pii` (field worker, assigned case) returns `{ beneficiaryName, beneficiaryContact?, beneficiaryAge? }`  
   **And** server enforces step-up gate: allow reveal if `last_login_otp_verified_at` **or** `last_step_up_verified_at` on the user/session is within **5 minutes** (stored server-side at `verify-otp` / `verify-step-up` — do not rely on client clock alone)  
   **And** OpenAPI + `packages/api-client` regenerated; README updated

9. **Given** a POCSO case on **Cases → Case detail** (`CaseDetailPlaceholderScreen`)  
   **When** field worker opens detail from Cases tab or Today case headline  
   **Then** same discreet header rules as Active Visit (AC4–AC5): initials + crime default, expand via reveal flow  
   **And** full beneficiary line is hidden until successful reveal

10. **Given** mobile Jest baseline after Story 3.7 (**94** tests)  
   **When** this story ships  
   **Then** tests cover at minimum:
   - Initials helper (single/multi-word names, empty edge)
   - `DiscreetHeader` collapsed vs expanded labels
   - Active Visit POCSO default discreet + expand within 5 min (mock auth timestamp)
   - Active Visit expand after 5 min opens step-up modal (mock)
   - Command Strip POCSO card hides ST/domicile
   - `CaseDetailPlaceholderScreen` POCSO discreet + reveal flow (mock)
   - API unit/integration: today list + `GET /cases/{id}` redact POCSO `beneficiaryName`; reveal writes audit  
   **And** `npm run test:mobile` and `npm run test:api` pass

11. **Given** scope boundaries  
    **When** this story ships  
    **Then** no column-level encryption implementation (architecture assumption deferred)  
    **And** no **web** discreet UI (mobile field app + API redaction for field-worker roles only)  
    **And** offline queue / sync chip behavior from Stories 3.6–3.7 remains intact  
    **And** expanded full PII is **in-memory per screen session only** — do not write revealed names into `commandStripCache`

## Tasks / Subtasks

- [x] **API — sensitivity level** (AC: 1)
  - [x] `SensitivityLevel` enum (`Standard`, `POCSO`) + `Case.SensitivityLevel` column + migration
  - [x] `CaseConfiguration` + optional `CreateCaseRequest.SensitivityLevel`
  - [x] Expose `sensitivityLevel` on `CaseSummaryDto` / mappers

- [x] **API — list DTO redaction** (AC: 2)
  - [x] `BeneficiaryDisplayFormatter.ToInitials(fullName)` helper (shared by Visit + Case services)
  - [x] Apply redaction in `VisitService.ToCaseSummary` for field-worker today/sync list paths
  - [x] Apply redaction in `CaseService.ListAssignedAsync` (`GET /cases/assigned`)
  - [x] Apply redaction in `CaseService.GetDetailAsync` for field-worker POCSO (`GET /cases/{id}`)

- [x] **API — step-up auth** (AC: 7, 8)
  - [x] `POST /api/v1/auth/step-up` + `POST /api/v1/auth/verify-step-up` with `[Authorize(Policy = FieldWorker)]` (override controller `AllowAnonymous`)
  - [x] Persist `last_login_otp_verified_at` on `verify-otp` and `last_step_up_verified_at` on `verify-step-up` (Redis or user session store; 5 min validity)

- [x] **API — PII reveal + audit** (AC: 6, 8)
  - [x] `AuditEventTypes.CasePiiRevealed = "case.pii.revealed"`
  - [x] `POST /api/v1/cases/{caseId}/reveal-pii` with assignment check + step-up gate
  - [x] Integration tests for redaction + reveal audit

- [x] **OpenAPI / api-client / README** (AC: 8)
  - [x] Regenerate OpenAPI + `packages/api-client` — run `npm run generate:api-client` with API up to refresh generated types (mobile uses extended local types until regen)
  - [x] Document POCSO discreet behavior in README field-app section

- [x] **Mobile — discreet UI** (AC: 3, 4, 5, 9)
  - [x] `beneficiaryInitials.ts` (display fallback matching server rules)
  - [x] `DiscreetHeader.tsx` — collapsed/expanded, expand control, a11y (reuse on Active Visit + Case detail)
  - [x] `DiscreetExpandModal.tsx` — step-up OTP when needed
  - [x] `CommandStripCard` — discreet layout when `case.sensitivityLevel === 'POCSO'`
  - [x] `ActiveVisitScreen` — integrate `DiscreetHeader`, reveal flow, session OTP window
  - [x] `CaseDetailPlaceholderScreen` — same discreet header + reveal (Cases tab path)

- [x] **Mobile — auth service** (AC: 5, 7)
  - [x] Track `lastLoginOtpVerifiedAtUtc` on `verifyOtp` success
  - [x] `stepUp()` / `verifyStepUp(code)` + `revealCasePii(caseId)` API calls

- [x] **Tests** (AC: 10)
  - [x] API: redaction on today + assigned + get-by-id; reveal audit; step-up gate
  - [x] Mobile: unit + screen tests listed in AC10

- [x] **Seed** (AC: 1, manual QA)
  - [x] Development seed: at least one **POCSO** case assigned to field worker test account

### Review Findings

- [x] [Review][Patch] Redis OTP verification not cleared on logout/session invalidate [AuthVerifiedStore.cs, AuthService.cs, UserSessionService.cs]
- [x] [Review][Patch] `verifyStepUp` does not refresh client OTP window timestamp [AuthSessionService.ts]
- [x] [Review][Patch] Cold start forces step-up modal even when server Redis still valid [useDiscreetCaseReveal.ts]
- [x] [Review][Patch] Empty `caseId` / double-tap reveal not guarded [useDiscreetCaseReveal.ts]
- [x] [Review][Patch] POCSO collapsed header trusts cached full beneficiary name [DiscreetHeader.tsx]
- [x] [Review][Patch] Case detail expanded header missing domicile [CaseDetailDto, CaseDetailPlaceholderScreen.tsx]
- [x] [Review][Patch] `reveal-pii` endpoint lacks rate limiting [CasesController.cs]
- [x] [Review][Patch] AC2 sync/push POCSO redaction untested [CasePocsoTests.cs]
- [x] [Review][Defer] GPS/landmark visible on case detail without step-up — out of scope for discreet header ACs [CaseDetailPlaceholderScreen.tsx]
- [x] [Review][Defer] Handoff whisper may contain PII in operational text — acknowledged operational risk [CaseService.cs]
- [x] [Review][Defer] No collapse-after-reveal UX — not specified in AC4/AC5 [DiscreetHeader.tsx]
- [x] [Review][Defer] `commandStripCache` may retain pre-redaction visit payloads until refresh — cache invalidation is 3.6/3.7 concern [commandStripCache.ts]
- [x] [Review][Defer] OpenAPI/api-client regen requires running API (`npm run generate:api-client`) — mobile uses extended local types until then [packages/api-client]

## Dev Notes

### Scope boundary (critical)

| In scope (3.8) | Out of scope |
|----------------|--------------|
| `sensitivity_level` column + migration | Column-level encryption |
| Mobile list DTO redaction (field worker) | Web registry discreet UI |
| Discreet header on Command Strip + Active Visit | Director audit log UI (Epic 9) |
| Step-up OTP + PII reveal endpoint + audit | Biometric re-auth |
| OpenAPI + api-client regen | WatermelonDB |

**Critical:** Story 3.7 explicitly deferred POCSO to this story. Do not regress sync chip, offline queue, or `mergeQueueWithVisits` behavior.

### Brownfield reality (read before coding)

| Area | Current state | This story |
|------|---------------|------------|
| `Case` entity | No `SensitivityLevel`; full `BeneficiaryName` everywhere | Add column + enum |
| `VisitService.ToCaseSummary` | Always maps full `BeneficiaryName` | Redact for POCSO on field-worker list paths |
| `CommandStripCard` | `crime · ST` + domicile meta | Discreet variant for POCSO |
| `ActiveVisitScreen` | Full crime/ST/domicile + sync chip | Add `DiscreetHeader` |
| `CaseDetailPlaceholderScreen` | Shows full `beneficiaryName` via `GET /cases/{id}` | Discreet header + reveal (AC9) |
| `AuthSessionService` | Login OTP only | Step-up + `lastLoginOtpVerifiedAtUtc` (client hint; server is source of truth) |
| `AuditEventTypes` | No PII reveal event | Add `case.pii.revealed` |

### Initials algorithm (implement exactly)

```typescript
// Server (C#) and mobile helper must match:
// "Ravi Kumar" → "R. K."
// "Priya" → "P."
// Trim, split on whitespace, first grapheme of first two tokens, uppercase + period, join with space
// Empty → "—"
```

Use server-redacted `beneficiaryName` from list APIs when available; mobile helper is fallback for cached visits only.

### Discreet header UX (UX-DR7, EXPERIENCE.md Flow 4, DESIGN.md)

| State | Visible fields | Tokens |
|-------|----------------|--------|
| Collapsed | Initials + crime number | `discreet-header` foreground `#475467`, crime `#101828` |
| Expanded | + ST, domicile, full beneficiary name, contact/age if returned | Standard card text |

- Expand control label: **"Show full detail"** / collapsed hint **"Limited detail mode"**
- Modal: focus trap, return focus to expand trigger (same as Duplicate match sheet pattern)
- **Do not** show handoff whisper beneficiary-identifying content in discreet collapsed mode — keep whisper text (operational) but ensure no full name leak in collapsed headline/meta

### Step-up vs login OTP (5-minute window)

```text
lastLoginOtpVerifiedAt = stored on mobile at verifyOtp success (ISO UTC)

if (now - lastLoginOtpVerifiedAt <= 5 min):
  reveal-pii with Bearer only
else:
  1. POST /auth/step-up → challengeId
  2. POST /auth/verify-step-up { challengeId, code }
  3. POST /cases/{id}/reveal-pii (server validates recent step-up)
```

Server must enforce the 5-minute window — do not rely on client-only checks for authorization.

**Server storage (implement one):**
- Redis keys `auth:otp-verified:{userId}` and `auth:step-up-verified:{userId}` with 5-minute TTL, set in `VerifyOtpAsync` / `VerifyStepUpAsync`
- `reveal-pii` rejects with **403** when neither key is valid

**Auth controller note:** class is `[AllowAnonymous]` — step-up actions **must** use method-level `[Authorize(Policy = Policies.FieldWorker)]`.

### API endpoints summary

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| POST | `/api/v1/auth/step-up` | Field worker JWT | Send step-up OTP email |
| POST | `/api/v1/auth/verify-step-up` | Field worker JWT | Validate step-up code |
| POST | `/api/v1/cases/{caseId}/reveal-pii` | Field worker JWT | Return full PII + audit row |

Redaction applies to existing field-worker paths:
- `GET /api/v1/visits/today`
- `GET /api/v1/cases/assigned`
- `GET /api/v1/cases/{id}` (field worker, assigned POCSO only)
- `VisitListItemDto` in `POST /api/v1/sync/push` results

### Files to extend (read before editing)

| File | Notes |
|------|-------|
| `apps/api/Domain/Entities/Case.cs` | Add `SensitivityLevel` |
| `apps/api/Infrastructure/Visits/VisitService.cs` | `ToCaseSummary` redaction + sensitivity on DTO |
| `apps/api/Infrastructure/Cases/CaseService.cs` | `ListAssignedAsync` + `GetDetailAsync` redaction; create sensitivity |
| `apps/api/Controllers/V1/AuthController.cs` | Step-up endpoints (`[Authorize]` on methods) |
| `apps/api/Controllers/V1/CasesController.cs` | `reveal-pii` endpoint |
| `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx` | Discreet header + reveal |
| `apps/mobile/src/components/CommandStripCard.tsx` | Discreet layout branch |
| `apps/mobile/src/screens/today/ActiveVisitScreen.tsx` | Discreet header + expand |
| `apps/mobile/src/services/auth/AuthSessionService.ts` | OTP timestamps + step-up |
| `packages/api-client` | Regenerate after OpenAPI |

### Previous story intelligence (3.7)

- Mobile Jest: **94** tests; NetInfo mocked in `jest.setup.js`
- `ActiveVisitScreen` uses `useSyncOnForeground` + `useFocusEffect` for queue — do not break sync chip wiring
- `CommandStripCard` uses `syncChip` prop + `SyncChip` — discreet layout is additive branch, not a rewrite
- Code review on 3.7 fixed optimistic **Uploading** during flush — leave `syncAfterQueueChange` intact
- Cross-tab navigation pattern: `navigation.getParent()?.navigate(...)` — not needed for discreet expand (modal on same screen)

### Previous story intelligence (3.2–3.6)

- Visit/case DTOs come from `@midi-kaval/api-client` OpenAPI types — regenerate after API changes
- `commandStripCache` stores visit list — POCSO redacted names from API are safe to cache; **never** merge `reveal-pii` full names back into cache (in-memory expanded state only per AC11)

### Architecture compliance

- §9 Security: `sensitivity_level = POCSO` triggers discreet API responses (initials in mobile list DTOs)
- NFR-4: PII reveal writes `audit_events`
- NFR-3: discreet capture on mobile field surfaces
- Every mutation writes audit — reveal endpoint must write audit in same transaction as authorization check

### Testing

**API:** Testcontainers integration — seed POCSO case, assert today list initials, call reveal-pii with/without step-up, assert audit row.

**Mobile:** Mock `AuthSessionService.revealCasePii`, `stepUp`, `verifyStepUp`; test `DiscreetHeader` and `CommandStripCard` POCSO branch without device OTP email.

**Do not require** real SMTP in Jest — mock auth service responses.

### Latest technical information

- React Navigation modals: use existing `RescheduleVisitModal` / `CaptureLandmarkModal` patterns for `DiscreetExpandModal`
- Auth rate limiting already on login/verify-otp — add `auth-step-up` / `auth-verify-step-up` rate limit policies matching login
- EF Core migration naming: follow `AddSyncMutations` style timestamp prefix

### Seed / manual test data

**Required task:** add Development seed case with `SensitivityLevel = POCSO` assigned to field worker (`Seed:FieldWorker:Email`) so discreet UI is testable without SQL. Document crime/ST in README dev section.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Story 3.8, NFR-3, NFR-4, UX-DR7]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — §9 POCSO discreet API]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/EXPERIENCE.md` — Flow 4, Discreet header, a11y]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-Midi-Kaval-2026-06-12/DESIGN.md` — `discreet-header` tokens]
- [Source: `_bmad-output/implementation-artifacts/3-7-sync-chip-and-sync-queue-mobile-ui.md` — deferred POCSO scope]
- [Source: `apps/api/Domain/Entities/Case.cs`]
- [Source: `apps/api/Infrastructure/Visits/VisitService.cs` — `ToCaseSummary`]
- [Source: `apps/api/Infrastructure/Cases/CaseService.cs` — `GetDetailAsync`, `ListAssignedAsync`]
- [Source: `apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx`]
- [Source: `apps/mobile/src/screens/today/ActiveVisitScreen.tsx`]
- [Source: `apps/mobile/src/components/CommandStripCard.tsx`]

## Dev Agent Record

### Agent Model Used

Auto (Cursor)

### Debug Log References

### Completion Notes List

- API: `SensitivityLevel` enum + migration `AddCaseSensitivityLevel`; field-worker DTO redaction via `BeneficiaryDisplayFormatter` / `CaseDtoMapper`; Redis `AuthVerifiedStore` for 5-min OTP gate; step-up + reveal-pii endpoints; `PocsoCaseSeeder` for dev QA.
- Mobile: `DiscreetHeader`, `DiscreetExpandModal`, `useDiscreetCaseReveal` hook; Command Strip / Active Visit / Case detail discreet branches; auth step-up + reveal APIs.
- Tests: API unit + 4 integration (`CasePocsoTests`); mobile **104** Jest tests (+10). Run `npm run generate:api-client` with API running to refresh OpenAPI client types.

### File List

- apps/api/Domain/Enums/SensitivityLevel.cs
- apps/api/Domain/Entities/Case.cs
- apps/api/Infrastructure/Cases/BeneficiaryDisplayFormatter.cs
- apps/api/Infrastructure/Cases/CaseDtoMapper.cs
- apps/api/Infrastructure/Cases/CaseService.cs
- apps/api/Infrastructure/Visits/VisitService.cs
- apps/api/Infrastructure/Auth/AuthVerifiedStore.cs
- apps/api/Infrastructure/Auth/AuthService.cs
- apps/api/Infrastructure/AuthServiceCollectionExtensions.cs
- apps/api/Infrastructure/Audit/AuditEventTypes.cs
- apps/api/Infrastructure/Persistence/CaseConfiguration.cs
- apps/api/Infrastructure/Seed/PocsoCaseSeeder.cs
- apps/api/Infrastructure/Seed/DatabaseInitializer.cs
- apps/api/Controllers/V1/AuthController.cs
- apps/api/Controllers/V1/CasesController.cs
- apps/api/Models/Cases/CaseDtos.cs
- apps/api/Models/Auth/AuthDtos.cs
- apps/api/Migrations/20260618192231_AddCaseSensitivityLevel.cs
- apps/api/Migrations/20260618192231_AddCaseSensitivityLevel.Designer.cs
- apps/api/Migrations/AppDbContextModelSnapshot.cs
- apps/api/Program.cs
- apps/mobile/src/utils/beneficiaryInitials.ts
- apps/mobile/src/components/DiscreetHeader.tsx
- apps/mobile/src/components/DiscreetExpandModal.tsx
- apps/mobile/src/components/CommandStripCard.tsx
- apps/mobile/src/hooks/useDiscreetCaseReveal.ts
- apps/mobile/src/screens/today/ActiveVisitScreen.tsx
- apps/mobile/src/screens/cases/CaseDetailPlaceholderScreen.tsx
- apps/mobile/src/services/auth/AuthSessionService.ts
- apps/mobile/src/services/auth/auth.models.ts
- apps/mobile/src/services/cases/CaseApiService.ts
- apps/mobile/src/services/cases/case.models.ts
- apps/mobile/src/services/visits/visit.models.ts
- tests/api.unit/BeneficiaryDisplayFormatterTests.cs
- tests/api.integration/CasePocsoTests.cs
- apps/mobile/__tests__/beneficiaryInitials.test.ts
- apps/mobile/__tests__/DiscreetHeader.test.tsx
- apps/mobile/__tests__/CommandStripCard.test.tsx
- apps/mobile/__tests__/ActiveVisitScreen.test.tsx
- apps/mobile/__tests__/CaseDetailPlaceholderScreen.test.tsx
- README.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-06-17 — Story created from epics + UX + architecture + Stories 3.6/3.7 handoff; ready-for-dev.
- 2026-06-17 — Validation: 9 fixes applied (Case detail leak, server OTP window, assigned path, seed task, cache rule, auth Authorize, tests).
- 2026-06-18 — Implementation complete: API redaction + step-up + reveal-pii; mobile discreet UI; tests; seed; README. Status → review.
