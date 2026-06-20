# Story Validation Report — 1.8 RBAC Policies on Protected Endpoints

**Story:** `1-8-rbac-policies-on-protected-endpoints`  
**Validated:** 2026-06-14  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 8 fixes applied)

---

## Summary

Story 1.8 correctly scopes server-side FR-2/NFR-2 RBAC before Epic 2 business endpoints. Brownfield analysis matches current API (auth-only controllers, no role policies yet). Probe scaffold approach is appropriate. Eight fixes were merged to eliminate handler-order ambiguity, incomplete test matrices, and helper duplication that would have caused flaky or incomplete implementation.

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass |
| Architecture §6.3 alignment | Pass |
| Stories 1.5–1.7 continuity | Pass |
| Scope vs 1.9 / Epic 2 | Pass |
| Story 1.2 Testing env regression | Pass |
| Testability | Pass (after matrix + collection fixes) |
| LLM dev-agent clarity | Fixed (handler order, helpers, DoD) |

---

## Findings Applied (fixes merged into story)

### Critical (3) — fixed

**Authorization handler branch order unspecified**  
Dev agent could return RBAC message for deactivated users or empty 403 bodies.  
**Fix:** Pseudocode added — inactive branch first, then `authorizeResult.Forbidden` → RBAC Problem Details, else default handler (401 for unauthenticated).

**Allowed-call matrix missing from AC**  
Epic requires denied **and** implied allowed scenarios; only denied cases were listed.  
**Fix:** AC3 now includes explicit denied + allowed tables per role.

**Integration test collection not specified**  
Parallel `AuthWebApplicationFactory` fixtures cause flaky Testcontainer failures without shared collection.  
**Fix:** Mandate `[Collection("AuthIntegration")]` on `RbacAuthorizationTests`.

### Major (4) — fixed

**Duplicate OTP helper risk**  
Story proposed new `RbacTestHelpers` duplicating `AuthTestHelpers.LoginAndVerifyAsync`.  
**Fix:** Extend existing helper with `email` parameter; update file list accordingly.

**Unauthenticated probe behavior untested**  
Wrong role → 403, but missing token must be 401 (distinct semantics).  
**Fix:** AC3 + task for `Unauthenticated_Probe_Returns401`.

**Test user seed idempotency**  
Re-running tests could violate unique email constraints.  
**Fix:** Upsert on `(organisation_id, email)`; shared `AuthTestData.OrganisationId` + password.

**AllowAnonymous audit absent**  
Epic AC requires no `[AllowAnonymous]` on data mutations — needed explicit controller audit task.  
**Fix:** Task confirms only auth surface is anonymous; Health/Meta/Diagnostics out of scope.

### Enhancements (2) — applied

1. Definition of Done checklist added (matches Stories 1.5–1.7 pattern)
2. Deactivated user on RBAC probe documented (inactive branch, not RBAC message)
3. DI registration note if handler class renamed

### Optimizations (1) — applied

- Consolidated test user table; removed redundant inline C# seed snippet

---

## Checklist Results

### Epics alignment
- User story matches `epics.md` Story 1.8
- Policies map to four roles — covered via `Policies.cs` + registration
- Social Worker → Coordinator endpoint → 403 — covered via probe + denied matrix
- `[AllowAnonymous]` absent on data mutations — covered via audit task + probe rules
- Integration tests: ≥1 denied per role — covered (4 scenarios)

### Architecture alignment
- `[Authorize(Policy = Policies.CoordinatorOrAbove)]` pattern per §6.3
- 403 RBAC denied per §6.2 HTTP codes
- Policy-based auth per §5.2
- Server-only enforcement (clients unchanged) per FR-2

### Disaster prevention
- Brownfield UPDATE table accurate (no policies today; JWT role claim exists)
- 1.8/1.9 boundary explicit (no password reset)
- 1.8/Epic 2 boundary explicit (probe scaffold, policies persist)
- Do-not-break list: Testing env, auth tests, deactivated 403, refresh contracts
- Previous story intelligence from 1.5–1.7 included

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| Per-resource authorization (case assignment) deferred to Epic 2+ | Low | Correct scope |
| `Policies.Director` vs `DirectorOnly` naming — dev should use composites where possible | Low | Documented in policy matrix |
| Probe routes live in production until Epic 2 — acceptable for pilot | Low | README marks as scaffold |
| No unit tests for policy registration — integration coverage sufficient | Low | Acceptable for 1.8 |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 1.8. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement policies, handler, probe controller, RBAC tests
2. Code review — say **"1"** after implementation to apply patches + mark done
3. `bmad-create-story` — Story 1.9 after 1.8 is `done`
