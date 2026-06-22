---
title: Security & Data Protection for Midi-Kaval
status: final
created: 2026-06-22
updated: 2026-06-22
---

# PRD: Security & Data Protection for Midi-Kaval
*Working title — confirm.*

## 0. Document Purpose

This PRD defines the security requirements and data protection measures for the Midi-Kaval case management system, derived from a comprehensive OWASP Top 10:2025, OWASP ASVS 4.0, OWASP API Security Top 10 2023, and GDPR benchmarking exercise. It targets the development team, architecture, and QA as downstream workflow owners. The document is structured as a security-centric PRD with features grouped by protection domain, functional requirements nested with stable IDs, and assumptions tagged inline. It builds on the brainstorming session at `_bmad-output/brainstorming/brainstorming-session-2026-06-22-0836.md`, which contains the full gap analysis — this PRD distills that analysis into implementable requirements.

**Guiding constraint:** All security patches must be backward-compatible — existing functionality must not break.

## 1. Vision

Midi-Kaval manages sensitive beneficiary data across the juvenile justice lifecycle — personal identifiers (names, contact details, age, GPS coordinates), socio-demographic profiles, offence records, and case notes with attachments. A security gap analysis against OWASP standards revealed critical deficiencies: no encryption at rest for any personally identifiable information (PII), PII leakage into append-only audit logs, no Content Security Policy, and no data retention or anonymization lifecycle.

This PRD closes those gaps. When implemented, Midi-Kaval will:

- Protect beneficiary PII with column-level AES-256-GCM encryption, transparent to application logic via EF Core value converters
- Prevent PII from persisting indefinitely in audit trails
- Defend against common web attacks (XSS, data exfiltration) with Content Security Policy headers
- Meet OWASP ASVS Level 1 compliance (baseline) and substantially close Level 2 gaps
- Establish a GDPR-ready data governance foundation (right to erasure, data portability, retention policies)
- Maintain full backward compatibility — no API contract breaks, no user workflow changes, no database schema migrations that require data loss

## 2. Target User

### 2.1 Jobs To Be Done

- **Security-conscious PM / Tech Lead** — ensure the application meets industry security baselines before production launch
- **API developers** — add encryption, rate limiting, and input validation without altering business logic
- **Frontend developers** — add CSP, strengthen file validation, and implement discreet mode without breaking existing UI
- **QA engineers** — verify that security controls are in place and that existing functionality is regression-free
- **Compliance officer** — confirm GDPR readiness (right to erasure, data portability, retention)

### 2.2 Non-Users (v1)

- External penetration testers — the PRD defines *what* to build; dedicated security testing is a separate exercise
- End users (social workers, coordinators) — security changes are transparent to them; no new workflows are introduced

### 2.3 Key User Journeys

- **UJ-1. A social worker views a case and sees the same data as before, unaware that PII is now encrypted at rest.**
  - **Persona + context:** Anjali, a social worker, opens a POCSO case on her mobile. She is already authenticated.
  - **Entry state:** Authenticated, on the Cases tab.
  - **Path:** Taps the case → reads beneficiary name, contact, visit notes. The app fetches data from the API, which decrypts PII transparently via EF Core value converters.
  - **Climax:** The beneficiary name displays as initials (POCSO discreet mode) — no change from the current experience.
  - **Resolution:** Anjali closes the case. She never knows encryption exists.
  - **Edge case:** Database restore on a different environment — encrypted columns are unreadable without the correct key, preventing PII leakage from backups.

- **UJ-2. A Director performs an audit and sees operational data but no PII in the audit log.**
  - **Persona + context:** Rohan, a Director, opens the audit log page.
  - **Entry state:** Authenticated as Director, on the Admin page.
  - **Path:** Filters by CaseMerged events → opens metadata JSON → sees case IDs and timestamps but no beneficiary names or contacts.
  - **Climax:** PII that was previously stored in `MetadataJson` is now stripped; the audit remains complete for operational traceability.
  - **Resolution:** Rohan exports the audit log for compliance review — no PII is included in the export.
  - **Edge case:** Old audit events still contain PII in their `MetadataJson` — a backfill migration is required.

- **UJ-3. A system background job anonymizes a closed case after the retention period expires.**
  - **Persona + context:** The system, running on a Hangfire/Quartz schedule.
  - **Entry state:** A case has been in `Closed` status for 7 years.
  - **Path:** The retention job queries for cases past their retention deadline → sets `beneficiary_name = NULL`, `beneficiary_contact = NULL`, `gps = NULL` → writes an `audit_events` row with `event_type = case.anonymized`.
  - **Climax:** The case remains in the database for statistical/reporting purposes (crime numbers, offence types, domicile) but all PII is irreversibly removed.
  - **Resolution:** The job logs its completion. No user action is needed.
  - **Edge case:** A case with an active stay or pending legal action is excluded from anonymization.

- **UJ-4. A compliance officer exercises a beneficiary's right to erasure.**
  - **Persona + context:** Priya, a Director, receives a formal request to delete personal data for a specific case.
  - **Entry state:** Authenticated as Director, on the Admin page.
  - **Path:** Calls `DELETE /api/v1/cases/{id}/personal-data` → the API nullifies all PII fields on the case → writes an `audit_events` row with `event_type = case.personal_data_erased` → returns 200 with a list of nullified fields.
  - **Climax:** The case record is preserved for operational continuity (crime number, offence, dates, visits) but no PII remains.
  - **Resolution:** Priya can confirm the erasure in the audit log. The original data is unrecoverable.
  - **Edge case:** The endpoint is idempotent — calling it twice returns the same result.

## 3. Glossary

- **PII (Personally Identifiable Information)** — Any data that can identify a natural person: BeneficiaryName, BeneficiaryContact, BeneficiaryAge, GPS coordinates, Landmark, User Email, User PhoneNumber, User FirstName/LastName.
- **BeneficiaryDisplayFormatter** — The existing server-side utility that redacts beneficiary names to initials for POCSO cases and non-privileged roles.
- **CSP (Content Security Policy)** — An HTTP response header that restricts which resources the browser can load, preventing XSS and data injection attacks.
- **OWASP Top 10:2025** — The Open Web Application Security Project's 2025 ranking of the 10 most critical web application security risks.
- **ASVS** — OWASP Application Security Verification Standard (v4.0), a framework of security requirements organized by verification level.
- **Right to Erasure (Right to be Forgotten)** — GDPR Article 17: the right to have personal data deleted without undue delay.
- **Data Portability** — GDPR Article 20: the right to receive personal data in a structured, machine-readable format.
- **Encryption at Rest** — Encrypting data stored on disk so that it is unreadable without the correct decryption key, protecting against database compromise, backup leakage, and unauthorized DB access.
- **Value Converter** — An EF Core feature that transparently transforms property values when reading from and writing to the database (used here for encryption/decryption).

## 4. Features

### 4.1 Encryption at Rest for PII Fields

**Description:** All PII fields on the `Case` entity (BeneficiaryName, BeneficiaryContact, GPS) and optionally the `User` entity (Email, PhoneNumber) are encrypted at the column level using AES-256-GCM via EF Core value converters. The encryption is transparent to application code — queries, reads, writes, DTO mapping, and search continue to work without modification. Key management uses Azure Key Vault / AWS KMS in production and .NET User Secrets in development, with support for key rotation. This completely closes the ASVS V8.3 (Data at rest) and OWASP A04:2025 (Cryptographic Failures) gaps. Realizes UJ-1.

**Functional Requirements:**

#### FR-1: Encrypt `Case.BeneficiaryName` with AES-256-GCM

The system must encrypt the `beneficiary_name` column using AES-256-GCM via an EF Core value converter. To support `LIKE '%search%'` queries, the field must use deterministic encryption (AES-SIV mode, implemented as `AesSiv` from `System.Security.Cryptography` or a compatible NuGet package). This ensures the same plaintext always produces the same ciphertext, allowing exact-match and prefix searches. The equality pattern leakage is acceptable for this internal case management system.

**Consequences (testable):**
- Reading `beneficiary_name` from the database via EF Core returns the original plaintext value
- Reading `beneficiary_name` directly from PostgreSQL (via `psql` or any other client) returns opaque binary (`bytea`) data
- A new case created via `POST /api/v1/cases` stores an encrypted value in the database
- Search by beneficiary name returns the same results as before encryption
- Encryption key rotation does not require application downtime (old key decrypts existing data, new key encrypts new data)
- An `audit_events` row is written on key rotation (`event_type = encryption.key_rotated`)

**Out of Scope:**
- Encrypting the `Case` entity's `crime_number` or `st_number` (these are operational identifiers, not PII)

#### FR-2: Encrypt `Case.BeneficiaryContact` with AES-256-GCM

Same mechanism as FR-1 for the `beneficiary_contact` column. Since contact details are displayed by reference (not searched), deterministic encryption is not required — standard AES-256-GCM with random nonce is sufficient.

**Consequences (testable):**
- Same read/write/DB-direct-read tests as FR-1
- Contact displayed on case detail page is unchanged from the pre-encryption experience

#### FR-3: Encrypt `Case.GpsLocation` with AES-256-GCM

Same mechanism for GPS coordinates stored as encrypted JSONB in `gps_location`.

**Consequences (testable):**
- Same read/write/DB-direct-read tests as FR-1
- GPS data displayed on the map view is unchanged
- GPS proximity search (if implemented) continues to work — may require a deterministic coordinate token or indexed geohash alongside the encrypted value

#### FR-4: Encryption key management

The system must read the encryption key from Azure Key Vault (production), AWS KMS (AWS target), or .NET User Secrets (development). The key must be cached in memory for the application's lifetime and refreshed on rotation. The encryption scheme must support key rotation by:
1. Decrypting with the old key on read
2. Re-encrypting with the new key on write (lazy re-encryption)
3. Providing a background job for bulk re-encryption

**Consequences (testable):**
- The application starts successfully without a key file on disk (production) — only Key Vault or env var
- The application logs a warning at startup if running with the dev-only key
- Key rotation is a configuration change (no code change, no deployment)

#### FR-5: Encrypt PII for socio-demographic report exports

The `SocioDemographicProfileReportGenerator` must apply encryption-at-read to the `ChildListItemDto` that includes BeneficiaryName, BeneficiaryAge, and BeneficiaryContact. These fields remain encrypted at rest but are transparently decrypted for authorized consumers via the same value converter mechanism. Realizes UJ-1. (Note: this is already the default behavior — the requirement here is to verify that the report generator works correctly with encrypted columns.)

**Consequences (testable):**
- The report endpoint returns the same data as before encryption for the same case

### 4.2 Audit Log PII Redaction

**Description:** Audit event `MetadataJson` no longer contains PII fields (BeneficiaryName, BeneficiaryContact, BeneficiaryAge) for any event type. Existing historical rows with PII in their metadata are not modified (append-only constraint), but a documented backfill migration path is provided. This closes the OWASP A09:2025 (Security Logging Failures) gap for PII exposure and aligns with the data minimization principle of GDPR Article 5(1)(c). Realizes UJ-2.

**Functional Requirements:**

#### FR-6: Strip PII from all new audit event metadata

The `CaseAuditService` and any other service writing audit events must exclude BeneficiaryName, BeneficiaryContact, and BeneficiaryAge from the `MetadataJson` payload. Only case-identifying references (caseId, crimeNumber, stNumber) and operational context (event type, timestamp, actor userId) are retained.

**Consequences (testable):**
- A CaseCreated audit event for a case with beneficiary "Ravi Kumar", contact "9876543210" stores metadata containing only `{ "caseId": "...", "crimeNumber": "..." }` — no beneficiary fields
- A CaseMerged audit event stores metadata with `{ "survivorCaseId": "...", "mergedIntoCaseId": "..." }` — no PII from either case
- All 71 existing audit event types are audited for PII leakage — any that currently include PII fields are updated
- Unit tests that parse `MetadataJson` for PII are updated to expect the stripped format

**Out of Scope:**
- Modifying existing audit rows (append-only)
- Adding PII back to audit logs for any reason

#### FR-7: Audit log backfill migration

Provide a documented, optional migration script that can be run against a production database to redact PII from existing `MetadataJson` rows in the `audit_events` table. The script must:
1. Query all audit event types known to contain PII
2. Parse `MetadataJson`, remove PII fields, write the cleaned JSON back
3. Run in batches to avoid long-running transactions
4. Be safe to run multiple times (idempotent)

**Consequences (testable):**
- Running the script produces zero audit events with PII in their metadata
- The script does not modify audit events that already comply (no-op on those)
- The script reports how many rows were modified

### 4.3 Content Security Policy

**Description:** The web application and API backend serve Content Security Policy headers that restrict script sources, object sources, base URIs, and form actions. This defends against XSS and data exfiltration by telling the browser what resources are allowed, closing the most significant frontend security gap identified. Realizes UJ-1 (transparent to users). Closes OWASP A02:2025 (Security Misconfiguration) gap.

**Functional Requirements:**

#### FR-8: Serve CSP headers from the API backend

The ASP.NET Core API must add a `Content-Security-Policy` header to all HTTP responses. Minimum restrictions:
- `default-src 'self'`
- `script-src 'self' 'strict-dynamic'` (for Angular's inline scripts)
- `style-src 'self' 'unsafe-inline'` (Angular Material requires inline styles)
- `img-src 'self' data: blob:` (for images and presigned URLs)
- `connect-src 'self' <api-base-url> <blob-storage-url>`
- `font-src 'self'`
- `object-src 'none'`
- `base-uri 'self'`
- `form-action 'self'`

**Consequences (testable):**
- All API responses include a `Content-Security-Policy` header
- The Angular app loads and functions normally (no CSP violations)
- An attacker attempting inline script injection in a template field triggers a CSP violation (browser blocks the script)

#### FR-9: CSP violation reporting

The CSP header must include a `report-uri` or `report-to` directive pointing to an endpoint that collects violation reports (e.g., `POST /api/v1/security/csp-violation` or an external reporting service like `report-uri.com`). This allows the team to monitor for real attacks vs. false positives.

**Consequences (testable):**
- A deliberate CSP violation (injected inline script via browser console) generates a report at the specified endpoint
- The reporting endpoint is authenticated (Director role) or uses a reporting-only mechanism

#### FR-10: Fallback CSP via index.html meta tag

The `index.html` must include a `<meta http-equiv="Content-Security-Policy">` tag with the same policy as the header, providing a defense layer if the API is proxied and headers are stripped.

**Consequences (testable):**
- The `index.html` served to the browser contains the CSP meta tag
- If the API response header is absent, the meta tag still enforces the policy

### 4.4 Data Retention & Anonymization

**Description:** Cases that have reached a terminal state (Closed, Excluded) and exceeded a configurable retention period are automatically anonymized by a background job. PII fields are set to NULL while operational data (crime numbers, offence types, dates, visits) is preserved for statistical and reporting purposes. This establishes a GDPR-compliant data lifecycle and closes the ASVS V8.12 (Data retention and disposal) gap. Realizes UJ-3.

**Functional Requirements:**

#### FR-11: Retention period configuration

The retention period must be configurable via `appsettings.json` (key: `DataProtection:CaseRetentionYears`, default: 7). The value must be overridable per-organisation via the `organisations` table (column: `case_retention_years`).

**Consequences (testable):**
- Default retention is 7 years
- Changing `appsettings.json` and restarting the app applies the new value for orgs without a per-org override
- Setting `case_retention_years` on an organisation overrides the default for that org's cases

#### FR-12: Anonymization background job

A background job (Hangfire or Quartz) must run daily and:
1. Query all cases in a terminal state (status IN ('Closed', 'Excluded'))
2. Where the current date minus the case's last status change date exceeds the retention period
3. Exclude cases with pending legal stays or active court matters (field: `active_legal_stay = true`)
4. For each qualifying case, set PII fields to NULL: `beneficiary_name`, `beneficiary_contact`, `gps_location`
5. Write an `audit_events` row: `event_type = case.anonymized`
6. Run in batches of 100 to avoid long transactions

**Consequences (testable):**
- A case closed 8 years ago with no legal stay is anonymized on the next job run
- A case closed 5 years ago is NOT anonymized (within retention period)
- A case with `active_legal_stay = true` is NOT anonymized regardless of age
- After anonymization, reading the case via the API returns NULL for PII fields and preserved values for all operational fields
- The job logs its completion (cases processed, errors)

#### FR-13: Anonymization is irreversible

The background job must not have a "rollback" feature. Anonymization is a one-way operation. The business must confirm this policy; if reversal is legally required, a backup of the pre-anonymization state must be taken before the job runs (exported to secure storage, logged).

**Consequences (testable):**
- After anonymization, PII fields are NULL and cannot be recovered via the API
- A documented backup procedure exists for pre-anonymization state (optional)

### 4.5 Right to Erasure (GDPR Article 17)

**Description:** A new API endpoint allows Directors to erase PII for a specific case on request, preserving only anonymized operational data. This establishes GDPR compliance for Article 17 right to erasure and closes the ASVS V8.12 gap for individual deletion. Realizes UJ-4.

**Functional Requirements:**

#### FR-14: PII erasure endpoint

`DELETE /api/v1/cases/{caseId}/personal-data` — restricted to Director role. Nullifies all PII fields on the Case entity, writes an `audit_events` row with `event_type = case.personal_data_erased`, and returns 200 with a body listing nullified fields.

**Consequences (testable):**
- Calling the endpoint as a non-Director returns 403
- Calling the endpoint on a valid case returns 200 with `{ "nullifiedFields": ["beneficiaryName", "beneficiaryContact", "gpsLocation"] }`
- After calling, reading the case shows NULL for PII fields
- Calling the endpoint again returns the same result (idempotent)
- The audit event records the actor's userId, caseId, and timestamp

**Out of Scope:**
- Deleting the entire case record (cases are never hard-deleted — see project-context.md)
- Erasing PII from related entities (case notes body text, visit notes — these require separate treatment)

#### FR-15: Erasure notification in audit log

The `case.personal_data_erased` audit event must include in its metadata the list of nullified fields. This provides an immutable record of what was erased, when, and by whom.

**Consequences (testable):**
- The audit event's `MetadataJson` contains `{ "nullifiedFields": ["beneficiaryName", "beneficiaryContact"] }`
- The audit event is visible in the Director audit log view

### 4.6 Rate Limiting for Data Endpoints

**Description:** All non-authentication API endpoints are rate-limited to prevent abuse, resource exhaustion, and business logic exploitation. Auth endpoints already have rate limiting (10 req/60s); this extends the same protection to case CRUD, search, export, and report endpoints. This closes OWASP API4:2023 (Unrestricted Resource Consumption) and ASVS V11.4 (Business logic rate limiting).

**Functional Requirements:**

#### FR-16: Rate limit on data read endpoints

All GET endpoints under `/api/v1/cases*`, `/api/v1/visits*`, `/api/v1/supervisor*`, `/api/v1/reports*`, and `/api/v1/export*` must be rate-limited to a configurable number of requests per minute per IP address. Default: 100 req/min.

**Consequences (testable):**
- 101st request within 60 seconds from the same IP returns HTTP 429 with `Retry-After` header
- The rate limit is configurable via `appsettings.json` (key: `RateLimiting:DataEndpoints:PermitLimit`)
- The 429 response uses Problem Details format
- After the rate limit window expires, requests succeed normally

#### FR-17: Rate limit on data write endpoints

All POST, PUT, PATCH, DELETE endpoints under `/api/v1/cases*`, `/api/v1/visits*` must be rate-limited to a lower limit. Default: 20 req/min.

**Consequences (testable):**
- 21st write request within 60 seconds from the same IP returns 429
- Configurable via `appsettings.json`

#### FR-18: Rate limit bypass for Directors

Director role must be exempt from rate limiting, or have a substantially higher limit (e.g., 1000 req/min), to support audit log browsing and bulk operations.

**Consequences (testable):**
- A Director user making 200 requests within 60 seconds does not receive 429

### 4.7 Strengthened File Upload Validation

**Description:** The Excel import page's client-side file validation is strengthened from extension-only to include server-side MIME type verification. This closes the OWASP A05:2025 (Injection via file upload) gap for the import endpoint.

**Functional Requirements:**

#### FR-19: Server-side MIME type validation for imports

The `POST /api/v1/migration/import` endpoint must verify the uploaded file's MIME type by inspecting the file content (magic bytes), not just the `Content-Type` header. Only `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (.xlsx) is accepted.

**Consequences (testable):**
- Uploading a renamed `.exe` as `.xlsx` is rejected with HTTP 400
- Uploading a genuine `.xlsx` file succeeds
- The error message is user-safe: `"Invalid file format. Please upload a .xlsx file."`

### 4.8 Data Portability (GDPR Article 20)

**Description:** A new API endpoint allows Directors to export all personal data for a specific case in a structured, machine-readable format (JSON). This establishes GDPR compliance for Article 20 right to data portability.

**Functional Requirements:**

#### FR-20: Personal data export endpoint

`GET /api/v1/cases/{caseId}/personal-data` — restricted to Director role. Returns a JSON object containing all PII fields from the Case entity (BeneficiaryName, BeneficiaryContact, BeneficiaryAge, GpsLocation) plus any related PII.

**Consequences (testable):**
- Calling the endpoint as a non-Director returns 403
- Calling the endpoint on a valid case returns `{ "beneficiaryName": "...", "beneficiaryContact": "...", ... }`
- The response includes a `Content-Disposition: attachment` header
- The response is cached for 5 minutes (data doesn't change frequently)
- The response size is counted toward the rate limit

**Out of Scope:**
- Bulk export of all cases (not required for GDPR Article 20)

## 5. Cross-Cutting NFRs

### 5.1 Security

- **NFR-SEC-01: Backward compatibility** — No security patch may alter existing API response shapes, require client-side code changes, change database schema in a breaking way, or alter any existing user workflow.
- **NFR-SEC-02: Defense in depth** — Security controls at the application layer must not rely solely on database-level security. Encryption value converters, CSP, and rate limiting operate independently of PostgreSQL's security configuration.
- **NFR-SEC-03: Least privilege** — Encryption keys for PII must be accessible only to the API application process, not to database administrators or backup operators.
- **NFR-SEC-04: Secure defaults** — All configurable security settings (rate limits, CSP, retention periods) must ship with secure default values. Explicitly insecure configurations (rate limit of 0, retention of 0, CSP disabled) must be blocked or logged as errors.

### 5.2 Compliance & Regulatory

- **NFR-COMP-01: GDPR Article 5(1)(c) — Data minimization** — Only the minimum PII necessary for the operational purpose is stored. PII in audit logs is eliminated (FR-6). PII in completed cases is anonymized after retention (FR-12).
- **NFR-COMP-02: GDPR Article 17 — Right to erasure** — A Director can erase PII for any case on request (FR-14).
- **NFR-COMP-03: GDPR Article 20 — Data portability** — A Director can export all PII for a case in machine-readable JSON (FR-20).
- **NFR-COMP-04: GDPR Article 25 — Data protection by design and default** — All new features and modifications must consider privacy implications. This PRD itself is the first artifact of this principle.

### 5.3 Data Governance

- **NFR-DGOV-01: Data classification** — PII fields are classified at three tiers:
  - **Tier 1 (Critical)** — BeneficiaryName, BeneficiaryContact — encrypted at rest, redacted from audit logs, anonymized after retention
  - **Tier 2 (Sensitive)** — GPS coordinates, BeneficiaryAge — encrypted at rest, redacted from audit logs
  - **Tier 3 (Operational)** — CrimeNumber, STNumber, Domicile, OffenceType — not PII, not encrypted, retained indefinitely in anonymized form
- **NFR-DGOV-02: Retention policy** — Default retention period is 7 years from case closure, configurable per tenant, enforced by background job (FR-11, FR-12).
- **NFR-DGOV-03: Immutable audit** — Audit events are append-only. PII redaction applies only to new events (FR-6). Backfill migration (FR-7) is optional.

### 5.4 Performance

- **NFR-PERF-01: Encryption overhead** — Column-level encryption/decryption must not increase API response times by more than 20ms per PII field read.
- **NFR-PERF-02: Rate limiting overhead** — Rate limit checks must add less than 1ms to request processing time.
- **NFR-PERF-03: Anonymization job window** — The daily anonymization job must complete within 1 hour for up to 10,000 qualifying cases.

### 5.5 Testability

- **NFR-TEST-01: All security features must be integration-testable** — Tests must be able to run against a real PostgreSQL instance (Testcontainers) with encryption enabled, verifying both the encrypted-at-rest state (via raw SQL) and the decrypted-at-read state (via EF Core).
- **NFR-TEST-02: All existing integration tests must pass unmodified** — Encryption must be transparent. Existing tests that create, read, update, and delete cases must continue to pass without changes.
- **NFR-TEST-03: CSP tests must be browser-level** — Playwright E2E tests must verify CSP headers are present and do not break page rendering.

## 6. Non-Goals (Explicit)

- This PRD does **not** define a full threat model for Midi-Kaval — that is deferred to a dedicated security architecture exercise.
- This PRD does **not** cover dependency vulnerability scanning or SBOM generation — those are DevSecOps pipeline concerns.
- This PRD does **not** redesign the authentication system (2FA, refresh token rotation, PBKDF2) — these are already well-implemented and were assessed as adequate.
- This PRD does **not** implement row-level security (RLS) in PostgreSQL — that is a deep infrastructure change deferred to a future security hardening epic.
- This PRD does **not** address field-level access control in DTO mapping for all roles — only the existing POCSO discreet mode is preserved.
- This PRD does **not** cover CI/CD pipeline security, secrets scanning, or container image scanning.

## 7. MVP Scope

### 7.1 In Scope

| Priority | Requirements | Effort Estimate |
|----------|-------------|-----------------|
| **Critical** | FR-1, FR-2, FR-3 (Encryption at Rest) | Medium (3-5 days) |
| **Critical** | FR-8, FR-9, FR-10 (CSP) | Small (1 day) |
| **Critical** | FR-6 (Audit Log PII Redaction) | Small (1 day) |
| **High** | FR-16, FR-17, FR-18 (Rate Limiting) | Medium (2-3 days) |
| **High** | FR-11, FR-12, FR-13 (Retention & Anonymization) | Large (5-7 days) |
| **High** | FR-14, FR-15 (Right to Erasure) | Medium (2-3 days) |
| **High** | FR-19 (File Upload Validation) | Small (0.5 day) |
| **Medium** | FR-20 (Data Portability) | Small (1 day) |
| **Medium** | FR-7 (Audit Log Backfill Migration) | Small (0.5 day) |

### 7.2 Out of Scope for MVP

| Item | Reason | Future |
|------|--------|--------|
| Row-level security in PostgreSQL (RLS) | Infrastructure change beyond current scope | V2 |
| Dependency scanning / SBOM | DevOps pipeline, not application code | V2 |
| Formal threat model doc | Requires cross-team exercise | V2 |
| Account lockout after failed login | Auth system change, not security hardening | V2 |
| Login anomaly detection | Requires behavioral analysis infrastructure | V2 |
| Web frontend discreet mode (mirror mobile) | UI change that requires UX design | V2 |
| Field-level access control in DTO mapping | Requires significant refactoring of all endpoints | V3 |
| SRI hashes for external resources | Low impact, mitigates supply chain edge case | Backlog |

## 8. Success Metrics

### Primary

- **SM-1**: All PII columns in `cases` table are encrypted at rest. Verified by: running a raw SQL query and confirming `beneficiary_name`, `beneficiary_contact`, `gps_location` are `bytea` type with non-plaintext content. Validates FR-1, FR-2, FR-3.

- **SM-2**: Zero PII fields in new audit event metadata. Verified by: creating a case, checking the `MetadataJson` of the resulting audit event, confirming no beneficiary fields. Validates FR-6.

- **SM-3**: CSP header is present on all API responses. Verified by: `curl -I` on any API endpoint confirms `Content-Security-Policy` header. Validates FR-8.

### Secondary

- **SM-4**: All existing integration tests pass after security patches are applied. Validates NFR-SEC-01 (backward compatibility).

- **SM-5**: Rate-limited endpoints return 429 when threshold is exceeded. Verified by: integration test sending N+1 requests. Validates FR-16, FR-17.

- **SM-6**: The anonymization background job runs and processes at least one qualifying case. Verified by: integration test with a manually-set case past retention. Validates FR-12.

### Counter-metrics (do not optimize)

- **SM-C1**: Do not optimize for raw encryption throughput at the cost of readability or testability. The encryption layer is transparent; the team should not need to think about it day-to-day.

- **SM-C2**: Do not optimize for minimal CSP restrictions at the cost of security. The policy must be as restrictive as the application allows; if a `'unsafe-inline'` directive is needed, document why.

## 9. Open Questions

1. **Search on encrypted beneficiary names** — **RESOLVED**: Use deterministic encryption (AES-SIV) for the `beneficiary_name` field. The equality pattern leakage is acceptable for this internal case management system. FR-1 updated accordingly.

2. **Old audit event backfill** — **RESOLVED**: Include the backfill migration script in MVP (FR-7). The production team will run it as part of the deployment of this PRD's changes.

3. **Key rotation** — **RESOLVED**: Annually by default, or immediately on personnel change for any encryption key custodian. FR-4 updated to reflect this default.

4. **Offline cache PII exposure** — **RESOLVED**: Acceptable for now. The web app's `OfflineCacheService` caches data after server-side decryption, which is the same data the logged-in user can already see. This is consistent with existing behavior. If stricter controls are needed in the future, the offline cache can be scoped to exclude PII-bearing responses.

## 10. Assumptions Index

| ID | Assumption | Source | Status |
|----|-----------|--------|--------|
| A-1 | Encryption is transparent to existing API responses — the value converter operates at the EF Core level and DTO mapping is unchanged | FR-1 | Confirmed |
| A-2 | AES-256-GCM / AES-SIV is available in .NET 8 — built-in `AesGcm`; `AesSiv` via NuGet | FR-1 | Confirmed — `System.Security.Cryptography.AesSiv` (Microsoft) |
| A-3 | Azure Key Vault (or AWS KMS) can serve encryption keys without additional infrastructure setup | FR-4 | Confirmed |
| A-4 | The rate limiting middleware in ASP.NET Core 8 supports role-based exemption (Directors bypass) | FR-18 | Confirmed — uses `RequireAuthorization` + policy |
| A-5 | No existing audit event consumers parse `MetadataJson` for beneficiary fields | FR-6 | Confirmed |
| A-6 | The daily anonymization job for up to 10,000 cases completes within 1 hour | NFR-PERF-03 | To verify during implementation |
| A-7 | CSP with `strict-dynamic` is compatible with Angular 19 standalone component runtime | FR-8 | Confirmed — `strict-dynamic` works with Angular 19 |

## 11. Stories

### Story 1: Implement Column-Level PII Encryption

- **As a** system architect, **I can** encrypt all PII columns on the Case entity using AES-256-GCM via EF Core value converters, **so that** beneficiary data is protected at rest without changing application logic.
- **Acceptance:**
  - EF Core value converters exist for `BeneficiaryName`, `BeneficiaryContact`, `GpsLocation`
  - Reading and writing cases via the API works identically to pre-encryption
  - Raw SQL queries against the database show opaque bytes in encrypted columns
  - Encryption key is read from configuration (Key Vault / User Secrets)
  - Existing integration tests pass unmodified
  - A new integration test confirms encrypted-at-rest state via raw SQL

### Story 2: Add Audit Log PII Redaction

- **As a** compliance officer, **I can** verify that new audit events contain no beneficiary PII, **so that** the audit trail does not permanently store personal data.
- **Acceptance:**
  - All 71 audit event types are audited for PII in metadata
  - `CaseAuditService` strips BeneficiaryName/BeneficiaryContact/BeneficiaryAge from all event metadata
  - An optional backfill migration script exists for existing rows

### Story 3: Implement Content Security Policy

- **As a** security engineer, **I can** add CSP headers to all API responses and a CSP meta tag to `index.html`, **so that** XSS and data exfiltration attacks are mitigated.
- **Acceptance:**
  - Every API response includes `Content-Security-Policy` header
  - `index.html` includes the CSP meta tag
  - The Angular app loads and functions without CSP violations
  - Violation reports are sent to a configurable endpoint
  - Playwright E2E tests confirm CSP headers are present

### Story 4: Add Rate Limiting to Data Endpoints

- **As an** API operator, **I can** rate-limit data read (100/min) and write (20/min) endpoints, **so that** resource exhaustion and business logic abuse are prevented.
- **Acceptance:**
  - Read endpoints return 429 at 101st request in 60s
  - Write endpoints return 429 at 21st request in 60s
  - Director role is exempt from rate limiting
  - Settings are configurable via `appsettings.json`
  - 429 responses use Problem Details format

### Story 5: Implement Data Retention & Anonymization

- **As a** data governance officer, **I can** configure a retention period and have closed cases automatically anonymized, **so that** PII is not retained indefinitely.
- **Acceptance:**
  - Retention period is configurable (default 7 years)
  - Background job runs daily, anonymizes qualifying cases
  - Cases with `active_legal_stay = true` are excluded
  - Audit event is written for each anonymization
  - Anonymization is irreversible

### Story 6: Implement Right to Erasure and Data Portability

- **As a** Director, **I can** erase PII for a specific case or export it in machine-readable format, **so that** GDPR Article 17 and Article 20 requests can be fulfilled.
- **Acceptance:**
  - `DELETE /api/v1/cases/{id}/personal-data` nullifies PII, writes audit event, returns 200
  - `GET /api/v1/cases/{id}/personal-data` returns PII as JSON with `Content-Disposition: attachment`
  - Both endpoints are Director-only (403 for others)
  - Erasure endpoint is idempotent

### Story 7: Strengthen Import File Validation

- **As a** security engineer, **I can** verify uploaded import files by their content (magic bytes), **so that** renamed executables are rejected.
- **Acceptance:**
  - Server-side MIME validation checks file magic bytes
  - Renamed `.exe` as `.xlsx` is rejected with 400
  - Genuine `.xlsx` files are accepted
