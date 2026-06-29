# Code Review Findings — Story 2.16 (Director 2FA Mandate & Recovery)

## Unified Triage

| # | Source | Classification | Title |
|---|---|---|---|
| 1 | edge+auditor | **patch** | `POST /api/v1/auth/verify-totp-login` controller endpoint is missing |
| 2 | blind+edge | **patch** | Admin `ResetTwoFactor` has no organisation-scope check |
| 3 | edge | **decision_needed** | Admin `ResetTwoFactor` doesn't check last-Director protection |
| 4 | blind+edge | **patch** | TOTP login has no per-user brute-force protection (no failed-attempt tracking) |
| 5 | blind+edge | **patch** | `GenerateProvisioningAsync` overwrites existing secret before verification |
| 6 | edge | **patch** | Vendor `ResetTwoFactor` has no organisation-scope check |
| 7 | blind | **patch** | `GetUserId()` throws unhandled `UnauthorizedAccessException` instead of returning 401 |
| 8 | blind+edge | **patch** | No binding between login password step and TOTP verification (no challenge/nonce) |
| 9 | blind | **patch** | `ChallengeId = Guid.Empty` sentinel value for TOTP login path |
| 10 | blind | **patch** | Inconsistent actor-ID resolution between admin and vendor controllers |
| 11 | blind+edge | **patch** | `KeyNotFoundException.Message` leaked to client |
| 12 | blind | **dismiss** | `[Authorize]` not visible on `ResetTwoFactor` — handled by class-level `[Authorize(Policy = Policies.DirectorOnly)]` |
| 13 | blind | **patch** | TOTP code length validated before trimming whitespace |
| 14 | blind | **defer** | `IsEnrolledAsync` only checks `TotpEnrolledAt` (partial-enrollment state) |
| 15 | blind | **dismiss** | Non-sealed service with virtual methods — intentional for testability |
| 16 | blind | **dismiss** | Missing XML docs on some endpoints — minor style, follow existing pattern |
| 17 | blind | **patch** | Unused `using OtpNet` in `AuthService.cs` |
| 18 | edge+auditor | **patch** | `require('qrcode-generator')` may break in strict ESM |
| 19 | edge | **patch** | `ResetTwoFactor` returns anonymous object instead of `ApiResponse<T>` |
| 20 | edge | **dismiss** | `IsSuspended` not checked in `VerifyTotpLoginAsync` — `IsSuspended = true` implies `IsActive = false` per domain logic |
| 21 | edge | **patch** | `TokenVersion` not checked in `VerifyTotpLoginAsync` |
| 22 | edge | **defer** | TOTP enrollment has no rotation/re-enrollment requirement |
| 23 | edge | **defer** | `TokenVersion` increment doesn't invalidate existing sessions (by design) |
| 24 | edge | **defer** | Unenrolled Directors hit 403 on actions — covered by `[Require2FA]` attribute, separate story |
| 25 | edge+auditor | **patch** | TOTP login state (`requiresTotp`, `totpUserId`) not persisted across page refresh |
| 26 | edge | **dismiss** | Inconsistent `AsNoTracking()` usage — intentional pattern |
| 27 | edge | **defer** | `Require2FA` filter does DB query on every request — pre-existing |
| 28 | auditor | **decision_needed** | Spurious `[Require2FA]` on Vendor `ResetTwoFactor` — Vendor already has `[Require2FA]` at class level |
| 29 | auditor | **defer** | 403→modal interception UI unwired — frontend UX, separate story |

## Totals

- **patch**: 16 items (fixable, unambiguous)
- **decision_needed**: 2 items (ambiguous, needs human input)
- **defer**: 5 items (real but not actionable in this change)
- **dismiss**: 5 items (false positives / handled elsewhere)
