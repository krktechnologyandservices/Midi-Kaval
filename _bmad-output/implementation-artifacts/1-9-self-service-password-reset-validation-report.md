# Story Validation Report — 1.9 Self-Service Password Reset

**Story:** `1-9-self-service-password-reset`  
**Validated:** 2026-06-15  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (after 10 fixes applied)

---

## Summary

Story 1.9 correctly spans API + web + mobile per epics FR-1 AC3. Brownfield analysis aligns with Stories 1.4–1.8 (Redis stores, session invalidation, auth shells). Ten fixes merged to prevent enumeration leaks, wrong reset order, guestGuard blocking email links, and ambiguous token error semantics.

| Check | Result |
|-------|--------|
| Epic AC coverage | Pass |
| Architecture / auth patterns | Pass |
| Stories 1.5–1.8 continuity | Pass |
| Scope vs Story 9.2 | Pass |
| Web + mobile testability | Pass (after routing fix) |
| LLM dev-agent clarity | Fixed (reset order, SMTP, guards) |

---

## Findings Applied (fixes merged into story)

### Critical (3) — fixed

**Reset execution order unspecified**  
Dev agent could consume token before password save, or invalidate before DB commit.  
**Fix:** Mandated 6-step order: validate password → peek token → save hash → `InvalidateUserSessionsAsync` → consume token → audit completed.

**SMTP failure on forgot-password could leak account existence**  
Login returns 503 on email failure; same on forgot would reveal valid emails.  
**Fix:** Always return 200 generic on forgot (including SMTP failure, empty email, deactivated).

**Web `guestGuard` blocks reset email links**  
Authenticated users clicking reset links redirect to `/home`.  
**Fix:** `/forgot-password` and `/reset-password` must **not** use `guestGuard`.

### Major (4) — fixed

**Token store pattern underspecified**  
Story said "mirror OtpChallengeStore" but single-use consume matches `RefreshTokenStore` Lua pattern better.  
**Fix:** SHA-256 hash, peek/consume, revoked marker; key naming convention documented.

**401 vs 400 token semantics ambiguous**  
Epic distinguishes expired/used (400) vs invalid (401).  
**Fix:** Never-issued/malformed → 401; consumed/revoked → 400; short password → 400 before token touch.

**Mobile `shouldAttachBearer` not a task**  
Mentioned in notes only — easy to miss.  
**Fix:** Explicit task to extend `PUBLIC_AUTH_PATHS` in `apiClient.ts`.

**Audit events on reset incomplete**  
`InvalidateUserSessionsAsync` also writes `auth.session.invalidated`.  
**Fix:** Integration tests expect 3 audit types; dev notes document both events.

### Enhancements (3) — applied

1. `auth.models.ts` updates listed for web + mobile
2. `SwaggerEndpointTests` + `infra/.env.example` in tasks
3. Integration test: full forgot → reset → login+OTP with new password
4. `PasswordReset` config section name explicit

---

## Checklist Results

### Epics alignment
- Forgot-password generic response, rate-limited, deactivated silent — covered
- Reset-password updates password, invalidates sessions, OTP re-login — covered
- Web + mobile forgot/reset UI with accessibility — covered
- Audit events for request + reset — covered
- Admin force-reset out of scope (9.2) — covered

### Architecture alignment
- Redis token store (§5.2 auth pattern)
- `[AllowAnonymous]` auth surface (Story 1.8)
- `IUserSessionService` force logout (§5.2)
- api-client regeneration required

### Disaster prevention
- Brownfield table accurate
- 40 .NET + 15 web + 15 mobile regression baseline stated
- No enumeration anti-patterns documented
- RefreshTokenStore/OtpChallengeStore lessons referenced

### Remaining minor notes (non-blocking)

| Note | Severity | Action |
|------|----------|--------|
| Mobile universal deep linking (iOS/Android) not fully specified | Low | Manual token entry + route param sufficient for pilot |
| TTL-expired token indistinguishable from never-issued in Redis | Low | Both map to 401/400 per store design; acceptable for pilot |
| `InvalidateUserSessionsAsync` writes separate transaction from password update | Low | Documented order; acceptable for auth-only flow |
| Password complexity beyond min 8 deferred | Low | Matches existing login validation |

---

## Recommendation

**Proceed to `bmad-dev-story`** for Story 1.9. Story file updated with validation fixes.

**Next in cycle:**
1. `bmad-dev-story` — implement API + web + mobile password reset
2. Code review — say **"1"** after implementation
3. Epic 1 retrospective after 1.9 is `done`
