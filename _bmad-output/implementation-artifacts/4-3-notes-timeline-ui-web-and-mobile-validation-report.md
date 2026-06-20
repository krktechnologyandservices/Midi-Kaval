# Story Validation Report — 4.3 Notes Timeline UI (Web and Mobile)

**Story:** `4-3-notes-timeline-ui-web-and-mobile`  
**Validated:** 2026-06-19  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied 2026-06-19)

---

## Summary

Story 4.3 correctly scopes FR-13 UI on web case detail (supervisors) and mobile case detail (field workers primary), consuming Stories 4.1–4.2 APIs with no backend changes. Architecture alignment on presign → PUT → confirm and RBAC-via-download-url is strong. Ten gaps could cause broken whisper scroll, missing attachment chips after upload, wrong date-picker approach, or epic AC misinterpretation on “api-client only.”

| Check | Result |
|-------|--------|
| Epic AC (timeline + badges + attachment + api-client) | Pass (clarified api-client = types + envelope services) |
| Story 4.1 API contracts (note types, action due, 403) | Pass |
| Story 4.2 attachment flow (presign after note, SAS PUT) | Pass |
| Handoff whisper → scroll (UX-DR4) | **Fix** (mobile root View) |
| Post-confirm timeline refresh | **Fix** |
| Web due-date control (no MatDatepicker in repo) | **Fix** |
| Mobile due-date / refresh patterns | **Fix** (RescheduleVisitModal, CasesListScreen) |
| fileName basename before presign | **Fix** |
| Mobile attachment preview 403 | **Fix** |
| Test commands | **Fix** |
| Scope vs 4.4/4.5 / visit_notes merge | Pass |

---

## Critical Issues (Must Fix)

### 1. Mobile whisper scroll impossible with current root `View`

`CaseDetailPlaceholderScreen` uses a root `View`, not `ScrollView`. AC7 requires scroll to notes timeline; tasks mentioned ScrollView ref but did not require replacing the root layout.

**Fix:** AC5 — require `ScrollView` + refs; tasks — mirror `CasesListScreen` RefreshControl; READ FIRST item 12.

### 2. Attachment chips may not appear after confirm without list refresh

AC3 implied chips appear after confirm, but timeline list DTOs only include attachments from `GET /cases/{id}/notes`. Confirm returns `AttachmentDto`, not an updated note row.

**Fix:** AC3 — re-fetch notes (or explicit merge); READ FIRST item 11.

### 3. Web “mat datepicker” not used anywhere in `apps/web`

Story tasks referenced `MatDatepicker`; repo has zero datepicker usage. Dev agent might add `@angular/material/datepicker` + adapter unnecessarily.

**Fix:** Tasks — use `<input type="datetime-local">` with `matInput` and client-side future validation.

### 4. `fileName` path segments not mentioned for client presign

Story 4.2 rejects `/`, `\`, `..` in `fileName`. Web file inputs and mobile pickers can supply paths.

**Fix:** AC3 basename rule; Dev Notes presign prep bullet.

---

## Enhancement Opportunities (Should Add)

### 5. Mobile attachment preview 403 missing (epic: “preview respects role access”)

Web AC4 covered download-url **403**; mobile AC6 did not.

**Fix:** AC6 — download-url **403** handling parity with web.

### 6. Mobile refresh pattern underspecified

AC5 said “pull-to-refresh or explicit refresh” without pointing to existing patterns.

**Fix:** AC5 — `RefreshControl` + reload after add; Dev Notes reference `CasesListScreen.tsx`.

### 7. Mobile action due date — no concrete UI reference

`@react-native-community/datetimepicker` already used in `RescheduleVisitModal.tsx`.

**Fix:** AC6 + Dev Notes — mirror RescheduleVisitModal (future-only).

### 8. Submit/upload busy state not required

Without disabled submit, double-submit could create duplicate notes during slow blob PUT.

**Fix:** AC3 — disable submit while create/upload/confirm in progress.

### 9. v1 multi-attachment scope ambiguous

API supports multiple attachments per note; UI could over-build multi-file upload.

**Fix:** AC3 — one attachment per note submission in v1.

### 10. Epic “generated api-client only” vs HttpClient envelope pattern

Web/mobile use `HttpClient` / `AuthSessionService` with types from `@midi-kaval/api-client`, not openapi-fetch operations. Dev agent might misinterpret AC10.

**Fix:** AC10 — types + envelope services; SAS PUT via raw `fetch` exception.

---

## Optimizations (Nice to Have)

1. **`MAX_ATTACHMENT_BYTES = 10485760`** constant alongside MIME allow-list (applied in tasks).
2. **`POST` 201 Created** called out for `createCaseNote` (applied in tasks).
3. **AC11** — explicit `npm test` in `apps/web` and `apps/mobile` (applied).
4. **Mobile RBAC wording** — primary field worker, supervisors allowed by API (applied in AC6).

---

## LLM Optimization

- READ FIRST expanded to **12** pinned guardrails (was 10).
- Concrete file references for mobile ScrollView, RefreshControl, DateTimePicker, and web datetime-local.
- Attachment confirm → list refresh made explicit to prevent “confirm succeeded but no chip” bug.
- Epic api-client AC disambiguated from SAS PUT exception.

---

## Verdict

Story is **implementable** and well-scoped vs Stories 4.4–4.5. All **10** fixes applied to the story file. Safe to run `dev-story` for Story 4.3.

**Out of scope confirmed:** API changes, `visit_notes` merge, offline note sync, interventions UI, E2E Playwright (optional).

**Known pre-existing:** `VisitGroupingTests` (2 failures) — unchanged by this UI story.
