# Story Validation Report — 3.4 GPS Capture, Landmark, and Google Maps Navigation

**Story:** `3-4-gps-capture-landmark-and-google-maps-navigation`  
**Validated:** 2026-06-16  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (9 fixes applied 2026-06-16)

---

## Summary

Story 3.4 correctly closes the FR-9 gap deferred from Stories 3.2–3.3: case-level GPS storage, field-worker verify API, landmark capture gate, and Google Maps handoff. Scope vs proximity grouping (3.5), offline (3.6), and embedded Maps SDK is explicit. Nine gaps could cause **duplicate navigate logic**, **missing DTO flags on visit lists**, **OpenAPI drift**, or **stub tests left in place**.

| Check | Result |
|-------|--------|
| Epic 3.4 AC (landmark gate, verify on case, unverified flag) | Pass |
| FR-9 PRD consequences (Maps, coords, verified by) | Pass |
| Case aggregate location fields (`case-and-lifecycle.md`) | Pass |
| Story 3.2/3.3 Navigate stub replacement | Pass |
| OpenAPI / api-client regen workflow | **Fix** |
| Shared mobile navigate helper (no duplication) | **Fix** |
| Visit list `gpsVerified` on nested `CaseSummaryDto` | **Fix** |
| Command Strip meta `GPS unverified` suffix | **Fix** |
| Geolocation permission + denied-path UX | **Fix** |
| Re-verify allowed (update coords) | **Fix** |
| Linking fallback when Maps unavailable | **Fix** |
| Director/wrong-assignee 403 on verify | **Fix** |
| Proximity grouping / offline | Correctly deferred to 3.5 / 3.6 |

---

## Critical Issues (Must Fix) — Applied

### 1. OpenAPI export workflow missing

Same regression as pre-validate 3.3 — AC must include `EXPORT_OPENAPI_PATH` + snapshot commit + `API_OPENAPI_FILE` build steps.

**Fix applied:** AC12 + task bullets with Windows env vars.

### 2. Visit list DTOs would omit `gpsVerified`

Navigate gate reads `visit.case` from `GET /visits/today`. Without GPS fields on `CaseSummaryDto`, mobile cannot branch.

**Fix applied:** AC6 + `VisitService.ToCaseSummary` task.

### 3. Duplicate Navigate logic across Today + Active Visit

Two screens currently have identical Alert stubs; implementing separately risks drift.

**Fix applied:** AC11 + shared `visitNavigation` module task.

### 4. Command Strip meta line still omits GPS hint

Story 3.2 explicitly deferred `GPS unverified` on meta until 3.4; mockup shows it.

**Fix applied:** AC10 + `CommandStripCard` task.

### 5. Verify endpoint RBAC underspecified in epic

Epic only mentions field capture; must forbid coordinator/director mutation and wrong assignee.

**Fix applied:** AC4 + service task (`EnsureCanReadCase` + supervisor reject on write).

### 6. Audit + transaction not stated

Other visit mutations require audit in same `SaveChangesAsync`.

**Fix applied:** AC2 + `AuditEventTypes.CaseGpsVerified` task.

---

## Enhancement Opportunities — Applied (2026-06-16)

### 7. Re-verify path

Field workers may need to correct landmark after first capture.

**Fix applied:** AC5.

### 8. Geolocation permission denied UX

AC7 must not silently fail when permission denied.

**Fix applied:** AC7 inline error + dev notes permission strings.

### 9. `Linking.canOpenURL` fallback

Devices without Google Maps should not crash navigate flow.

**Fix applied:** AC9.

---

## LLM Optimization Notes

- **Linking vs Maps SDK:** Explicit v1 uses external Maps URL — prevents dev agent adding `react-native-maps` unnecessarily.
- **`decimal(9,6)`:** Stated in AC1 to match geo precision convention.
- **Exact modal title:** Matches EXPERIENCE.md / epic wording for QA.

---

## Verdict

Story is **implementation-ready** after applied fixes. Run `bmad-dev-story` on `3-4-gps-capture-landmark-and-google-maps-navigation.md`.
