# Story Validation Report — 2.1 Case Aggregate Schema and Create API

**Story:** `2-1-case-aggregate-schema-and-create-api`  
**Validated:** 2026-06-15  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 9 fixes applied)

---

## Summary

Story 2.1 correctly scopes Epic 2 kickoff to **API + `cases` migration only** (no web/mobile). Brownfield analysis matches Epic 1 auth/RBAC/audit patterns. Nine fixes merged to prevent split audit transactions, ambiguous 403 bodies, case-sensitive duplicate bypass, and incomplete integration test coverage.

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass (Director via `CoordinatorOrAbove` clarified) |
| Architecture §5.1 / §5.3 alignment | Pass |
| Epic 1 continuity (RBAC, audit, Testcontainers) | Pass |
| Scope vs Stories 2.2–2.9 | Pass |
| LLM dev-agent clarity | Fixed (normalization, 409 mapping, claims) |
| Testability | Pass (after matrix expansion) |

---

## Findings Applied (fixes merged into story)

### Critical (2) — fixed

**Audit split across two `SaveChanges` calls**  
Story referenced `AuditService.RecordAsync`, which persists audit in its own transaction.  
**Fix:** Mandate direct `db.AuditEvents.Add` + single `SaveChangesAsync` with case insert (mirror `UserSessionService`).

**403 semantics conflated deactivated vs wrong role**  
Story 1.8 handler order: inactive users get `DeactivatedMessage`, not `ForbiddenByRoleMessage`.  
**Fix:** AC4b for deactivated; security section documents both 403 variants.

### Major (5) — fixed

**Crime/ST unique index case-sensitive**  
Without normalization, `CR-001` and `cr-001` could both exist.  
**Fix:** `Trim()` + `ToUpperInvariant()` before persist; documented for Story 2.3 alignment.

**Postgres duplicate → 409 mapping underspecified**  
Generic `DbUpdateException` catch is fragile.  
**Fix:** Check inner `PostgresException.SqlState == "23505"`.

**Claims resolution not actionable**  
Dev agent needs `IHttpContextAccessor` + exact claim types (`organisation_id`, `NameIdentifier`/`sub`).  
**Fix:** Explicit in `CaseService` task; `CaseService` DI registration added.

**Enum JSON binding**  
API has no `JsonStringEnumConverter` — binding request DTOs directly to C# enums would fail or behave inconsistently.  
**Fix:** String properties on request DTO; parse case-insensitively in service.

**Integration test matrix incomplete**  
Only duplicate crime listed; epic requires both identifiers unique.  
**Fix:** Separate crime/ST 409 tests; deactivated coordinator; invalid enum 400; director uses `AuthTestData.Email`.

### Enhancements (2) — applied

1. `Created(...)` 201 response pattern documented (`ApiEnvelopeFilter` wraps 2xx)
2. `CaseTestData` helper shape specified (`BuildValidRequest`, `BuildCoordinatorSessionAsync`)
3. Migration apply via `DatabaseInitializer` in Development noted for test factory

---

## Checklist Results

### Epics alignment
- POST create with required fields, initial stage, audit — covered
- Migration scoped to `cases` only — covered
- 400 validation — covered
- Coordinator create — covered (+ Director via policy, documented)

### Architecture alignment
- `UNIQUE(organisation_id, crime_number/st_number)` — covered
- `/api/v1/cases` REST + envelope — covered
- RBAC on mutation, no `[AllowAnonymous]` — covered
- 409 duplicate semantics per `project-context.md` — covered

### Disaster prevention
- 53/19/17 regression baseline stated
- `AuthWebApplicationFactory` vs `TestingWebApplicationFactory` distinction — covered
- Brownfield table accurate
- Epic 1 retro lessons referenced (scoped migration, review workflow)

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| `sensitivity_level` / POCSO column deferred | Low | Story 2.4+ or discreet mode epic |
| `assigned_worker_id` deferred | Low | Story 2.8 assignment |
| `GET /cases/{id}` not in 2.1 | Low | Story 2.9 web detail UI |
| Full PRD §5.2 optional fields deferred | Low | Incremental stories |
| `Location` header URI stability | Low | `Created` with `/api/v1/cases/{id}` sufficient |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 2.1. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement case entity, migration, POST API, integration tests
2. Code review — say **"1"** after implementation
3. `bmad-create-story` for Story 2.2 when 2.1 is `done`
