# Story Validation Report — 1.4 Login and Email OTP API

**Story:** `1-4-login-and-email-otp-api`  
**Validated:** 2026-06-14  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 6 fixes applied)

---

## Summary

Story 1.4 is comprehensive and aligns with epics FR-1, architecture §5.2/§5.3, and Story 1.3 brownfield state. Scope is correctly bounded against Stories 1.5 (refresh/logout), 1.8 (RBAC), and 1.6–1.7 (client UI). Six fixes were merged to prevent the same Testing-environment regression class as Story 1.3 and to clarify auth integration test wiring.

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass |
| Architecture alignment | Pass (with CORS deferred to 1.6) |
| Story 1.3 continuity | Pass |
| Scope vs 1.5 / 1.8 | Pass |
| Story 1.2 regression risk | Fixed (Testing env skips Redis/JWT) |
| Testability | Fixed (auth fixture pattern documented) |
| LLM dev-agent clarity | Pass |

---

## Findings Applied (fixes merged into story)

### Critical (2) — fixed

**Redis/JWT registration would break Story 1.2 HTTP smoke tests**  
Adding Redis + JWT at startup (like DbContext in 1.3) would force `TestingWebApplicationFactory` tests to require local Redis and fail in CI.  
**Fix:** Added explicit rule — register Redis, JWT, email sender **only when `!IsTesting()`** (mirror DbContext pattern). HTTP regression tests unchanged.

**Auth integration test fixture underspecified**  
Story said "separate fixture" but not how to inject Testcontainers Postgres + Redis into `Program.cs` without using `Testing` env.  
**Fix:** Added **Auth test environment strategy** — dedicated `AuthWebApplicationFactory` with `ConfigureWebHost` overrides for container connection strings + `FakeEmailSender`; uses `Development` or `AuthIntegration` env, **not** `Testing`.

### Major (2) — fixed

**Missing `UseAuthentication()` / `UseAuthorization()` middleware**  
JWT bearer registration alone does not validate tokens; middleware order matters for future protected routes.  
**Fix:** Added Program.cs task — insert `UseAuthentication()` and `UseAuthorization()` after exception handler, before `MapControllers`.

**OTP test assertion ambiguity**  
OTP is hashed in Redis; tests cannot read OTP from Redis.  
**Fix:** Clarified tests extract 6-digit code from `FakeEmailSender` captured message body (plaintext captured at send time before hashing).

### Enhancements (2) — applied

1. Added note: rate-limit **429** responses must flow through existing Problem Details pipeline (configure `OnRejected` or middleware)
2. Added JWT claim naming guidance: use `organisation_id` and `token_version` as custom claim types (consistent with DB snake_case intent in JWT payload)

---

## Checklist Results

### Epics alignment
- User story matches `epics.md` Story 1.4
- All epic AC elements covered: login, verify-otp, JWT 15min, refresh contract, 401/403, rate limit, OTP email
- Deactivated message "Contact your coordinator" matches epic

### Architecture alignment
- `/api/v1/auth/login`, `/api/v1/auth/verify-otp` per §5.3
- JWT + refresh, Redis OTP state per §5.2
- Rate limiting on auth per §5.2
- Envelope + Problem Details preserved from Story 1.2
- No `audit_events` table (deferred to 1.5) — correct

### Disaster prevention
- Anti-patterns: no refresh/logout endpoints, no RBAC policies, no audit table, no account enumeration
- Brownfield UPDATE table present
- Story 1.3 intelligence included (email normalization, IsTesting, Testcontainers)
- 1.4/1.5 and 1.4/1.8 boundaries explicit

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| CORS allowlist for web origin not in 1.4 | Low | Defer to Story 1.6 when Angular shell calls API |
| `SendGrid` mentioned in architecture; story uses MailKit SMTP | Low | SMTP sufficient for dev/pilot; SendGrid adapter later |
| Auth tests need Docker for Postgres + Redis containers | Low | Document in README; same as 1.3 Testcontainers |
| Epic says "five roles"; codebase has 4 (`AppRole`) | Low | Pre-existing; RBAC in 1.8 |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 1.4. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement login + OTP API
2. `bmad-code-review` — after implementation
3. `bmad-create-story` — Story 1.5 after 1.4 is `done`
