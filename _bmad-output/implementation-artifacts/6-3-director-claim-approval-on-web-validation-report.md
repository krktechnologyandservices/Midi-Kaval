# Story Validation Report — 6.3 Director Claim Approval on Web

**Story:** `6-3-director-claim-approval-on-web`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (12 fixes applied)

---

## Summary

Story 6.3 correctly scopes Director approve/return API, crisis-queue pending claim rows, Admin web review, and partial Crisis Queue UI per FR-18 Flow 6. Alignment with Stories 6.1/6.2/5.4/4.5 is strong. Twelve gaps could cause notification insert failures, coordinator navigation dead-ends, RBAC leaks, or ambiguous controller/envelope patterns.

| Check | Result |
|-------|--------|
| Epic AC (approve/return, comment, crisis queue neutral row) | Pass |
| Architecture §5.3 approve endpoint | Pass (+ explicit return endpoint) |
| Story 6.1 attachment read for supervisors | Pass (Director already in `IsSupervisorRole`) |
| Story 5.4 crisis-queue extension | Pass |
| Notification `CaseId` for travel claims | **Fix** |
| `claimantEmail` DTO mapping scope | **Fix** |
| Coordinator read-only review route | **Fix** |
| Approve response envelope pattern | **Fix** |
| Nullable `CrisisQueueItemDto` court fields | **Fix** |
| Field-worker `decisionComment` on GET (AC 3b) | **Fix** |
| Controller layout pinned | **Fix** |
| Swagger + CaseTestData helpers | **Fix** |
| Monthly totals after return regression | **Fix** |
| Scope vs 6.4 / 8.1 / 8.2 | Pass |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Notification rows **must** set `CaseId` to first linked case from `travel_claim_cases` (`in_app_notifications` requires it); `ResourceType=TravelClaim`, `ResourceId=claimId` |
| 2 | `claimantEmail` mapped **only** on director/supervisor list/get — not field-worker `ListMine`/`Get` |
| 3 | AC **4b** + `GET /api/v1/supervisor/travel-claims/{id}` for Coordinator read-only review |
| 4 | Web route **`/crisis-queue/travel-claims/:id`** with read-only `TravelClaimReviewComponent` (coordinator cannot use `directorGuard` admin routes) |
| 5 | Approve/return return **`Ok(dto)`** wrapped by `ApiEnvelopeFilter` — mirror submit; list endpoints use explicit `ApiResponse<>` |
| 6 | Pin controllers: `DirectorTravelClaimsController` (pending/get), `TravelClaimsController` (approve/return), `SupervisorController` (coordinator get) |
| 7 | `CrisisQueueItemDto` court fields **nullable** on claim rows; update crisis-queue integration tests |
| 8 | AC **3b** — field-worker GET own Approved/Returned claim includes `decisionComment` |
| 9 | `receiptCount` = count of Confirmed attachments; title uses **email local-part** |
| 10 | `SwaggerEndpointTests` + `CaseTestData` helper tasks for director flows |
| 11 | `Program.cs` DI update for `TravelClaimService` + `NotificationService` |
| 12 | Dev note — monthly totals exclude `Returned`; approve keeps claim in totals |

---

## Remaining Notes (non-blocking)

- **Return without reopen:** v1 intentionally has no claimant resubmit path; mobile stays read-only until follow-up story.
- **Story 6.4:** Richer notification channels/copy; this story creates in-app rows only (push deferred 7.2).
- **Story 8.2:** Full crisis-queue empty polish, 200% zoom, all row types — partial UI in 6.3 is intentional.

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `6-3-director-claim-approval-on-web`.

**Out of scope confirmed:** full crisis queue UI (8.2), Redis merge (8.1), push delivery (7.2), notification bell (7.4), mobile reopen/resubmit UI.
