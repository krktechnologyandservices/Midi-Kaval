# Story Validation Report — 1.5 Refresh, Logout, and Forced Session Invalidation

**Story:** `1-5-refresh-logout-and-forced-session-invalidation`  
**Validated:** 2026-06-14  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 7 fixes applied)

---

## Summary

Story 1.5 is comprehensive and aligns with epics FR-1/FR-2, architecture §5.1/§5.2, and Story 1.4 brownfield auth. Scope is correctly bounded against Stories 1.6–1.7 (client UI), 1.8 (RBAC policies), and 1.9 (password reset). Seven fixes were merged to eliminate ambiguities that would have caused implementation drift (logout auth model, Redis store migration, refresh validation, rate limits).

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass |
| Architecture alignment | Pass |
| Story 1.4 continuity | Pass |
| Scope vs 1.8 / 1.9 | Pass |
| Story 1.2 regression risk | Pass (Testing env unchanged) |
| Testability | Pass (after test matrix fix) |
| LLM dev-agent clarity | Fixed (logout/refresh ambiguities resolved) |

---

## Findings Applied (fixes merged into story)

### Critical (4) — fixed

**Logout endpoint auth model ambiguous**  
Task listed `[AllowAnonymous]` OR `[Authorize]` without a decision — dev agent could implement logout requiring expired JWT (unusable).  
**Fix:** Logout is `[AllowAnonymous]` but **requires valid refresh token** (cookie or body). Returns **204 No Content** without envelope.

**RefreshTokenStore IDistributedCache migration underspecified**  
Story 1.4 code review proved `IDistributedCache` vs `IConnectionMultiplexer` key-prefix mismatch causes production bugs. Current `RefreshTokenStore` still uses `IDistributedCache`.  
**Fix:** Mandate `IConnectionMultiplexer` only; store `userId` + `token_version` in Redis JSON value; add `refresh:user:{userId}` index for bulk revoke.

**Refresh flow missing DB re-validation**  
AC4 requires `token_version` bump invalidates refresh — Redis-only userId lookup is insufficient.  
**Fix:** AC1 now requires reload user from DB on refresh; reject inactive (**403**) or version mismatch (**401**).

**Optional refreshToken in AC1 response**  
Rotation requires always returning new refresh token — optional `?` would allow incomplete implementation.  
**Fix:** AC1 requires `refreshToken` always in success envelope + cookie.

### Major (2) — fixed

**Missing reuse detection on rotated refresh tokens**  
1.4 deferred item explicitly assigned to 1.5.  
**Fix:** AC1 + RefreshTokenStore task — reuse of revoked hash revokes all user refresh tokens.

**Per-user refresh cap marked optional**  
1.4 code review deferred unbounded tokens to 1.5.  
**Fix:** `RefreshToken:MaxActivePerUser` (default 5) now **required**.

### Enhancements (3) — applied

1. Added `auth-logout` rate limit policy alongside `auth-refresh`
2. Added `GET /auth/me` deactivated user test (403 coordinator message)
3. Documented `[Authorize]` on `/auth/me` overrides controller-level `[AllowAnonymous]`
4. Added `ReadRefreshToken(HttpRequest)` helper task (cookie-first, then body)
5. Anti-pattern: do not wrap 204 logout in API envelope

---

## Checklist Results

### Epics alignment
- User story matches `epics.md` Story 1.5
- Refresh issues new access token; logout invalidates refresh — covered
- `token_version` increment → 401 on next API call — covered via JWT middleware + refresh DB check
- `audit_events` for login, logout, failed OTP — covered (+ optional refresh success)

### Architecture alignment
- `/api/v1/auth/refresh`, `/api/v1/auth/logout` per §5.3
- JWT + refresh rotation per §5.2
- `audit_events` append-only table per §5.1
- Force logout via `token_version` per §5.2
- Envelope on data responses; 204 logout exempt

### Disaster prevention
- Brownfield UPDATE table present with accurate 1.4 file states
- 1.5/1.8 and 1.5/1.9 boundaries explicit
- Anti-patterns include no RBAC on business routes, no Testing-env Redis/JWT
- Previous story intelligence includes 1.4 CR learnings (Redis prefix, test factory env vars)
- `IUserSessionService` reusable by 1.9 password reset

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| 403 on inactive user via JWT bearer requires custom handler (not default 401) | Low | Dev notes now reference `IAuthorizationMiddlewareResultHandler` |
| Epic audit list omits `auth.refresh.success` | Low | Optional in story; recommended |
| `GET /audit` Director UI deferred to 9.3 | Low | Correct scope |
| Refresh token SHA256 not slow-hashed | Low | Acceptable for pilot; noted in 1.4 deferred-work |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 1.5. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement refresh/logout/session invalidation + audit_events
2. `bmad-code-review` — after implementation
3. `bmad-create-story` — Story 1.6 after 1.5 is `done`
