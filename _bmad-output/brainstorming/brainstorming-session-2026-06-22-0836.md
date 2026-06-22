---
stepsCompleted: [1]
inputDocuments: []
session_topic: 'OWASP Security Gaps & Data Protection Requirements'
session_goals: 'Identify OWASP Top 10/ASVS/API Top 10 gaps in Midi-Kaval, define requirements for protecting personal/Case/PII data in storage and retrieval'
selected_approach: 'AI-Recommended Comprehensive Analysis'
techniques_used: ['Codebase Exploration (3 parallel agents)', 'OWASP Standards Benchmarking', 'GDPR Requirements Mapping']
ideas_generated:
  - 'Critical gaps identified'
  - 'High-priority security requirements'
  - 'Medium-priority security requirements'
  - 'Data protection requirements'
context_file: ''
---

# Brainstorming Session Results

**Facilitator:** Admin
**Date:** 2026-06-22

## Session Overview

**Topic:** OWASP Security Gaps & Data Protection Requirements for Midi-Kaval
**Goals:** Identify OWASP Top 10:2025 / ASVS / API Top 10 gaps, define requirements for protecting personal/Case/PII data

### Methodology

Three parallel codebase exploration agents analyzed:
1. **API Authentication & Security Architecture** — Auth flows, RBAC, rate limiting, error handling, audit logging
2. **Data Protection & Database Security** — PII fields, encryption, data flow, GDPR compliance, EF Core patterns
3. **Web Frontend & Mobile Security** — Token storage, interceptors, CSP, file uploads, route guards, error handling

All findings were benchmarked against:
- OWASP Top 10:2025
- OWASP ASVS 4.0 (Level 1 & 2)
- OWASP API Security Top 10 2023
- GDPR Articles 5, 25, 32 (Privacy by Design, Security of Processing)

---

## 1. OWASP Top 10:2025 Gap Analysis

### A01:2025 — Broken Access Control ⚠️ PARTIALLY ADDRESSED

| Requirement | Status | Evidence |
|---|---|---|
| Role-based access on all endpoints | ✅ Good | Policies defined for Director, Coordinator, FieldWorker, Accountant |
| Object-level authorization for data access | ⚠️ Partial | `EnsureCanReadCase` checks assignment, but many endpoints rely on org-level filtering only |
| Tenant isolation via OrganisationId | ✅ Good | Manual filtering on every query |
| Deny by default | ✅ Good | All endpoints require explicit `[Authorize(Policy = ...)]` |
| SSRF protection | ❌ **Missing** | No SSRF validation on external URLs (web hooks, presigned URL generation) |
| Mass assignment protection | ⚠️ Partial | DTOs limit exposed fields, but no explicit allowlist for PATCH operations |
| **Direct object reference prevention** | ⚠️ **Partial** | GUIDs used (not sequential IDs), but no secondary ownership check on related entities (e.g., case notes, attachments) |

### A02:2025 — Security Misconfiguration ⚠️ GAPS FOUND

| Issue | Severity | Location |
|---|---|---|
| **No CSP headers** | **HIGH** | Web app + backend — zero CSP anywhere |
| Development secrets in config | MEDIUM | `appsettings.Development.json` — plaintext SMTP/Gmail credentials, JWT placeholder key |
| Hardcoded DB password in design-time factory | MEDIUM | `AppDbContextDesignTimeFactory.cs` — fallback password in source |
| CORS allows all headers/methods | LOW | Standard SPA pattern, acceptable |
| No HTTPS in development | LOW | By design (local dev) |

### A03:2025 — Software Supply Chain Failures ⚠️ NOT ASSESSED

| Concern | Status |
|---|---|
| Dependency vulnerability scanning | ❌ Not observed |
| SBOM (Software Bill of Materials) | ❌ Not observed |
| Lockfile integrity | ⚠️ Partial (`package-lock.json` present for web) |
| NuGet package signature verification | ❌ Not configured |

### A04:2025 — Cryptographic Failures 🔴 CRITICAL GAPS

| Issue | Severity | Location |
|---|---|---|
| **No encryption at rest for PII** | **CRITICAL** | All `Case` PII fields (BeneficiaryName, BeneficiaryContact, GPS) in plaintext PostgreSQL |
| OTP hashing uses SHA256 without salt | MEDIUM | `OtpHasher.cs` — raw SHA256, no HMAC, no KDF (mitigated by short TTL + rate limiting) |
| JWT signed with HMAC-SHA256 (symmetric) | LOW | Acceptable for single-service architecture |
| TLS in transit | ✅ Good | HTTPS forced in production |
| HSTS enabled | ✅ Good | Production only |

### A05:2025 — Injection ✅ ADEQUATELY ADDRESSED

| Risk | Status | Evidence |
|---|---|---|
| SQL Injection | ✅ Protected | EF Core parameterized queries throughout |
| ILIKE search escaping | ✅ Good | Custom `ToILikeContainsPattern` escapes `\`, `%`, `_` |
| XSS (reflected/stored) | ✅ Safe | Angular auto-escaping in templates; no `innerHTML` usage |
| OS Command Injection | ✅ Not exposed | No system command execution |

### A06:2025 — Insecure Design ⚠️ NOTABLE

| Issue | Severity | Location |
|---|---|---|
| No threat model document | MEDIUM | Entire project — no formal threat model |
| No rate limiting on data endpoints | MEDIUM | Only auth endpoints are rate-limited (10/60s). Case search, export, CRUD have no limits |
| No CSRF protection | LOW | Mitigated by SameSite=Strict cookies + Bearer token pattern |
| No business logic abuse prevention | MEDIUM | No behavioral anomaly detection for sensitive flows |

### A07:2025 — Authentication Failures ✅ GENERALLY GOOD

| Measure | Status | Evidence |
|---|---|---|
| Two-factor (password + email OTP) | ✅ Good | Login requires OTP verification |
| Refresh token rotation with reuse detection | ✅ Excellent | Lua script detects reuse, invalidates ALL sessions |
| Token versioning for global revocation | ✅ Excellent | `TokenVersion` on User entity invalidates all JWTs |
| Brute-force protection | ✅ Good | Rate limiting on auth endpoints (10/60s per IP) |
| Password hashing (PBKDF2) | ✅ Good | ASP.NET Core Identity default (60K iterations, HMAC-SHA256, 128-bit salt) |
| Account lockout | ⚠️ Partial | OTP challenge auto-deletes after 5 failed attempts, but no permanent account lockout |
| Password complexity minimum | ⚠️ Partial | Min 8 chars only — no complexity requirements (uppercase, digit, special) |

### A08:2025 — Software or Data Integrity Failures ⚠️ GAPS

| Issue | Severity | Location |
|---|---|---|
| No integrity verification for NuGet packages | MEDIUM | No trusted-signer configuration |
| No Subresource Integrity (SRI) | MEDIUM | Angular `index.html` has no `integrity` attributes on external resources |
| No signature verification on uploaded files | LOW | Excel import files not signed |

### A09:2025 — Security Logging & Alerting Failures ⚠️ GAPS

| Issue | Severity | Location |
|---|---|---|
| Audit logging (71 event types) | ✅ Good | Comprehensive coverage of auth, case, visit, financial events |
| **No PII redaction in audit logs** | **HIGH** | Case merge events include full beneficiary details in metadata JSON |
| No alerting on suspicious activity | MEDIUM | No integration with SIEM or alerting system |
| No centralized log aggregation | MEDIUM | Logs go to console/stdout only |
| No failed login monitoring dashboard | MEDIUM | Login failures logged to audit, but no real-time monitoring |
| Who-viewed-which-case not audited (non-PII-reveal) | LOW | Only explicit PII reveal is audited |

### A10:2025 — Mishandling of Exceptional Conditions ✅ GOOD

| Measure | Status | Evidence |
|---|---|---|
| No stack trace leakage | ✅ Good | Global exception handler returns generic 500 with no Detail field |
| Consistent Problem Details (RFC 9457) | ✅ Good | All error responses use `application/problem+json` |
| Graceful degradation on auth failures | ✅ Good | 401 → auto-refresh; 403 → role-based messages |
| Custom exception types for domain errors | ✅ Good | `CaseNotFoundException`, `CaseForbiddenException`, etc. |

---

## 2. OWASP ASVS 4.0 Compliance Assessment

### Level 1 (Baseline — all applications)

| Chapter | Status | Key Gaps |
|---|---|---|
| V1 — Architecture | ⚠️ Partial | No formal threat model; no documented security architecture |
| V2 — Authentication | ✅ Good | Most requirements met (2FA, credential storage, token management) |
| V3 — Session Management | ✅ Good | Token rotation, revocation, versioning — all implemented |
| V4 — Access Control | ⚠️ Partial | Object-level checks inconsistent across endpoints |
| V5 — Validation & Sanitization | ✅ Good | EF Core parameterization, enum validation, length checks |
| V6 — Cryptography | ❌ **FAIL** | **No encryption at rest**; OTP hashing insufficient |
| V7 — Error Handling & Logging | ✅ Good | Clean error handling, comprehensive audit logging |
| V8 — Data Protection | ❌ **FAIL** | **No at-rest encryption, no data classification beyond POCSO, no retention policies** |
| V9 — Communication | ✅ Good | TLS 1.2+, HSTS |
| V10 — Malicious Code | ⚠️ Partial | No integrity verification for dependencies |
| V11 — Business Logic | ⚠️ Partial | No rate limiting on data endpoints |
| V12 — Files & Resources | ⚠️ Partial | Import file validation is extension-only |
| V13 — API & Web Services | ⚠️ Partial | No API schema validation enforcement |
| V14 — Configuration | ⚠️ Partial | Dev secrets in config; no CSP |

### Level 2 (Sensitive Data — Recommended for this Project)

**ASVS Level 2 failure items (in addition to Level 1 gaps):**

| Requirement | Gap |
|---|---|
| V2.5 — MFA for administrative access | ❌ Directors have 2FA (password + OTP), but no additional MFA for sensitive operations |
| V8.3 — Sensitive data in memory is protected | ❌ No evidence of `SecureString` or zeroing patterns |
| V8.7 — Data at rest is encrypted | ❌ **CRITICAL** — No PII encryption at rest |
| V8.10 — Data classification policy enforced | ❌ Only POCSO/Standard binary classification |
| V8.12 — Data retention and disposal | ❌ No automated data retention/deletion |
| V11.4 — Business logic rate limiting | ❌ Data endpoints unthrottled |

---

## 3. OWASP API Security Top 10 2023 Gaps

| Risk | Status | Gap |
|---|---|---|
| API1 — Broken Object Level Authorization | ⚠️ Partial | Org isolation is good; entity-level checks inconsistent |
| API2 — Broken Authentication | ✅ Good | 2FA, token rotation, rate limiting on auth |
| API3 — Broken Object Property Level Authorization | ⚠️ Partial | No explicit allowlist for which DTO properties each role can access |
| API4 — Unrestricted Resource Consumption | ❌ **GAP** | No payload size limits, no pagination caps beyond default, no execution timeouts |
| API5 — Broken Function Level Authorization | ✅ Good | Role-based policies on all endpoints |
| API6 — Unrestricted Access to Sensitive Business Flows | ❌ **GAP** | No bot detection, no behavioural analysis on sensitive flows (exports, bulk case creation) |
| API7 — Server Side Request Forgery | ❌ **GAP** | No SSRF validation on external URL interactions |
| API8 — Security Misconfiguration | ⚠️ Partial | CSP missing, CORS generous |
| API9 — Improper Inventory Management | ⚠️ Partial | No formal API inventory; versioning not enforced |
| API10 — Unsafe Consumption of APIs | ⚠️ Partial | No circuit breaker for downstream API calls (email, blob storage) |

---

## 4. Data Protection & PII Security — Requirements

### 4.1 PII Inventory (What Needs Protection)

| Field | Entity | Risk Level | Current Protection |
|---|---|---|---|
| BeneficiaryName | Case | **HIGH** | POCSO initials redaction (server-side) |
| BeneficiaryContact | Case | **HIGH** | Step-up OTP reveal for field workers |
| BeneficiaryAge | Case | MEDIUM | Step-up OTP reveal for field workers |
| GPS Coordinates | Case | MEDIUM | Visible to supervisors, step-up for field workers |
| Landmark | Case | MEDIUM | No special protection |
| User Email | User | MEDIUM | Plaintext in DB |
| User PhoneNumber | User | MEDIUM | Plaintext in DB |
| User FirstName/LastName | User | LOW | Plaintext in DB |
| Socio-demographic fields | Case | MEDIUM | No special protection |

### 4.2 Critical Data Protection Gaps

| # | Gap | Risk | OWASP Reference |
|---|---|---|---|
| 1 | **No encryption at rest for PII** | All beneficiary data exposed if DB compromised | A04:2025, ASVS V8.3 |
| 2 | **PII in audit log metadata** | Permanent record of PII in append-only audit table | A09:2025, ASVS V7 |
| 3 | **No data retention/deletion policy** | Cannot comply with right to erasure | ASVS V8.12, GDPR Art. 17 |
| 4 | **No data portability export** | Cannot provide user data in machine-readable format | GDPR Art. 20 |
| 5 | **No data classification beyond POCSO/Standard** | Cannot enforce tiered protection policies | ASVS V8.10 |
| 6 | **Excel export contains full PII** | Exports bypass redaction logic | A01:2025 |
| 7 | **SocioDemographicProfileReport contains full PII** | Report DTO includes Name, Age, Contact | A04:2025 |
| 8 | **No field-level access control in DB** | DB admin can read all PII | ASVS V8.3 |
| 9 | **Beneficiary names in duplicate match results** | Exposed to anyone checking duplicates | A01:2025 |
| 10 | **Web frontend has no discreet mode** | Coordinators see full PII without step-up on web | ASVS V2.5 |

---

## 5. Prioritized Security Requirements

### 🔴 Critical (Must Fix — Immediate)

| ID | Requirement | OWASP | Effort |
|---|---|---|---|
| SEC-01 | **Implement column-level encryption for PII fields** (BeneficiaryName, BeneficiaryContact) using AES-256 with key rotation. Use EF Core value converters or PostgreSQL `pgcrypto` extension. | A04:2025, ASVS V8.3 | Medium |
| SEC-02 | **Add Content Security Policy headers** — restrict `script-src`, `object-src`, `base-uri`, `form-action`. Deploy via backend middleware AND `index.html` meta tag. | A02:2025 | Small |
| SEC-03 | **Redact PII from audit log metadata** — strip BeneficiaryName, BeneficiaryContact, BeneficiaryAge from all audit event metadata JSON (especially CaseCreated, CaseMerged). Store reference-only identifiers. | A09:2025, ASVS V7 | Small |
| SEC-04 | **Strengthen OTP hashing** — replace raw SHA256 with HMAC-SHA256 using a per-challenge random key, or use a KDF (PBKDF2/bcrypt) with a 1-iteration minimum since OTPs are 6-digit. | A04:2025 | Small |

### 🟠 High (Should Fix — This Sprint)

| ID | Requirement | OWASP | Effort |
|---|---|---|---|
| SEC-05 | **Add rate limiting on all data endpoints** — case search, export, CRUD operations. At minimum 100 req/min per IP for read endpoints, 20 req/min for write endpoints. | API4:2023, A06:2025 | Medium |
| SEC-06 | **Strengthen import file validation** — add server-side MIME type verification for uploaded Excel files (not just extension check). Validate ZIP entry signatures within .xlsx. | A05:2025 | Small |
| SEC-07 | **Add payload size limits globally** — enforce `MaxRequestBodySize` on API endpoints (e.g., 10 MB upload, 1 MB JSON body). | API4:2023 | Small |
| SEC-08 | **Implement data retention policy for Case PII** — add `scheduled_deletion_at` column; background job to auto-anonymize cases older than configurable retention period (e.g., 7 years after case closure). | ASVS V8.12, GDPR Art. 17 | Large |
| SEC-09 | **Add right-to-erasure API endpoint** — `DELETE /api/v1/cases/{id}/personal-data` that nullifies all PII fields on the Case while preserving anonymized operational data. | GDPR Art. 17 | Medium |

### 🟡 Medium (Should Fix — Next Sprint)

| ID | Requirement | OWASP | Effort |
|---|---|---|---|
| SEC-10 | **Redact PII in Excel/PDF exports** — apply `BeneficiaryDisplayFormatter` to export paths for non-Director roles. | A01:2025 | Small |
| SEC-11 | **Redact PII in SocioDemographicProfileReport** — add sensitivity-aware redaction to `ChildListItemDto`. | A04:2025 | Small |
| SEC-12 | **Add data portability endpoint** — `GET /api/v1/cases/{id}/export-personal-data` returning JSON with all beneficiary PII for right-to-access compliance. | GDPR Art. 20 | Medium |
| SEC-13 | **Add discreet mode to web frontend** — mirror mobile's POCSO discreet header on the web case detail page, with step-up OTP for PII reveal. | ASVS V2.5 | Medium |
| SEC-14 | **Validate SMTP/enum endpoints for SSRF** — ensure presigned URL generation and email sending don't allow attacker-controlled URLs to be fetched server-side. | API7:2023 | Small |
| SEC-15 | **Add execution timeouts to API endpoints** — configure `CancellationToken` timeouts on long-running operations (exports, reports). | API4:2023 | Small |
| SEC-16 | **Add CSP `report-uri` or `report-to`** — configure violation reporting for CSP monitoring. | A02:2025 | Small |

### 🔵 Low (Nice to Have — Backlog)

| ID | Requirement | OWASP | Effort |
|---|---|---|---|
| SEC-17 | Add Subresource Integrity (SRI) hashes to all external resources in `index.html` | A08:2025 | Small |
| SEC-18 | Implement API versioning with deprecation headers | API9:2023 | Medium |
| SEC-19 | Add NuGet package signature verification in CI pipeline | A03:2025 | Small |
| SEC-20 | Generate formal threat model document (STRIDE per component) | A06:2025 | Large |
| SEC-21 | Implement account lockout after N failed login attempts (not just OTP failures) | A07:2025 | Small |
| SEC-22 | Add login anomaly detection — alert on login from new device/location for Director accounts | A07:2025 | Large |
| SEC-23 | Move all development secrets from `appsettings.Development.json` to .NET User Secrets (`secrets.json`) | A02:2025 | Small |
| SEC-24 | Add row-level security (RLS) in PostgreSQL for multi-tenant data isolation | A01:2025 | Large |
| SEC-25 | Implement field-level access control in DTO mapping (e.g., auto-redact PII for non-privileged roles) | API3:2023 | Medium |

---

## 6. Data Protection Architecture Recommendations

### 6.1 Encryption Strategy

```
┌────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                      │
│                                                          │
│  EF Core Value Converter (AES-256-GCM)                   │
│  ┌──────────────────────────────────────────┐            │
│  │ BeneficiaryName → Encrypted byte[]        │            │
│  │ BeneficiaryContact → Encrypted byte[]     │            │
│  │ BeneficiaryAge → Plaintext (low sensitivity)│          │
│  │ GPS → Encrypted byte[]                    │            │
│  │ User.Email → Encrypted byte[] (optional)   │            │
│  └──────────────────────────────────────────┘            │
│                                                          │
│  Key Management:                                         │
│  ┌──────────────────────────────────────────┐            │
│  │ Azure Key Vault / AWS KMS                │            │
│  │   └─ Master Key (KEK)                     │            │
│  │   └─ Per-tenant or per-org DEK           │            │
│  │ Dev fallback: User Secrets               │            │
│  └──────────────────────────────────────────┘            │
└────────────────────────────────────────────────────────┘
                          │
                          ▼
┌────────────────────────────────────────────────────────┐
│                   DATABASE LAYER                         │
│                                                          │
│  PostgreSQL:                                              │
│  ┌──────────────────────────────────────────┐            │
│  │ Column: beneficiary_name → bytea (enc)    │            │
│  │ Column: beneficiary_contact → bytea (enc) │            │
│  │ Column: gps → encrypted JSONB             │            │
│  │ TDE: pg_tde extension (optional)          │            │
│  │ SSL: enforce sslmode=require              │            │
│  └──────────────────────────────────────────┘            │
└────────────────────────────────────────────────────────┘
```

### 6.2 Audit Log PII Protection

```
Current (VULNERABLE):
  AuditEvent.MetadataJson = {
    "caseId": "...",
    "crimeNumber": "...",
    "beneficiaryName": "Ravi Kumar",        ← PII leaked
    "beneficiaryContact": "9876543210"       ← PII leaked
  }

Proposed (SECURE):
  AuditEvent.MetadataJson = {
    "caseId": "...",
    "crimeNumber": "..."
  }
  // PII reference stored only in Case entity (encrypted)
  // No personal data in append-only audit log
```

### 6.3 Data Retention Lifecycle

```
Case Created
    │
    ▼
[Active] ─── Normal operations, PII accessible
    │
    │ (Case reaches terminal state + retention period)
    ▼
[Scheduled for Anonymization]
    │
    ▼
[Anonymized] ─── PII fields set to NULL / anonymized
    │              • BeneficiaryName → NULL
    │              • BeneficiaryContact → NULL
    │              • GPS → NULL
    │              • Audit event: case.anonymized
    │
    │ (Optional: hard delete after extended period)
    ▼
[Deleted] ─── Entire case row soft-deleted or purged
```

---

## 7. Summary

### Current Security Posture Assessment

| Area | Grade | Key Strengths | Critical Gaps |
|---|---|---|---|
| Authentication | **B+** | 2FA, token rotation, PBKDF2 | OTP hashing (SHA256 no salt), no account lockout |
| Authorization | **B** | RBAC, org isolation, token versioning | Object-level checks inconsistent |
| Cryptography | **D** | TLS/HTTPS good | **No encryption at rest** — biggest gap |
| Data Protection | **D** | POCSO redaction, step-up reveal | No PII encryption, no retention, PII in audit logs |
| Error Handling | **A** | No stack leakage, Problem Details | — |
| Logging | **B-** | 71 event types | PII in logs, no alerting |
| Frontend Security | **B** | Angular auto-escaping, mobile keychain | **No CSP**, import validation weak |
| API Security | **B-** | Input validation, enum safety | No rate limiting on data endpoints, no SSRF protection |

### Immediate Action Items (Ordered by Priority)

1. **SEC-01** — Implement column-level PII encryption (AES-256-GCM via EF Core value converters)
2. **SEC-02** — Add Content Security Policy headers
3. **SEC-03** — Redact PII from audit log metadata
4. **SEC-04** — Strengthen OTP hashing with HMAC-SHA256
5. **SEC-05** — Add rate limiting to all data endpoints
6. **SEC-06** — Strengthen import file validation server-side
7. **SEC-08** — Implement data retention/anonymization policy
8. **SEC-09** — Add right-to-erasure API endpoint

---

## Next Steps

**Would you like to:**

1. **Create a security story** — turn these findings into structured user stories for the sprint backlog
2. **Drill deeper on a specific area** — e.g., encryption implementation patterns, CSP configuration, OTP hardening
3. **Generate a security checklist** — create a formal OWASP ASVS compliance checklist document
4. **Review deferred findings** — cross-reference with existing `deferred-work.md` security items
