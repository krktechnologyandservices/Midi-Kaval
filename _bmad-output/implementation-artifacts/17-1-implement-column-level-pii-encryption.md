# Story 17.1: Implement Column-Level PII Encryption

Status: ready-for-dev

## Story

As a system architect,
I want to encrypt all PII columns on the Case entity using AES-256-GCM via EF Core value converters,
So that beneficiary data is protected at rest without changing application logic.

## Acceptance Criteria

1. **Given** a running API with encryption configured
   **When** a new case is created via POST /api/v1/cases
   **Then** beneficiary_name, beneficiary_contact, and landmark are stored as encrypted bytea in PostgreSQL
   **And** reading the case via GET /api/v1/cases/{id} returns original plaintext values

2. **Given** an encrypted case in the database
   **When** a raw SQL query reads the beneficiary_name column
   **Then** the value is opaque binary data (not human-readable)

3. **Given** a search by beneficiary name
   **When** the search term matches exactly
   **Then** the correct case is returned (AES-SIV deterministic encryption)

4. **Given** the existing integration test suite
   **When** all tests are run
   **Then** they pass unmodified (encryption is transparent)

5. **Given** a key rotation configuration change
   **When** the application is restarted
   **Then** existing data is readable with the old key and new writes use the new key

6. **Given** the socio-demographic profile report endpoint
   **When** generating a report for a case with encrypted PII
   **Then** the exported data includes the decrypted plaintext values (FR-5)

## Tasks / Subtasks

- [ ] Create `Infrastructure/Encryption/EncryptionKeyProviderOptions.cs` — options class for key source config (AC: 1, 5)
  - [ ] SectionName = "EncryptionKey", Provider enum (UserSecrets/KeyVault/Environment), MasterKey string, KeyVaultUrl string
  - [ ] `ActiveKeyVersion` property (byte, default 0) — track which key version is current for rotation
- [ ] Create `Infrastructure/Encryption/EncryptionKeyProvider.cs` — singleton that resolves encryption key from configured source (AC: 1)
  - [ ] Returns 32-byte key (AES-256). Production: Azure Key Vault / env var. Dev: User Secrets. Test: hardcoded test key.
  - [ ] Reads `ActiveKeyVersion` from options for ciphertext prepend
- [ ] Create `Infrastructure/Encryption/PiiEncryptionConverter.cs` — AES-256-GCM ValueConverter<string?, byte[]?> (AC: 1, 2)
  - [ ] Encrypt: take plaintext string, prepend 1-byte key version + 12-byte random nonce, AES-256-GCM encrypt, return byte[]
  - [ ] Decrypt: parse key version byte, read nonce, decrypt, return string
  - [ ] Non-searchable — random nonce per encryption
  - [ ] Injects EncryptionKeyProvider (static or via thread-local — converter constructor is constrained by EF)
- [ ] Create `Infrastructure/Encryption/SearchablePiiEncryptionConverter.cs` — AES-SIV ValueConverter<string?, byte[]?> (AC: 3)
  - [ ] Deterministic: same plaintext → same ciphertext (for exact-match WHERE clauses)
  - [ ] Use `System.Security.Cryptography.AesSiv` (Microsoft NuGet package) or manual AES-SIV construction
- [ ] Create `Infrastructure/Encryption/GpsEncryptionConverter.cs` — AES-256-GCM ValueConverter<string?, byte[]?> for GPS data (AC: 1, 2)
  - [ ] Accepts a string (JSON like `{"lat": 12.345, "lng": 67.890}`) and returns encrypted bytea
  - [ ] Decrypts the bytea back to the JSON string
- [ ] Modify `Domain/Entities/Case.cs` — add un-mapped computed property for GPS + encrypt Landmark (AC: 1)
  - [ ] Add `[NotMapped] public string? GpsJson { get => SerializeGps(); set => DeserializeGps(value); }` — combines Latitude + Longitude into JSON for the converter
  - [ ] Keep existing `Latitude`/`Longitude` properties — they populate from GpsJson on read
  - [ ] `Landmark` stays as `string?` — will use its own value converter
- [ ] Modify `Infrastructure/Persistence/CaseConfiguration.cs` — add value converters to PII properties (AC: 1, 2)
  - [ ] beneficiary_name: `.HasConversion(new SearchablePiiEncryptionConverter()).HasColumnType("bytea")`
  - [ ] beneficiary_contact: `.HasConversion(new PiiEncryptionConverter()).HasColumnType("bytea")`
  - [ ] landmark: `.HasConversion(new PiiEncryptionConverter()).HasColumnType("bytea")`
  - [ ] gps_json (new property): `.HasConversion(new GpsEncryptionConverter()).HasColumnType("bytea")`
  - [ ] Latitude/Longitude: remove `.HasPrecision(9, 6)` — data flows through GpsJson now
- [ ] Add `Infrastructure/SecurityServiceCollectionExtensions.cs` — DI registration (AC: 1, 4)
  - [ ] Register EncryptionKeyProvider as singleton
  - [ ] Configure EncryptionKeyProviderOptions from IConfiguration
- [ ] Modify `Program.cs` — call security service registration (AC: 1)
  - [ ] Add `builder.Services.AddMidiKavalSecurity(builder.Configuration);` in the non-testing block (after line 133)
  - [ ] Ensure encryption key is logged as warning if using dev-only key
- [ ] Fix `Infrastructure/Cases/CaseService.cs` — remove broken ILike filter on beneficiary_name (AC: 3, 4)
  - [ ] Remove line 1025-1026 (`EF.Functions.ILike(c.BeneficiaryName, likePattern...)`) — bytea columns do not support ILike
  - [ ] Keep the `Contains` filters on CrimeNumber/StNumber and the ILike on BeneficiaryContact — see Dev Notes for migration strategy
- [ ] Generate EF Core migration for column type changes (AC: 1, 2)
  - [ ] `dotnet ef migrations add EncryptPiiColumns` — changes beneficiary_name, beneficiary_contact, landmark to bytea; removes Latitude/Longitude precision; adds gps_json bytea column
  - [ ] Verify migration does not drop or damage existing data
- [ ] Configure test encryption key in integration test setup (AC: 4)
  - [ ] Add `"EncryptionKey": { "Provider": "Environment", "MasterKey": "<base64-test-key>" }` to test `appsettings.json`
  - [ ] Or override `WebApplicationFactory` to inject test key programmatically
- [ ] Verify FR-5: socio-demographic report export with encrypted columns (AC: 6)
  - [ ] Create case with known PII values, call report export endpoint, confirm decrypted values in output
- [ ] Add integration test verifying encrypted-at-rest state (AC: 2)
  - [ ] Create case, then run raw SQL SELECT to confirm bytea content is not plaintext
- [ ] Add integration test verifying exact-match name search still works (AC: 3)
  - [ ] Create case with known name, search by exact name, confirm case is returned
- [ ] Verify existing integration tests pass (AC: 4)
  - [ ] Run full suite to confirm no regressions

## Dev Notes

### Architecture Compliance

This story implements architecture decisions **AD-01** (EF Core ValueConverters), **AD-02** (AES-256-GCM + AES-SIV), and **AD-03** (key prepended to ciphertext). See `architecture-security.md` Sections 2.1 and 2.2.

### Files to Create (NEW)

```
apps/api/Infrastructure/Encryption/
├── PiiEncryptionConverter.cs           # AES-256-GCM (non-searchable, for string? fields)
├── SearchablePiiEncryptionConverter.cs # AES-SIV (searchable — beneficiary_name only)
├── GpsEncryptionConverter.cs           # AES-256-GCM (for JSON-encoded GPS string)
├── EncryptionKeyProvider.cs            # Singleton key resolver
└── EncryptionKeyProviderOptions.cs     # Config model (Provider, MasterKey, KeyVaultUrl, ActiveKeyVersion)

apps/api/Infrastructure/
└── SecurityServiceCollectionExtensions.cs  # DI registration
```

### Files to Modify (UPDATE)

```
apps/api/Infrastructure/Persistence/CaseConfiguration.cs
  - line 25-27: BeneficiaryName: replace .HasMaxLength/IsRequired with
      .HasConversion(new SearchablePiiEncryptionConverter()).HasColumnType("bytea").IsRequired()
  - line 29-30: BeneficiaryContact: add .HasConversion(new PiiEncryptionConverter()).HasColumnType("bytea")
  - line 97-98: Landmark: add .HasConversion(new PiiEncryptionConverter()).HasColumnType("bytea")
  - line 91-95: Latitude/Longitude: remove .HasPrecision(9, 6) — data flows through GpsJson
  - NEW: builder.Property(c => c.GpsJson).HasColumnType("bytea").HasConversion(new GpsEncryptionConverter())
        .HasColumnName("gps_json")

apps/api/Domain/Entities/Case.cs
  - ADD [NotMapped] computed property:
      [NotMapped]
      public string? GpsJson
      {
          get => Latitude is not null && Longitude is not null
              ? $"{{\"lat\":{Latitude},\"lng\":{Longitude}}}"
              : null;
          set {
              if (value is not null) { /* parse JSON, set Latitude/Longitude */ }
          }
      }
  - Landmark remains string? (converter handles it)

apps/api/Infrastructure/Cases/CaseService.cs
  - line 1025-1026 (ApplySearchFilters): REMOVE this block:
      || EF.Functions.ILike(c.BeneficiaryName, likePattern, ILikeEscapeCharacter)
    Reason: beneficiary_name is bytea after encryption — ILike raises PostgreSQL error.
    The name filter falls back to the EF Core AES-SIV exact-match via Where clause.
    If the controller's SearchAsync builds a query with a Name filter, it hits the
    exact-match path. The generic Q search will still match on CrimeNumber and StNumber.

apps/api/Program.cs
  - In the non-testing block (around line 133): builder.Services.AddMidiKavalSecurity(builder.Configuration);

apps/api/Migrations/
  - NEW: EncryptPiiColumns migration
```

### Key Management Rules

| Environment | Key Source | Notes |
|-------------|-----------|-------|
| Production | `EncryptionKey:MasterKey` env var or Key Vault | Never in appsettings.json |
| Development | .NET User Secrets | `dotnet user-secrets set "EncryptionKey:MasterKey" "<base64>"` |
| Testing | Hardcoded in test setup | Must never be the same as production key |

`ActiveKeyVersion` in `EncryptionKeyProviderOptions` (default 0). On key rotation, increment the version in config — new ciphertexts get the new version, old ciphertexts are decodable via the version byte prepended to the payload.

Ciphertext format: `[1 byte key version][12 byte nonce][AES-GCM ciphertext][16 byte GCM tag]` = 29 bytes overhead per encrypted value. For AES-SIV, the format is `[1 byte version][AES-SIV ciphertext]` (SIV includes synthetic IV internally).

### Encryption Algorithm Details

- **AES-256-GCM** (non-searchable): Use `System.Security.Cryptography.AesGcm` (built into .NET 8)
  - 256-bit key, 96-bit nonce, 128-bit authentication tag
  - Random nonce each encryption → different ciphertext for same plaintext (secure)
  - Encrypt → prepend key version + nonce; Decrypt → parse version+nonce → decrypt

- **AES-SIV** (searchable): Use `System.Security.Cryptography.AesSiv` NuGet package (Microsoft)
  - Deterministic: same key + same plaintext → same ciphertext
  - Enables `WHERE beneficiary_name = @p0` to work naturally (EF Core generates parameterized query, value converter encrypts the parameter)
  - Note: does NOT support `LIKE '%search%'` — only exact/prefix match

- **GPS encryption approach**: `GpsEncryptionConverter` takes a JSON string `{"lat": 12.345, "lng": 67.890}` and encrypts with AES-256-GCM. The `Case.GpsJson` un-mapped computed property serializes the two `decimal?` fields into a single JSON string before the converter fires.

### NuGet Dependencies

- `System.Security.Cryptography.AesSiv` — add to `apps/api/*.csproj` (if not already present)

### Testing

- **Integration test pattern** (for encrypted-at-rest verification):
  ```csharp
  [Fact]
  public async Task BeneficiaryName_IsEncryptedAtRest()
  {
      // Arrange: create case via API
      var caseId = await CreateCaseAsync(...);

      // Act: read raw bytea from PostgreSQL
      var raw = await db.Database.SqlQueryRaw<byte[]>(
          "SELECT beneficiary_name FROM cases WHERE id = {0}", caseId).FirstAsync();

      // Assert: not plaintext (does not contain original text)
      var rawString = Encoding.UTF8.GetString(raw);
      Assert.DoesNotContain("Ravi", rawString);
  }
  ```

- **Test key setup**: Add a `WebApplicationFactory` override or test `appsettings.Testing.json` with:
  ```json
  {
    "EncryptionKey": {
      "Provider": "Environment",
      "MasterKey": "VGVzdEtleUZvckFJQVNJVjI1NkJpdHMyNTY="
    }
  }
  ```
  The test project must never use the same key as production/staging.

- **Existing test suite must pass unmodified** — this is NFR-SEC-01. Encryption is transparent.

### Potential Pitfalls

1. **Migration of existing data**: The `ALTER COLUMN ... TYPE bytea` migration will have NULL for existing rows initially. All new writes go through the converter. Existing data must be backfilled (Story 18.1 for audit logs, or a dedicated data migration). For this story, focus on new data — existing rows get encrypted on next write (lazy re-encryption pattern).

2. **Search impact**: After encryption, `LIKE '%search%'` no longer works on `beneficiary_name`. The `ApplySearchFilters` method in `CaseService.cs` must have its `ILike(c.BeneficiaryName, ...)` filter removed. Search falls back to `crimeNumber`, `stNumber`, `typeOfOffence` for substring queries. Exact name matching via AES-SIV still works (the value converter encrypts the search parameter for `=` comparison).

3. **Performance**: AES-NI makes each encrypt/decrypt < 0.001 ms. EF Core converter overhead ~0.05–0.2 ms per field. Total: ~0.6 ms for 4 fields (name, contact, landmark, GPS). See `performance-impact.md` for full analysis.

4. **Value converter constructor constraints**: EF Core value converters must be resolvable at model building time. `PiiEncryptionConverter` should use a static key reference (e.g., `EncryptionKeyProvider.GetCurrentKey()`) or be passed the key during registration, since DI injection is not available inside `HasConversion()`. The `EncryptionKeyProvider` singleton can expose a static accessor for this purpose.

5. **Latitude/Longitude dual storage**: After adding `GpsJson`, the same data exists in two places on the entity (individual properties + encrypted JSON). The individual `Latitude`/`Longitude` properties become un-mapped after migration. Update all existing code that reads/writes these properties — DTOs, `CaseService`, report generators, etc. — to go through the decrypted entity values (which are populated from `GpsJson` via the computed property setter).

### References

- [Source: architecture-security.md#21-pii-encryption--ef-core-value-converters]
- [Source: architecture-security.md#22-key-management-fr-4]
- [Source: architecture-security.md#31-pattern-value-converter-registration]
- [Source: prd.md#fr-1-fr-4-fr-5]
- [Source: performance-impact.md#1-pii-encryption]
- [Source: CaseConfiguration.cs lines 25-30, 91-98]
- [Source: Case.cs lines 12-14, 39-41]
- [Source: CaseService.cs lines 1025-1027 (ILike to remove)]
- [Source: Program.cs lines 53-135]
- [Source: AppDbContext.cs — modelBuilder.ApplyConfigurationsFromAssembly]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
