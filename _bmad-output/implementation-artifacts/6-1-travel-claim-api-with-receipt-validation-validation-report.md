# Story Validation Report — 6.1 Travel Claim API with Receipt Validation

**Story:** `6-1-travel-claim-api-with-receipt-validation`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied)

---

## Summary

Story 6.1 correctly scopes API-only travel claims (Draft CRUD, receipt-gated submit, supervisor monthly totals) per FR-18/CAP-9. Attachment reuse from Story 4.2 and court-sitting CRUD patterns from 5.1 are well aligned. Ten gaps could cause `UsersSchemaTests` failure, attachment RBAC holes, or ambiguous presign rules after submit.

| Check | Result |
|-------|--------|
| Epic AC (receipt required, Draft→Submitted, monthly totals) | Pass |
| Architecture §5.1 TravelClaim aggregate | Pass (v1 single amount, no line_items table) |
| Story 4.2 attachment extension | Pass |
| FieldWorker RBAC | Pass |
| UsersSchemaTests table enumeration | **Fix** |
| Presign blocked after submit | **Fix** |
| GET claim wrong claimant (404 vs 403) | **Fix** |
| Monthly totals query validation | **Fix** |
| Coordinator read Submitted attachments | **Fix** |
| Integration test matrix (CaseWorker parity) | **Fix** |
| Regression test wording (TravelClaim wired) | **Fix** |
| api-client regen steps | **Fix** |
| Scope vs 6.2–6.4 / 8.1 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | `UsersSchemaTests` — **must** add `travel_claims`, `travel_claim_cases` to table list + `ClearUsersAsync` TRUNCATE |
| 2 | AC 5b — presign/confirm **422** when `status != Draft` |
| 3 | AC 3b — `GET /travel-claims/{id}` returns **404** for non-claimant (same org) — hide existence |
| 4 | AC 8b — invalid `year`/`month` query params → **400** |
| 5 | AC 5c — `CoordinatorOrAbove` may download **Submitted** claim attachments (6.3 prep) |
| 6 | Dev notes — v1 **no** `line_items` table; single amount per claim |
| 7 | Tests — `CaseWorker` parity alongside `SocialWorker` |
| 8 | Regression — assert **both** `CaseNote` and `TravelClaim` presign after wiring |
| 9 | api-client regen + `npm run build -w @midi-kaval/api-client` |
| 10 | Audit metadata — `claimId`, `status`, `transportMode` only (no `notes`) |

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `6-1-travel-claim-api-with-receipt-validation`.

**Out of scope confirmed:** mobile UI (6.2), Director approve/return (6.3), notifications (6.4), crisis-queue rows (8.1), offline sync.
