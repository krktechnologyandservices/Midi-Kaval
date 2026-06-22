---
stepsCompleted: [1, 2, 3, 4]
status: complete
completedAt: 2026-06-22
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-22/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/architecture-security.md
---

# Midi-Kaval - Security & Data Protection Epic Breakdown

## Overview

This document provides the epic and story breakdown for the Security & Data Protection initiative, decomposing the 20 functional requirements from the PRD and the 8 architectural decisions from the security architecture into implementable stories. All changes are backward-compatible infrastructure-layer patches.

## Requirements Inventory

### Functional Requirements

FR-1: Encrypt Case.BeneficiaryName with AES-256-GCM (AES-SIV for search support)
FR-2: Encrypt Case.BeneficiaryContact with AES-256-GCM
FR-3: Encrypt Case GPS fields (Latitude/Longitude) with AES-256-GCM
FR-4: Encryption key management with Azure Key Vault / User Secrets, lazy key rotation
FR-5: Verify socio-demographic report exports work correctly with encrypted columns
FR-6: Strip PII (BeneficiaryName, BeneficiaryContact, BeneficiaryAge) from all new audit event metadata
FR-7: Provide optional backfill migration script for existing audit rows with PII
FR-8: Serve CSP headers from the API backend on all HTTP responses
FR-9: CSP violation reporting endpoint for monitoring
FR-10: Fallback CSP via index.html meta tag
FR-11: Configurable retention period for case PII (default 7 years, per-org override)
FR-12: Daily background job to anonymize closed cases past retention period
FR-13: Anonymization is irreversible (one-way operation)
FR-14: DELETE /api/v1/cases/{id}/personal-data for Director PII erasure
FR-15: Erasure notification written to audit log
FR-16: Rate limit data read endpoints (100 req/min default)
FR-17: Rate limit data write endpoints (20 req/min default)
FR-18: Director role exempt from rate limiting
FR-19: Server-side MIME type validation (magic bytes) for import file uploads
FR-20: GET /api/v1/cases/{id}/personal-data for Director PII export

### Non-Functional Requirements

NFR-SEC-01: Backward compatibility — no breaking API changes, no client changes, existing tests pass unmodified
NFR-SEC-02: Defense in depth — encryption, CSP, rate limiting operate independently of DB-level security
NFR-SEC-03: Least privilege — encryption keys accessible only to API process, not DBAs
NFR-SEC-04: Secure defaults — all configurable security settings ship with safe values
NFR-PERF-01: Encryption overhead <20ms per PII field read (budget; real impact <1ms)
NFR-PERF-02: Rate limiting overhead <1ms per request
NFR-PERF-03: Anonymization job completes within 1 hour for up to 10,000 cases
NFR-COMP-01: GDPR Article 5(1)(c) — data minimization
NFR-COMP-02: GDPR Article 17 — right to erasure
NFR-COMP-03: GDPR Article 20 — data portability
NFR-TEST-01: All security features must be integration-testable with Testcontainers PostgreSQL
NFR-TEST-02: All existing integration tests must pass unmodified
NFR-TEST-03: CSP tests must be Playwright E2E browser-level

### Additional Requirements (from Architecture)

- AR-01: EF Core ValueConverter pattern for transparent PII encryption
- AR-02: AES-256-GCM for non-searchable fields; AES-SIV for searchable BeneficiaryName
- AR-03: Key version byte + nonce prepended to ciphertext for lazy key rotation
- AR-04: CSP middleware registered in Program.cs after UseExceptionHandler
- AR-05: Rate limiting policies added to existing AddRateLimiter() call, not a new middleware
- AR-06: Background job follows existing BackgroundService + JobRunner split pattern
- AR-07: PII stripped at audit event creation point (not in AuditService itself)
- AR-08: Magic byte check reads first 4 bytes of uploaded file stream

### FR Coverage Map

| Requirement | Epic | Story |
|-------------|------|-------|
| FR-1, FR-2, FR-3, FR-4, FR-5 | Epic-17: PII Encryption | 17-1 |
| FR-6, FR-7 | Epic-18: Audit PII Redaction | 18-1 |
| FR-8, FR-9, FR-10 | Epic-19: Content Security Policy | 19-1 |
| FR-16, FR-17, FR-18 | Epic-20: Rate Limiting | 20-1 |
| FR-11, FR-12, FR-13 | Epic-21: Data Retention & Anonymization | 21-1 |
| FR-14, FR-15, FR-20 | Epic-22: Erasure & Portability | 22-1 |
| FR-19 | Epic-23: File Validation | 23-1 |

## Epic List

### Epic 17: PII Encryption at Rest
Encrypt all beneficiary PII fields (BeneficiaryName, BeneficiaryContact, GPS) using AES-256-GCM via EF Core value converters, transparent to application logic. Includes key management with lazy rotation.
**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5

#### Story 17.1: Implement Column-Level PII Encryption

As a system architect,
I want to encrypt all PII columns on the Case entity using AES-256-GCM via EF Core value converters,
So that beneficiary data is protected at rest without changing application logic.

**Acceptance Criteria:**

**Given** a running API with encryption configured
**When** a new case is created via POST /api/v1/cases
**Then** the beneficiary_name, beneficiary_contact columns are stored as encrypted bytea in PostgreSQL
**And** reading the case via GET /api/v1/cases/{id} returns the original plaintext values

**Given** an encrypted case in the database
**When** a raw SQL query reads the beneficiary_name column
**Then** the value is opaque binary data (not human-readable)

**Given** a search by beneficiary name
**When** the search term matches exactly
**Then** the correct case is returned (AES-SIV deterministic encryption)

**Given** the existing integration test suite
**When** all tests are run
**Then** they pass unmodified (encryption is transparent)

**Given** a key rotation configuration change
**When** the application is restarted
**Then** existing data is readable with the old key and new writes use the new key

### Epic 18: Audit Log PII Redaction
Strip beneficiary PII (BeneficiaryName, BeneficiaryContact, BeneficiaryAge) from all new audit event metadata. Provide optional backfill migration for existing rows.
**FRs covered:** FR-6, FR-7

#### Story 18.1: Add Audit Log PII Redaction

As a compliance officer,
I want new audit events to contain no beneficiary PII,
So that the audit trail does not permanently store personal data.

**Acceptance Criteria:**

**Given** a case is created
**When** the CaseCreated audit event is written
**Then** its MetadataJson contains only caseId, crimeNumber, stNumber — no beneficiary fields

**Given** a case merge operation
**When** the CaseMerged audit event is written
**Then** its draftSnapshot excludes beneficiaryName, beneficiaryContact, beneficiaryAge

**Given** all 71 audit event types are audited
**When** any event that previously included PII is written
**Then** PII fields are absent from the metadata

**Given** the backfill migration script is run
**When** existing audit rows contain PII in metadata
**Then** those PII fields are removed
**And** the script reports how many rows were modified

### Epic 19: Content Security Policy
Add CSP headers to all API responses via middleware and a CSP meta tag to index.html. Includes violation reporting endpoint for monitoring.
**FRs covered:** FR-8, FR-9, FR-10

#### Story 19.1: Implement Content Security Policy

As a security engineer,
I want CSP headers on all API responses and a CSP meta tag in index.html,
So that XSS and data exfiltration attacks are mitigated.

**Acceptance Criteria:**

**Given** any API endpoint
**When** a response is returned
**Then** it includes a Content-Security-Policy header with directives for default-src, script-src, style-src, img-src, connect-src, font-src, object-src, base-uri, form-action

**Given** the Angular web app loads
**When** index.html is served
**Then** it contains a CSP meta tag matching the header policy

**Given** an attacker attempts inline script injection in a template field
**When** the page renders
**Then** the browser blocks the script (CSP violation)

**Given** a CSP violation occurs
**When** the browser sends a report to POST /api/v1/security/csp-violation
**Then** the violation is logged at Warning level under the CspReporter category

**Given** Playwright E2E tests
**When** they check response headers
**Then** CSP headers are present and the app renders correctly

### Epic 20: Rate Limiting for Data Endpoints
Rate-limit data read endpoints (100 req/min) and data write endpoints (20 req/min) with Director role exemption.
**FRs covered:** FR-16, FR-17, FR-18

#### Story 20.1: Add Rate Limiting to Data Endpoints

As an API operator,
I want data read and write endpoints rate-limited,
So that resource exhaustion and business logic abuse are prevented.

**Acceptance Criteria:**

**Given** a rate-limited data read endpoint (GET /api/v1/cases)
**When** 101 requests are made within 60 seconds from the same IP
**Then** the 101st request returns HTTP 429 with Retry-After header

**Given** a rate-limited data write endpoint (POST /api/v1/cases)
**When** 21 requests are made within 60 seconds from the same IP
**Then** the 21st request returns HTTP 429

**Given** a Director user
**When** they make 200 requests within 60 seconds
**Then** no request returns 429 (Director exemption)

**Given** the rate limit window expires
**When** a new request is made
**Then** the request succeeds normally

**Given** the 429 response
**When** inspected
**Then** it uses Problem Details format (application/problem+json)

### Epic 21: Data Retention & Anonymization
Automatically anonymize closed cases past configurable retention period (default 7 years) via daily background job. Irreversible one-way operation.
**FRs covered:** FR-11, FR-12, FR-13

#### Story 21.1: Implement Data Retention & Anonymization

As a data governance officer,
I want closed cases past their retention period to be automatically anonymized,
So that PII is not retained indefinitely.

**Acceptance Criteria:**

**Given** the retention period is configured as 7 years (default)
**When** a case has been in Closed status for 8 years
**Then** the daily anonymization job sets beneficiary_name, beneficiary_contact, gps_location to NULL
**And** an audit event (case.anonymized) records the operation

**Given** a case with active_legal_stay = true
**When** the anonymization job runs
**Then** the case is excluded and its PII is preserved

**Given** a case within the retention period (5 years closed)
**When** the anonymization job runs
**Then** the case is NOT modified

**Given** the anonymization job runs
**When** it completes
**Then** it logs the count of cases processed and any errors

**Given** a case has been anonymized
**When** reading it via the API
**Then** PII fields are NULL and all operational fields are preserved

### Epic 22: Right to Erasure & Data Portability
New Director-only API endpoints to erase personal data (DELETE) or export personal data (GET) for a specific case. GDPR Articles 17 and 20 compliance.
**FRs covered:** FR-14, FR-15, FR-20

#### Story 22.1: Implement Right to Erasure and Data Portability

As a Director,
I want to erase or export personal data for a specific case,
So that GDPR Article 17 and Article 20 requests can be fulfilled.

**Acceptance Criteria:**

**Given** an authenticated non-Director user
**When** they call DELETE /api/v1/cases/{id}/personal-data
**Then** they receive HTTP 403

**Given** an authenticated Director user
**When** they call DELETE /api/v1/cases/{id}/personal-data on a valid case
**Then** PII fields are nullified (beneficiaryName, beneficiaryContact, gpsLocation)
**And** the response is 200 with { "nullifiedFields": ["beneficiaryName", "beneficiaryContact", "gpsLocation"] }
**And** an audit event (case.personal_data_erased) records the operation

**Given** the erasure endpoint is called twice
**When** on the same case
**Then** the second call returns the same result (idempotent)

**Given** an authenticated Director user
**When** they call GET /api/v1/cases/{id}/personal-data on a valid case
**Then** the response contains all PII fields as JSON with Content-Disposition: attachment

**Given** a non-Director user
**When** they call GET /api/v1/cases/{id}/personal-data
**Then** they receive HTTP 403

### Epic 23: Import File Upload Validation
Server-side MIME type validation via magic byte inspection (PK\x03\x04) for the Excel import endpoint.
**FRs covered:** FR-19

#### Story 23.1: Strengthen Import File Validation

As a security engineer,
I want uploaded import files validated by their content (magic bytes),
So that renamed executables are rejected.

**Acceptance Criteria:**

**Given** an upload of a renamed .exe file (saved as .xlsx)
**When** POST /api/v1/migration/import is called
**Then** the request is rejected with HTTP 400
**And** the error message is "Invalid file format. Please upload a .xlsx file."

**Given** a genuine .xlsx file
**When** POST /api/v1/migration/import is called
**Then** the file is accepted and processed normally
