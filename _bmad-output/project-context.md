---
project_name: 'Midi-Kaval'
user_name: 'Admin'
date: '2026-06-13'
sections_completed:
  - technology_stack
  - language_rules
  - framework_rules
  - testing_rules
  - quality_rules
  - workflow_rules
  - anti_patterns
status: 'complete'
rule_count: 72
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

**Status:** Greenfield — pin exact versions in Epic 1 Story 1 scaffold. Until then, use these targets from `architecture.md`.

| Layer | Technology | Target |
|-------|------------|--------|
| API | ASP.NET Core | 8.x LTS |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 16+ |
| Cache | Redis | 7.x |
| Web | Angular PWA + Angular Material | 19+ |
| Mobile | React Native | 0.76+ |
| Mobile local DB | WatermelonDB or SQLite | Decide at scaffold |
| Jobs | Hangfire or Quartz | In-process with API host |
| Storage | Azure Blob (or S3-compatible) | Private container + SAS |
| Push | FCM + APNs | Via unified notification service |
| Email | SMTP or SendGrid | OTP + reminders |
| Maps | Google Maps SDK (mobile), Maps JS (web) | — |

**Monorepo layout (mandatory):**
`apps/api`, `apps/web`, `apps/mobile`, `packages/api-client`, `packages/shared-types`, `tests/`, `infra/docker-compose.yml`

**Contract hierarchy (read before coding):**
1. `_bmad-output/specs/spec-kaval-online/SPEC.md` — product WHAT
2. `_bmad-output/planning-artifacts/architecture.md` — technical HOW
3. `_bmad-output/planning-artifacts/epics.md` — implementation scope (v1 MVP = FR-1–FR-25)

**Version constraints for agents:**
- API and EF Core must stay on **.NET 8** — do not upgrade to preview SDKs
- Angular **standalone components + signals** for local UI state (not NgModules for new features)
- OpenAPI-generated `packages/api-client` is regenerated from API — never hand-edit generated files
- Single-org pilot v1; schema must remain **tenant-ready** (`organisation_id` on tenant-scoped tables)

---

## Critical Implementation Rules

### Language-Specific Rules

#### C# / ASP.NET Core (API)

- **Naming:** PascalCase types/methods; DB mapping uses snake_case columns via EF configuration
- **JSON serialization:** API responses use **camelCase** in JSON; C# properties remain PascalCase
- **IDs:** UUID v4 (`Guid`) in URLs and entities — no auto-increment public IDs
- **Timestamps:** Store and return **ISO 8601 UTC**
- **Errors:** Return **RFC 7807 Problem Details**
- **Authorization:** Policy-based `[Authorize(Policy = Policies.*)]`; **never** `[AllowAnonymous]` on data mutations
- **Business logic:** Domain rules in `Domain/` — not in Controllers, not duplicated in clients
- **Audit:** Every data-changing endpoint writes `audit_events` in the same transaction
- **HTTP status:** 400 validation, 401 unauthenticated, 403 RBAC, 404 not found, **409** duplicate crime/ST, **422** business rules

#### TypeScript — Angular Web (`apps/web`)

- TypeScript `strict`; no `any` except at generated client boundary
- Standalone components + **signals** for local UI state
- **Only** generated `packages/api-client` for HTTP — no raw fetch
- Auth/errors in `core/` interceptors — not per-feature
- Import from `@api-client` and `@shared-types` — not relative paths into `apps/api`

#### TypeScript — React Native Mobile (`apps/mobile`)

- Screens: `ScreenName.tsx` in `src/screens/`
- Generated `packages/api-client` only; sync uses `clientMutationId` for idempotent replay
- Offline: visits, visit notes, draft travel claims only via `POST /sync/push`
- Case create requires network (duplicate check)
- Conflict: server wins except notes merge by timestamp

### Framework-Specific Rules

#### ASP.NET Core API (`apps/api`)

- Structure: thin `Controllers/`; `Domain/` business rules; `Infrastructure/` EF/Redis/blob; `Jobs/` background work
- Routes: `/api/v1`, plural kebab-case resources
- Envelope: `{ "data": {}, "meta": { "requestId": "..." } }`
- Auth: JWT 15 min + refresh; token version on `users` for force-logout
- Dedicated endpoints: `GET /supervisor/crisis-queue`, `GET /visits/today`, `GET /supervisor/dashboard`
- Attachments: presign → PUT blob → confirm
- POCSO: list DTOs initials-only when `sensitivity_level = POCSO`

#### Angular PWA Web (`apps/web`)

- Features: `src/app/features/{feature}/`; core auth/guards in `src/app/core/`
- Angular Material + `DESIGN.md` semantic tokens (primary `#0D6E6E`)
- PWA: prefetch shell; freshness cache queue/dashboard (**read-only**); network-only mutations
- Web does **not** capture visits offline
- WCAG 2.2 AA; no gamification, modal stack >1, infinite scroll on visits

#### React Native Mobile (`apps/mobile`)

- Tabs: **Today · Cases · More**; pull-to-refresh on Today and Cases
- Today = Command Strip from `GET /visits/today`
- Sync chips: local/pending/synced/error with text labels
- POCSO discreet header: initials + crime number; expand after OTP re-entry
- Do **not** build Field Memory AI UI in v1 MVP

### Testing Rules

| Layer | Location | Tool |
|-------|----------|------|
| API unit | `tests/api.unit/` | xUnit |
| API integration | `tests/api.integration/` | WebApplicationFactory + Testcontainers PostgreSQL |
| Web | `apps/web/` | Jasmine + Angular Testing Library |
| Mobile | `apps/mobile/__tests__/` | Jest + RN Testing Library |
| E2E | `tests/e2e/` | Playwright (web critical paths) |

**Must test:** RBAC denial, duplicate 409, audit writes, sync idempotency, POCSO list DTOs, crisis-queue endpoint shape.

**Boundaries:** Unit = domain/validators/components; Integration = HTTP→DB; E2E = user-visible flows only.

### Code Quality & Style Rules

- Naming: DB snake_case plural; C# PascalCase; JSON camelCase; Angular kebab-case files; RN PascalCase screens
- Generated `packages/api-client/` — never hand-edit
- Map `DESIGN.md` tokens to Material theme — no hardcoded hex in components
- XML doc comments on Controllers for OpenAPI
- Do not add markdown docs unless a story requires it

### Development Workflow Rules

- BMad cycle: Sprint Planning → Create Story → Validate Story → Dev Story → Code Review
- v1 MVP = FR-1–FR-25 only
- Branches: `story/{story-id}-{short-slug}`; commits reference story ID
- Local: `docker-compose up` from `infra/`; API → regenerate client → web/mobile
- Do not edit PRD/architecture/epics during implementation without `bmad-correct-course`
- Do not change `H:\PayZenUI\WebAPI_Reference` — reference only

### Critical Don't-Miss Rules

**Never:**
- Compose Crisis Queue or Command Strip from generic case lists
- Duplicate business rules in clients
- Use UI hiding as security
- Offline case create or web visit capture
- Hard-delete cases
- Hand-edit generated API client
- Build v1.1 AI UI in v1 stories
- Add gamification or worker performance scoring

**Security:** POCSO initials in lists; SAS URLs 15 min; force-logout on role change; no public routes v1.

**Domain:** 409 on duplicate crime/ST; court miss hourly escalation; handoff whisper ≤7 days; server wins sync conflicts.

**Performance:** Search <2s; dashboard <60s fresh; Redis queue snapshot TTL 30s.

**AI guardrails (v1.1+):** 3-factor minimum; aggregates only on field UI; advisory-only; no punitive scoring.

---

## Usage Guidelines

**For AI Agents:**
- Read this file before implementing any code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- Update this file if new patterns emerge

**For Humans:**
- Keep this file lean and focused on agent needs
- Update when technology stack changes
- Review quarterly for outdated rules
- Remove rules that become obvious over time

Last Updated: 2026-06-13
