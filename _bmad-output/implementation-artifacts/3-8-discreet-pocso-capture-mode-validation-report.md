# Story Validation Report — 3.8 Discreet POCSO Capture Mode

**Story:** `3-8-discreet-pocso-capture-mode`  
**Validated:** 2026-06-17  
**Validator:** bmad-create-story (validate / checklist.md)  
**Verdict:** **PASS — ready for dev-story** (9 fixes applied 2026-06-17)

---

## Summary

Story 3.8 correctly targets NFR-3 / UX-DR7 discreet capture for POCSO cases. Scope spans **API redaction + step-up auth + mobile UI** — appropriate for epic AC. Nine gaps could cause **full PII leak via Case detail**, **client-only OTP window bypass**, **wrong assigned endpoint**, or **revealed names persisted in offline cache**.

| Check | Result |
|-------|--------|
| Epic 3.8 AC (discreet header, OTP expand, list initials, audit) | Pass (after fixes) |
| NFR-3 discreet capture | Pass |
| NFR-4 PII audit on reveal | Pass |
| UX-DR7 / EXPERIENCE Flow 4 | Pass |
| Story 3.7 sync/offline non-regression | Pass — called out in AC11 |
| Case detail mobile leak | **Fix** — `GET /cases/{id}` + `CaseDetailPlaceholderScreen` |
| Server 5-minute OTP gate | **Fix** — Redis/session timestamps at verify-otp / verify-step-up |
| Exact `GET /cases/assigned` path | **Fix** |
| commandStripCache PII persistence | **Fix** — in-memory reveal only |
| Dev seed POCSO case | **Fix** — required task |
| Auth `[AllowAnonymous]` controller | **Fix** — method-level `[Authorize]` on step-up |
| Case detail + API tests | **Fix** — AC10 expanded |

---

## Critical Issues (Must Fix) — Applied

### 1. Case detail screen leaks full beneficiary today

`CaseDetailPlaceholderScreen` calls `GET /api/v1/cases/{id}` and renders `Beneficiary: {detail.beneficiaryName}` with no discreet branch. Today → case headline navigates here.

**Fix applied:** AC2 includes `GET /cases/{id}` redaction; AC9 requires `CaseDetailPlaceholderScreen` discreet + reveal; task + brownfield table updated.

### 2. Server OTP window was underspecified

Story relied on client `lastLoginOtpVerifiedAtUtc` without mandating server storage. A malicious client could call `reveal-pii` without recent OTP.

**Fix applied:** AC8 — persist `last_login_otp_verified_at` / `last_step_up_verified_at` server-side (Redis TTL 5 min); `reveal-pii` returns **403** when invalid.

### 3. Assigned cases endpoint path vague

Mobile uses `GET /api/v1/cases/assigned` (`CaseApiService.listAssignedCases`), not generic search.

**Fix applied:** AC2 + API summary use exact path.

---

## Enhancement Opportunities — Applied

### 4. Reveal response must not poison offline cache

Writing full names into `commandStripCache` after reveal would persist PII offline.

**Fix applied:** AC11 — expanded PII in-memory per screen session only; dev notes updated.

### 5. `AuthController` is `[AllowAnonymous]` at class level

Step-up endpoints would be anonymous unless method-level `[Authorize]`.

**Fix applied:** Dev notes + task — `[Authorize(Policy = Policies.FieldWorker)]` on step-up actions.

### 6. Development seed POCSO case

No `SensitivityLevel` column exists yet; manual QA blocked without seed.

**Fix applied:** Required seed task for field worker assigned POCSO case + README note.

### 7. `CaseDetailDto` needs `sensitivityLevel`

Mobile must branch discreet UI without inferring from offence type alone.

**Fix applied:** AC1/AC2 — `sensitivityLevel` on summary/detail DTOs for field-worker responses.

### 8. Test coverage for Case detail path

AC9 listed Active Visit + Command Strip only.

**Fix applied:** AC10 adds `CaseDetailPlaceholderScreen` + `GET /cases/{id}` API tests.

### 9. Rate limit policy names for step-up

**Fix applied:** Latest tech — `auth-step-up` / `auth-verify-step-up` policies aligned with login.

---

## Optimizations (Not Applied — Acceptable for v1)

- **Cases list row** showing beneficiary initials — list currently shows crime/ST only; initials in row optional.
- **Auto-set POCSO from `TypeOfOffence` text** — explicit `sensitivityLevel` on create + seed is sufficient for pilot.
- **Handoff whisper free-text PII** — operational text may contain names; collapsed headline/meta must not leak; full whisper review deferred.

---

## LLM Dev-Agent Notes

- Mobile Jest baseline: **94** tests after Story 3.7.
- OpenAPI + `packages/api-client` **must** regenerate (API + DTO changes).
- Reuse `DiscreetHeader` on **Active Visit** and **Case detail** — single component.
- Do **not** break `syncAfterQueueChange` / `useSyncOnForeground` from 3.7 review patches.
- `Case` entity has **no** `SensitivityLevel` today — migration is greenfield.
- Coordinator web `GET /cases/search` stays full-name — redaction is field-worker mobile paths only.

---

## Verdict

Story is **implementation-ready** after applied fixes. Proceed with `bmad-dev-story`.
