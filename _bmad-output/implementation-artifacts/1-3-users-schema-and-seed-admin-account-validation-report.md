# Story Validation Report — 1.3 Users Schema and Seed Admin

**Story:** `1-3-users-schema-and-seed-admin-account`  
**Validated:** 2026-06-14  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story**

---

## Summary

Story 1.3 is comprehensive and implementation-ready after applying 5 fixes. Aligns with epics, architecture §5.1/§6.4, and `project-context.md`. Scope is correctly bounded against Story 1.4 (login API).

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass (with clarified password_hash + table-only scope) |
| Architecture alignment | Pass |
| Project context compliance | Pass |
| Scope vs Story 1.4 | Fixed (explicit boundary added) |
| Story 1.2 regression risk | Fixed (Testing environment strategy) |
| Testability | Pass |
| LLM dev-agent clarity | Pass |

---

## Findings Applied (fixes merged into story)

### Critical (1) — fixed

**DbContext startup would break Story 1.2 HTTP integration tests**  
Adding EF Core + auto-migrate/seed on startup would cause existing `WebApplicationFactory` tests to require local PostgreSQL and fail in CI/fresh clones.  
**Fix:** Added **Test environment strategy** tasks — `ASPNETCORE_ENVIRONMENT=Testing` skips migrate/seed; existing fixtures use `UseEnvironment("Testing")`; Testcontainers tests use a separate fixture.

### Major (2) — fixed

**Epic AC “columns only” ambiguity**  
Epic lists `role, email, token_version, organisation_id` but Story 1.4 requires `password_hash` for login. Dev agent could omit password column.  
**Fix:** Clarified epic AC means **no unrelated tables**; `password_hash`, `id`, timestamps, `is_active` are required. Added **Story 1.3 / 1.4 boundary** note.

**Identity package ambiguity**  
Story listed `Microsoft.AspNetCore.Identity.EntityFrameworkCore` which pulls full Identity stack.  
**Fix:** Pin to `Microsoft.Extensions.Identity.Core` only for `IPasswordHasher<User>`.

### Enhancements (2) — applied

1. Added `EFCore.NamingConventions` package explicitly for `UseSnakeCaseNamingConvention()`
2. Clarified Testcontainers provides Postgres during tests — `docker compose` not required for `dotnet test`

---

## Checklist Results

### Epics alignment
- User story statement matches `epics.md` Story 1.3
- All epic AC elements covered; story adds justified schema detail for Story 1.4 readiness
- Idempotent seed + CI-repeatable migrations addressed

### Architecture alignment
- Migrations in `apps/api` (not stale `src/api`)
- snake_case DB columns, UUID ids, `organisation_id` tenant-ready
- Testcontainers per §6.4
- `users` aggregate only — no cases/audit/legends

### Disaster prevention
- Anti-patterns: no auth API, no extra tables, no committed secrets
- Brownfield UPDATE table for Story 1.2 files
- Previous story intelligence from 1.2 included
- Test regression strategy explicit

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| PRD mentions “five roles”; codebase uses 4 (`AppRole` enum) | Low | Align with `AppRole` — matches Story 1.8 RBAC |
| Testcontainers requires Docker daemon for schema tests | Low | Document in README; acceptable per architecture |
| `appsettings.Development.json` seed placeholders left to dev agent | Low | Task already specifies |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 1.3. No further story edits required.

**Next in cycle:**
1. `bmad-dev-story` — implement users schema + seed
2. `bmad-code-review` — after implementation
3. `bmad-create-story` — Story 1.4 after 1.3 is `done`
