# Story Validation Report — 4.2 Attachment Presign Upload for Notes

**Story:** `4-2-attachment-presign-upload-for-notes`  
**Validated:** 2026-06-19  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (9 fixes applied 2026-06-19)

---

## Summary

Story 4.2 correctly scopes blob-backed note attachments (presign → PUT → confirm, read SAS, timeline metadata) for FR-13/NFR-5. Architecture alignment with the generic `/attachments` flow and Story 4.1 RBAC is strong. Nine gaps could cause Azurite integration failures, RBAC test holes, or regressions in existing `CaseNotesTests`.

| Check | Result |
|-------|--------|
| Epic AC (presign → PUT → confirm, SAS 15 min, RBAC) | Pass |
| Architecture §5.2 / §6 attachment flow | Pass |
| Story 4.1 dependency (note must exist first) | Pass |
| RBAC (`EnsureCanReadCase` chain) | Pass (duplicate helper documented) |
| Unassigned case 403 (Story 2.8) | **Fix** |
| Local Azurite / HTTP vs HTTPS | **Fix** |
| Integration test env (`Development` not `Testing`) | **Fix** |
| `CaseNoteDto.attachments` backward compat | **Fix** |
| Test matrix completeness | **Fix** |
| Blob path convention consistency | **Fix** |
| Confirm audit transaction | **Fix** |
| Scope vs 4.3 / Epic 6 | Pass |

---

## Critical Issues (Must Fix)

### 1. AC1 required HTTPS — breaks Azurite local dev and integration tests

AC1 stated "HTTPS URL" for `uploadUrl`. Azurite and `AuthWebApplicationFactory` use HTTP blob endpoints. Dev agent might reject valid presign responses or misconfigure SAS builder.

**Fix:** AC1 — "HTTPS in production; HTTP acceptable for local Azurite". READ FIRST item 6.

### 2. Wrong assumption about `Testing` environment in integration tests

Story said blob startup "not Testing if tests override". **`AuthWebApplicationFactory` uses `Development`** (`UseEnvironment("Development")`), not `Testing`. Blob services register in the main `Program.cs` block; Azurite env vars must be set in the factory before host start.

**Fix:** Tasks + READ FIRST item 12 + expanded `AuthWebApplicationFactory` Dev Notes.

### 3. Unassigned case 403 missing from AC2 / AC11

Story 4.1 and 2.8 require **403** when `assigned_worker_id` is null and actor is a field worker. Attachment presign/confirm/download must follow the same rule.

**Fix:** AC2 explicit bullet; AC11 integration test.

### 4. Blob path inconsistency between task and Dev Notes

Task used `{resourceType}` enum segment; Dev Notes used `case-note` lowercase segment. Dev agent could generate incompatible paths between service and tests/docs.

**Fix:** Unified path `{organisationId}/case-note/{caseNoteId}/{attachmentId}/{sanitizedFileName}` in task + Dev Notes.

### 5. `CaseNoteDto.attachments` could break existing `CaseNotesTests`

Adding `attachments` to timeline DTO without guidance risks null vs empty array failures in 20 existing tests.

**Fix:** AC11 regression bullet; Dev Notes — default `Array.Empty` in `MapToDto`; READ FIRST item 9.

---

## Enhancement Opportunities (Should Add)

### 6. Test matrix gaps (Story 4.1 parity)

Missing: CaseWorker parity, deactivated **403**, wrong-assignee **403** on `GET download-url`, numeric `resourceType` **400** (Story 4.1 `noteType` lesson).

**Fix:** Extended AC11 test list.

### 7. Confirm audit not explicitly transactional

Story 4.1 required audit in same `SaveChangesAsync`. Confirm flow should mirror.

**Fix:** AC3 + READ FIRST item 10.

### 8. Attachment summary ordering unspecified

Timeline could return attachments in arbitrary order when a note has multiple files.

**Fix:** AC6 — order by `confirmed_at_utc`, then `id`.

### 9. Blob container bootstrap location vague

"Ensure container on startup" did not specify where — dev agent might hook `Program.cs` incorrectly or skip for Development integration tests.

**Fix:** Extend `DatabaseInitializer.ApplyMigrationsAndSeedAsync` after migrate; add to file list.

---

## Optimizations (Nice to Have)

1. **Reuse `CaseConflictException`** for double confirm **409** — matches `CasesController.ConflictProblem` without new exception mapping (applied in tasks).
2. **Controller null-body guard** on POST presign/confirm — mirror notes routes (applied in tasks).
3. **README Quick start** — mention Azurite port 10000 alongside Postgres/Redis (applied in tasks).
4. **`resourceType` explicit whitelist** — reject `"0"` like Story 4.1 `noteType` fix (applied in AC2 + AC11).

---

## LLM Optimization

- READ FIRST expanded to **12** pinned guardrails (was 9).
- Corrected `Development` vs `Testing` env fact — prevents missing blob DI in integration tests.
- AC11 test list now mirrors Story 4.1 RBAC matrix.
- Blob bootstrap path specified (`DatabaseInitializer` + factory env vars).

---

## Verdict

Story is **implementable** and well-scoped vs Stories 4.3–4.5 and Epic 6 reuse. All **9** fixes applied to the story file. Safe to run `dev-story` for Story 4.2.

**Out of scope confirmed:** web/mobile UI (4.3), travel claims (Epic 6), attachment delete, offline sync, pending cleanup job.

**Known pre-existing:** `VisitGroupingTests` (2 failures) — do not block this story.
