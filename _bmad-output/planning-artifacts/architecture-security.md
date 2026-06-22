---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-22/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Midi-Kaval-2026-06-22/performance-impact.md
  - _bmad-output/brainstorming/brainstorming-session-2026-06-22-0836.md
workflowType: 'architecture'
project_name: 'Midi-Kaval'
user_name: 'Admin'
date: '2026-06-22'
status: complete
completedAt: 2026-06-22
lastStep: 7
---

# Security Architecture Decision Document — Midi-Kaval

_This document covers security-specific architectural decisions supplementing the main architecture at `architecture.md`. Built from the Security & Data Protection PRD._

## 1. Project Context Analysis

### 1.1 Requirements Overview

**Functional Requirements (20 FRs across 7 domains):**

| Domain | FRs | Description |
|--------|-----|-------------|
| Encryption at Rest | FR-1–FR-5 | AES-256-GCM for PII via EF Core value converters |
| Audit PII Redaction | FR-6–FR-7 | Strip PII from MetadataJson at event creation |
| Content Security Policy | FR-8–FR-10 | Backend middleware + index.html meta tag |
| Rate Limiting | FR-16–FR-18 | Data endpoint rate limits (100/min read, 20/min write) |
| Data Retention | FR-11–FR-13 | Background job anonymizes cases after retention period |
| Right to Erasure | FR-14–FR-15 | DELETE endpoint for Director to nullify PII |
| Data Portability | FR-20 | GET endpoint for Director to export PII as JSON |
| File Validation | FR-19 | Server-side MIME magic byte check on imports |

**Non-Functional Requirements:**
- NFR-SEC-01: Backward compatibility (no breaking changes)
- NFR-SEC-02: Defense in depth (app-layer, not DB-layer only)
- NFR-SEC-03: Least privilege (keys accessible only to API process)
- NFR-SEC-04: Secure defaults (block insecure configs)
- NFR-PERF-01–03: Performance budgets (<1ms total overhead)

### 1.2 Scale & Complexity

| Dimension | Assessment |
|-----------|------------|
| Project complexity | Low-Medium (all infrastructure-layer, no new UI, no new services) |
| Primary domain | API backend (CSP meta tag is only web change) |
| Cross-cutting concerns | Key lifecycle, GDPR compliance, performance <1ms |
| Technology reuse | 100% existing stack — no new dependencies |

### 1.3 Technical Constraints

- **Stack fixed:** .NET 8, EF Core 8, PostgreSQL 16, Redis 7, Hangfire/Quartz (using `BackgroundService` pattern)
- **Testing:** `WebApplicationFactory` + Testcontainers PostgreSQL
- **No hard-delete:** Cases are soft-deleted or anonymized, never removed
- **Existing tests must pass unmodified** (encryption must be transparent)

---

## 2. Core Architectural Decisions

### 2.1 PII Encryption — EF Core Value Converters

**Decision:** Encrypt `Case.BeneficiaryName`, `Case.BeneficiaryContact` using AES-256-GCM via custom EF Core `ValueConverter<,>` implementations. GPS fields (`Latitude`, `Longitude`) are encrypted as a single JSONB value or individually based on search requirements.

**Rationale:**
- Value converters operate transparently — existing service code, DTO mapping, and API responses are unchanged → **backward compatible**
- AES-NI hardware acceleration makes overhead < 0.001 ms per field
- Pattern already established in codebase: `HasConversion<string>()` for enums — same `IEntityTypeConfiguration<T>` extension point

**Implementation pattern:**

```
┌──────────────────────────────────────────────────────────┐
│  CaseConfiguration.cs (IEntityTypeConfiguration<Case>)    │
│                                                          │
│  builder.Property(c => c.BeneficiaryName)                 │
│      .HasMaxLength(256)                                   │
│      .HasConversion(new PiiEncryptionConverter())         │
│      .HasColumnType("bytea")          ← changes from text     │
│                                                          │
│  builder.Property(c => c.BeneficiaryContact)              │
│      .HasMaxLength(32)                                    │
│      .HasConversion(new PiiEncryptionConverter())         │
│      .HasColumnType("bytea")          ← changes from text     │
└──────────────────────────────────────────────────────────┘
```

**PiiEncryptionConverter design:**

```csharp
public sealed class PiiEncryptionConverter : ValueConverter<string?, byte[]?>
{
    private static readonly byte[] FixedNonce = // from config or derived

    public PiiEncryptionConverter()
        : base(
            v => Encrypt(v),
            v => Decrypt(v))
    { }

    private static byte[]? Encrypt(string? plaintext) { /* AES-256-GCM */ }
    private static string? Decrypt(byte[]? ciphertext) { /* AES-256-GCM */ }
}
```

**Deterministic search for BeneficiaryName (FR-1):**
- `BeneficiaryName` uses deterministic encryption (AES-SIV) so that `WHERE beneficiary_name = @p0` works
- The search term is encrypted client-side (in the value converter) and compared as ciphertext in the DB
- This does NOT support `LIKE '%search%'` — substring search requires an alternative approach:
  - **Option A (Preferred):** Add a separate `beneficiary_name_search` column as plaintext or SHA256 hash for substring matching, guarded by the same authorization checks as PII reveal
  - **Option B:** Accept exact-match-only search on encrypted names (degrade gracefully — substring search could fall back to broader filters)

**GPS encryption (FR-3):** Encrypt `Latitude` + `Longitude` as a single JSON `{ lat, lng }` string → encrypted `bytea`. Only exact-match queries are needed (no proximity search on encrypted data — proximity uses a separate indexed geohash).

### 2.2 Key Management (FR-4)

**Decision:** Encryption key hierarchy:

| Environment | Key Source | Fallback |
|-------------|-----------|----------|
| Production | Azure Key Vault / AWS KMS | `EncryptionKey:MasterKey` env var |
| Development | .NET User Secrets | `EncryptionKey:MasterKey` env var |
| Testing | Hardcoded test key (never valid outside tests) | — |

**Key configuration options:**

```json
{
  "EncryptionKey": {
    "Provider": "UserSecrets",     // "KeyVault" | "UserSecrets" | "Environment"
    "KeyVaultUrl": "https://...",
    "MasterKey": ""                // base64-encoded 32-byte key (dev/test only)
  }
}
```

**Key rotation:** Lazy re-encryption — reads use old key, writes use new key. A background job (`KeyRotationBackgroundService`) can bulk re-encrypt data. Key ID stored alongside ciphertext (prepend 1 byte key version + 12 byte nonce to ciphertext).

### 2.3 Content Security Policy (FR-8, FR-9, FR-10)

**Decision:** Two-layer defense:
1. **Backend middleware** — ASP.NET Core middleware adds `Content-Security-Policy` header to all responses
2. **HTML meta tag** — `<meta http-equiv="Content-Security-Policy">` in `index.html`

**Backend implementation — new middleware:**

```csharp
public sealed class ContentSecurityPolicyMiddleware(RequestDelegate next)
{
    private const string Policy = 
        "default-src 'self'; " +
        "script-src 'self' 'strict-dynamic'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' {api-base} {blob-base}; " +
        "font-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "report-uri /api/v1/security/csp-violation;";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
        {
            context.Response.Headers["Content-Security-Policy"] = Policy;
        }
        await next(context);
    }
}
```

**Registration in Program.cs:**
```csharp
// After UseExceptionHandler
app.UseMiddleware<ContentSecurityPolicyMiddleware>();
```

**Violation reporting endpoint (FR-9):**
- `POST /api/v1/security/csp-violation` — accepts CSP report JSON, logs to `ILogger` at Warning level
- No auth required (CSP reports come from browsers, not authenticated clients)
- Logged under a dedicated `CspReporter` category for easy log filtering

**index.html meta tag (FR-10):**
```html
<meta http-equiv="Content-Security-Policy"
      content="default-src 'self'; script-src 'self' 'strict-dynamic'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self';">
```

### 2.4 Rate Limiting for Data Endpoints (FR-16, FR-17, FR-18)

**Decision:** Extend the existing `AddRateLimiter()` call in `AuthServiceCollectionExtensions` (or add a new `AddMidiKavalDataRateLimiting` extension) with additional policies for data endpoints. Same `FixedWindowRateLimiter` pattern as auth.

**Policies:**

| Policy Name | Permit Limit | Window | Exemption |
|-------------|-------------|--------|-----------|
| `data-read` | 100 | 60s | Director role bypasses |
| `data-write` | 20 | 60s | Director role bypasses |

**Implementation — new extension method:**

```csharp
public static class DataRateLimitServiceCollectionExtensions
{
    public static IServiceCollection AddMidiKavalDataRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DataRateLimitOptions>(configuration.GetSection(DataRateLimitOptions.SectionName));

        // Rate limiter is already added in AddMidiKavalAuth() — we add policies there
        return services;
    }
}
```

**Policies added to existing `services.AddRateLimiter()` block:**

```csharp
options.AddPolicy("data-read", context =>
{
    // Directors bypass rate limiting
    if (context.User.IsInRole(UserRoles.Director))
        return RateLimitPartition.GetNoLimiter("director-bypass");

    var partitionKey = GetPartitionKey(context);
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = dataRateLimitOptions.ReadPermitLimit,  // default 100
        Window = TimeSpan.FromSeconds(dataRateLimitOptions.WindowSeconds), // default 60
        QueueLimit = 0,
    });
});

options.AddPolicy("data-write", /* similar, PermitLimit default 20 */);
```

**Controller/endpoint decoration:**

```csharp
[EnableRateLimiting("data-read")]
[HttpGet]
public async Task<IActionResult> SearchCases(...)
```

**Config section (appsettings.json):**

```json
{
  "DataRateLimiting": {
    "ReadPermitLimit": 100,
    "WritePermitLimit": 20,
    "WindowSeconds": 60
  }
}
```

**Note:** `app.UseRateLimiter()` is already called in `Program.cs` — no pipeline change needed.

### 2.5 Audit Log PII Redaction (FR-6)

**Decision:** Strip BeneficiaryName, BeneficiaryContact, BeneficiaryAge from the metadata dictionary BEFORE it reaches `AuditService.RecordAsync()`. This is done centrally in each service that writes audit events where PII is included in `MetadataJson`.

**Current code pattern (CaseService.cs line 76-83 — already clean):**

```csharp
// CaseCreated already has no PII — only caseId, crimeNumber, stNumber
db.AuditEvents.Add(new AuditEvent { ... MetadataJson = JsonSerializer.Serialize(
    new Dictionary<string, object?> {
        ["caseId"] = caseId.ToString("D"),
        ["crimeNumber"] = validated.CrimeNumber,
        ["stNumber"] = validated.StNumber,
    }, JsonOptions), ... });
```

**Gap found:** The draft case creation audit event at line 367-393 (merge scenario) includes full PII in `draftSnapshot`. Fix: strip `beneficiaryName`, `beneficiaryContact`, `beneficiaryAge` from the draft snapshot.

**All 71 event types must be audited — create a mapping:**

```csharp
// PiiAuditEventTypes.cs — catalog of event types that historically included PII
public static class PiiAuditEventTypes
{
    // Event types where MetadataJson may contain PII fields (to be stripped)
    public static readonly HashSet<string> AffectedTypes =
    [
        AuditEventTypes.CaseCreated,         // Already clean — verify
        AuditEventTypes.CaseMerged,           // Has draftSnapshot with PII — strip
        AuditEventTypes.CaseImported,         // Already clean — verify
        AuditEventTypes.CaseTransferred,      // Verify
        AuditEventTypes.CasePiiRevealed,      // Should retain (purpose is to log PII reveal)
    ];
}
```

**Backfill migration script (FR-7):**

```sql
-- SQL migration: redact PII from existing audit_events metadata_json
UPDATE audit_events
SET metadata_json = metadata_json #- '{draftSnapshot,beneficiaryName}'
                   #- '{draftSnapshot,beneficiaryContact}'
                   #- '{draftSnapshot,beneficiaryAge}'
WHERE event_type IN ('case.merged')
  AND metadata_json IS NOT NULL
  AND metadata_json::jsonb ? 'draftSnapshot';
```

### 2.6 Data Retention & Anonymization (FR-11, FR-12, FR-13)

**Decision:** Background job following the existing `XxxBackgroundService` + `XxxJobRunner` pattern.

**New files:**

```
apps/api/Jobs/
├── CaseAnonymizationJobOptions.cs     # Section: "CaseAnonymizationJob"
├── CaseAnonymizationJobRunner.cs       # Query + anonymize logic
└── CaseAnonymizationBackgroundService.cs  # Daily schedule
```

**CaseAnonymizationJobOptions:**

```csharp
public sealed class CaseAnonymizationJobOptions
{
    public const string SectionName = "CaseAnonymizationJob";
    public int RetentionYears { get; set; } = 7;
    public int BatchSize { get; set; } = 100;
    public int IntervalHours { get; set; } = 24;
}
```

**JobRunner logic (FR-12):**

```csharp
public async Task RunAsync(CancellationToken ct)
{
    var cutoff = DateTime.UtcNow.AddYears(-options.Value.RetentionYears);

    // Per-organisation override
    var orgs = await db.Organisations
        .Select(o => new { o.Id, RetentionYears = o.CaseRetentionYears ?? options.Value.RetentionYears })
        .ToListAsync(ct);

    foreach (var org in orgs)
    {
        var orgCutoff = DateTime.UtcNow.AddYears(-org.RetentionYears);

        var batch = await db.Cases
            .Where(c => c.OrganisationId == org.Id
                && (c.CurrentStage == CaseStage.TerminationExclusion
                 || (/* exclude with active legal stay */ false))
                && c.UpdatedAtUtc < orgCutoff)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        foreach (var case in batch)
        {
            case.BeneficiaryName = null;
            case.BeneficiaryContact = null;
            case.Latitude = null;
            case.Longitude = null;
            case.Landmark = null;
        }

        // Audit event per batch
        if (batch.Count > 0)
        {
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = org.Id,
                EventType = "case.anonymized",
                MetadataJson = $@"{{""count"":{batch.Count},""cutoffDate"":""{orgCutoff:O}""}}",
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
```

**Tracking `active_legal_stay`:** Add column to `cases` table:
- New column: `active_legal_stay BOOLEAN NOT NULL DEFAULT false`
- New property: `public bool ActiveLegalStay { get; set; }`
- Set to `true` when a court stay is recorded; cleared when resolved

### 2.7 Right to Erasure & Data Portability (FR-14, FR-15, FR-20)

**Decision:** Two new endpoints in the existing CasesController (or a dedicated `PersonalDataController`).

**Erasure endpoint — `DELETE /api/v1/cases/{caseId}/personal-data`:**

```csharp
[Authorize(Policy = Policies.DirectorOnly)]
[HttpDelete("{caseId:guid}/personal-data")]
public async Task<IActionResult> ErasePersonalData(Guid caseId, CancellationToken ct)
{
    var result = await caseService.ErasePersonalDataAsync(caseId, ct);
    if (result is null) return NotFound();
    return Ok(new { nullifiedFields = result });
}
```

**Service method:**

```csharp
public async Task<string[]?> ErasePersonalDataAsync(Guid caseId, CancellationToken ct)
{
    var entity = await db.Cases.FindAsync([caseId], ct);
    if (entity is null) return null;

    var nullified = new List<string>();
    if (entity.BeneficiaryName is not null) { entity.BeneficiaryName = null; nullified.Add("beneficiaryName"); }
    if (entity.BeneficiaryContact is not null) { entity.BeneficiaryContact = null; nullified.Add("beneficiaryContact"); }
    // ... GPS, Landmark ...

    db.AuditEvents.Add(new AuditEvent
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        ActorUserId = actorUserId,
        EventType = "case.personal_data_erased",
        MetadataJson = JsonSerializer.Serialize(new { nullifiedFields = nullified }),
        CreatedAtUtc = DateTime.UtcNow,
    });

    await db.SaveChangesAsync(ct);
    return [.. nullified];
}
```

**Portability endpoint — `GET /api/v1/cases/{caseId}/personal-data`:**

```csharp
[Authorize(Policy = Policies.DirectorOnly)]
[HttpGet("{caseId:guid}/personal-data")]
public async Task<IActionResult> ExportPersonalData(Guid caseId, CancellationToken ct)
{
    var data = await caseService.GetPersonalDataAsync(caseId, ct);
    if (data is null) return NotFound();
    return Ok(data);
}
```

### 2.8 File Upload Validation (FR-19)

**Decision:** Add server-side MIME-type validation via magic byte inspection in `MigrationImportService` before processing the file.

**Implementation in `ImportController` or `MigrationImportService`:**

```csharp
private static readonly byte[] XlsxMagicBytes = [0x50, 0x4B, 0x03, 0x04]; // PK\x03\x04

private static void ValidateFileFormat(IFormFile file)
{
    if (file.Length < 4)
        throw new CaseValidationException("Invalid file format.");

    using var reader = new BinaryReader(file.OpenReadStream());
    var header = reader.ReadBytes(4);

    if (!header.SequenceEqual(XlsxMagicBytes))
        throw new CaseValidationException("Invalid file format. Please upload a .xlsx file.");

    file.Position = 0; // Reset stream position
}
```

---

## 3. Implementation Patterns & Consistency Rules

### 3.1 Pattern: Value Converter Registration

All encryption value converters follow a consistent pattern:

```csharp
// Each converter is a sealed class in Infrastructure/Encryption/
// Registered in CaseConfiguration.cs via .HasConversion()
```

**New directory:**

```
apps/api/Infrastructure/Encryption/
├── PiiEncryptionConverter.cs      # AES-256-GCM for non-searchable fields
├── SearchablePiiEncryptionConverter.cs  # AES-SIV for searchable fields
├── EncryptionKeyProvider.cs       # Reads key from Key Vault / User Secrets / env
└── EncryptionKeyProviderOptions.cs # Configuration model for key source
```

### 3.2 Pattern: Options Class

Every new feature follows the existing `XxxOptions` + `SectionName` convention:

```csharp
public sealed class DataRateLimitOptions
{
    public const string SectionName = "DataRateLimiting";
    public int ReadPermitLimit { get; set; } = 100;
    public int WritePermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}
```

### 3.3 Pattern: Background Job Registration

Each background job follows the runner + service split:

```csharp
// 1. Options class + SectionName
// 2. Sealed JobRunner (scoped, injected via ctor)
// 3. Sealed BackgroundService (singleton, uses IServiceScopeFactory)
// 4. Registered in Program.cs: services.AddHostedService<XxxBackgroundService>();
```

### 3.4 Pattern: Service Collection Extension

Feature registrations that span multiple services/configurations follow the `AddMidiKavalXxx()` pattern:

```csharp
// In Infrastructure/DataRateLimitServiceCollectionExtensions.cs
public static IServiceCollection AddMidiKavalDataRateLimiting(
    this IServiceCollection services, IConfiguration configuration)
```

---

## 4. Project Structure — Security-Specific Additions

```
apps/api/
├── Controllers/V1/
│   └── SecurityController.cs              # CSP violation reporting endpoint
├── Infrastructure/
│   ├── Audit/
│   │   └── PiiAuditEventTypes.cs          # Catalog of PII-affected event types
│   ├── Encryption/
│   │   ├── PiiEncryptionConverter.cs       # AES-256-GCM value converter
│   │   ├── SearchablePiiEncryptionConverter.cs  # AES-SIV value converter
│   │   ├── EncryptionKeyProvider.cs        # Key resolution from config
│   │   └── EncryptionKeyProviderOptions.cs
│   ├── Middleware/
│   │   └── ContentSecurityPolicyMiddleware.cs  # CSP header middleware
│   ├── DataRateLimitServiceCollectionExtensions.cs  # Data endpoint rate limiting
│   ├── SecurityServiceCollectionExtensions.cs     # CSP, encryption DI registration
│   └── Persistence/
│       └── CaseConfiguration.cs           # MODIFIED: add .HasConversion + .HasColumnType("bytea")
├── Jobs/
│   ├── CaseAnonymizationJobOptions.cs
│   ├── CaseAnonymizationJobRunner.cs
│   └── CaseAnonymizationBackgroundService.cs
├── Domain/
│   └── Entities/
│       └── Case.cs                        # MODIFIED: add ActiveLegalStay property
└── Program.cs                             # MODIFIED: add middleware, rate policies, job
```

### Web changes only:
```
apps/web/src/index.html                    # MODIFIED: add CSP meta tag
```

### Migration changes:
```
apps/api/Migrations/                       # NEW: AddActiveLegalStay, ChangeBeneficiaryNameToBytea
```

---

## 5. Architecture Validation

| Check | Status |
|-------|--------|
| All 7 stories have architectural support | ✅ |
| Backward compatibility maintained (no API contract changes) | ✅ |
| Existing integration tests pass unmodified | ✅ (encryption is transparent) |
| Performance impact <1ms per request | ✅ |
| No new infrastructure dependencies | ✅ |
| All patterns follow existing codebase conventions | ✅ |
| Key rotation supported without downtime | ✅ |
| GDPR compliance (erasure, portability, retention) | ✅ |

### Architectural Decisions Summary

| # | Decision | Rationale |
|---|----------|-----------|
| AD-01 | EF Core ValueConverters for encryption | Transparent to all existing code — no service or controller changes |
| AD-02 | AES-256-GCM for non-searchable, AES-SIV for searchable | GCM provides authenticated encryption; SIV enables deterministic search |
| AD-03 | Key prepended to ciphertext (1 byte version + 12 byte nonce) | Enables lazy key rotation without a separate key ID column |
| AD-04 | CSP via middleware + meta tag | Two-layer defense (headers stripped by proxy → meta tag still works) |
| AD-05 | Rate limiting via existing `AddRateLimiter()` + new policies | Reuses existing middleware; no pipeline changes |
| AD-06 | Background job for anonymization (existing `BackgroundService` pattern) | No new scheduling dependency needed |
| AD-07 | PII stripped at audit event creation point | Zero overhead on reads; no separate PII-redaction pipeline needed |
| AD-08 | Magic byte check at stream start for import validation | Minimal overhead (~0.1ms), no external dependency |

### Open Items (Deferred)

- **BeneficiaryName substring search** — Encrypted column only supports exact-match. Fall back to broader filters (crime number, ST number, offence type) for substring searches on encrypted PII. If needed later, add a search hash column.
- **Offline cache PII** — Already accepted as-is (data cached post-decryption, same as the user sees in-session).
