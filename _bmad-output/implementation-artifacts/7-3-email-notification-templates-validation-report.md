# Story Validation Report — 7.3 Email Notification Templates

**Story:** `7-3-email-notification-templates`  
**Validated:** 2026-06-20  
**Validator:** bmad-create-story (validate)  
**Verdict:** **PASS — ready for dev-story** (10 fixes applied)

---

## Summary

Story 7.3 correctly scopes centralized plain-text templates, `EmailDeliveryService`, court job refactors, claim/assignment/report emails, and POCSO-safe rendering. Alignment with Stories **7.2** (push parallel channel), **7.4** (no bell/deep links), and **8.5** (report hook only) is sound. The primary gap was **conflicting failure semantics**: AC4 mandated internal catch on all `TrySend*` methods, which would **break** Story **5.3**/**5.4** dedup behavior and existing integration tests (`ReminderJob_EmailFailure_DoesNotSetDedupFlag`, `MissEscalationJob_EmailFailure_DoesNotSetDedupFlag`).

| Check | Result |
|-------|--------|
| Epic AC (court, claims, assignments, reports email) | Pass — four event groups + report hook |
| Do not change auth OTP/reset emails | Pass |
| Push/in-app unchanged (7.2) | Pass |
| Bell UI deferred to 7.4 | Pass |
| Court email-before-save order | **Fix** — `Send*` methods propagate failures |
| Post-save best-effort email | Pass — `TrySend*` with internal catch |
| POCSO purpose omission | Pass — AC3 + integration test added |
| Preferences gating (7.1 stub) | Pass — with FR-16/assignment overrides |
| Constructor wiring documented | **Fix** — CaseService, TravelClaimService, court runners |
| Integration test regression | **Fix** — email failure dedup tests named |
| Wrong AC cross-reference | **Fix** — assignments table pointed to AC8 |
| UTC/amount format ambiguity | **Fix** — ISO `O` + `{amount:0.##}` locked |
| Initial create assignment | **Reconciled** — transfer only (Story 2.8) |
| OpenAPI / mobile / web | Pass — API-only |

---

## Fixes Applied

| # | Fix |
|---|-----|
| 1 | Split **`SendCourtReminderEmailsAsync`** / **`SendCourtMissEscalationEmailsAsync`** (pre-save, **propagate** `EmailDeliveryException`) from post-save **`TrySend*`** (internal catch) |
| 2 | AC4 rewritten — two method families; court jobs **must not** use `TrySend*` pre-save |
| 3 | AC6/AC7 reference `Send*` methods; name regression tests `ReminderJob_EmailFailure_DoesNotSetDedupFlag` / `MissEscalationJob_EmailFailure_DoesNotSetDedupFlag` |
| 4 | AC12 + testing section — preserve email-failure dedup integration tests |
| 5 | Document **constructor injection**: `CaseService`, `TravelClaimService`, court runners replace `IEmailSender` with `EmailDeliveryService` |
| 6 | Fix AC5 table cross-ref: assignee override **see AC10** (was AC8) |
| 7 | Lock UTC format to **ISO 8601 `O`**; amount format **`{amount:0.##}`** |
| 8 | `TrySendTravelClaimDecisionAsync` — use `NotificationEventTypes` constants, not raw strings |
| 9 | AC8 — reuse existing **`GetClaimantEmailAsync`** for submit template context |
| 10 | AC12 + dev notes — POCSO integration test; initial case create assignment out of scope |

---

## Remaining Notes (non-blocking)

- **Court miss emails** remain **Coordinators only** (matches Story **5.4** implementation; Directors not added).
- **Intervention overdue email** not in epic 7.3 list — correctly out of scope.
- **HTML / SendGrid** deferred — plain text + existing `SmtpEmailSender` sufficient.
- **Docker/Testcontainers** — integration tests require Docker; defer note if unavailable in dev session.
- **Shared template footer** — no deep links until Story **7.4**.

---

## Verdict

Story is **implementation-ready**. Run `bmad-dev-story` on `7-3-email-notification-templates`.

**Out of scope confirmed:** auth transactional emails, push/bell UI, HTML templates, SendGrid SDK, report export job (Epic **8.5**), initial case create assignment email, preferences PATCH persistence, new REST endpoints.
