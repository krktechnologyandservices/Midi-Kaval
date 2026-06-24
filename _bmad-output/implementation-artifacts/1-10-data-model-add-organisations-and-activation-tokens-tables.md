---
baseline_commit: 95726001c494272ed9cb082a89ebc8e12d74087e
---

# Story 1.10: Data Model — Add organisations and activation_tokens tables

Status: review

## Story

As a developer,
I want the database schema to support organisations and activation tokens with their relationships,
So that the Vendor bootstrap flow has a persisted foundation.

## Acceptance Criteria

1. **Given** the existing Midi-Kaval PostgreSQL database
   **When** the new migration is applied
   **Then** an `organisations` table exists with: `id` (UUID PK), `name` (varchar), `is_active` (boolean, default false), `created_at_utc` (timestamp)

2. **Given** the `organisations` table exists
   **When** the new migration is applied
   **Then** an `activation_tokens` table exists with: `id` (UUID PK), `organisation_id` (FK → organisations), `token_hash` (varchar, SHA-256), `target_email` (varchar), `expires_at_utc` (timestamp), `consumed_at_utc` (nullable timestamp), `created_at_utc` (timestamp)

3. **Given** the existing `users` table
   **When** the new migration is applied
   **Then** these columns are added: `is_suspended` (boolean, default false), `totp_secret` (nullable varchar), `totp_enrolled_at` (nullable timestamp)
   **And** the existing columns `organisation_id`, `role`, `token_version` are already present and remain unchanged

4. **Given** EF Core migrations
   **When** `dotnet ef migrations add` is run
   **Then** a new migration file is generated under `apps/api/Migrations/`
   **And** `dotnet ef database update` applies it against PostgreSQL without errors

5. **Given** existing seed data
   **When** the migration runs
   **Then** existing rows in `users` get default values for new columns (`is_suspended` = false, `totp_secret` = null, `totp_enrolled_at` = null)
   **And** the existing `Admin` seed user works without data loss

6. **Given** integration tests use `Testcontainers PostgreSQL`
   **When** the test fixture applies migrations
   **Then** all existing tests pass unmodified (no breaking schema changes)

7. **Given** the schema is applied
   **When** EF model entities are used in queries
   **Then** FK relationship between `activation_tokens.organisation_id` → `organisations.id` is enforced via `ON DELETE CASCADE`
   **And** FK relationship between `users.organisation_id` → `organisations.id` remains as existing (non-nullable, already enforced by the unique index on `(organisation_id, email)`)

## Tasks / Subtasks

- [x] **Add Organisation entity model** — `apps/api/Domain/Entities/Organisation.cs`
  - Properties: `Id` (Guid), `Name` (string), `IsActive` (bool, default false), `CreatedAtUtc` (DateTime)
  - Navigation: `ActivationTokens` (ICollection<ActivationToken>), `Users` (ICollection<User>)

- [x] **Add ActivationToken entity model** — `apps/api/Domain/Entities/ActivationToken.cs`
  - Properties: `Id` (Guid), `OrganisationId` (Guid), `TokenHash` (string), `TargetEmail` (string), `ExpiresAtUtc` (DateTime), `ConsumedAtUtc` (DateTime?), `CreatedAtUtc` (DateTime)
  - Navigation: `Organisation` (Organisation)

- [x] **Extend User entity model** — `apps/api/Domain/Entities/User.cs`
  - The User entity already has: `Id`, `OrganisationId` (Guid, non-nullable), `Email`, `FirstName`, `LastName`, `PhoneNumber?`, `Role`, `TokenVersion`, `PasswordHash`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`
  - **Add these new properties**: `IsSuspended` (bool, default false), `TotpSecret` (string?), `TotpEnrolledAt` (DateTime?)
  - Navigation already implied by `OrganisationId` — add `Organisation` navigation property

- [x] **Add EF Core configurations** — `apps/api/Infrastructure/Persistence/`
  - `OrganisationConfiguration.cs` — table `organisations`, snake_case columns, PK
  - `ActivationTokenConfiguration.cs` — table `activation_tokens`, FK to organisations with cascade delete, composite index on `(organisation_id, token_hash)`
  - Extend existing `UserConfiguration.cs` — add new column configs for `IsSuspended`, `TotpSecret`, `TotpEnrolledAt`; add FK relationship to organisations

- [x] **Verify DbContext discovery** — `apps/api/Infrastructure/Persistence/AppDbContext.cs`
  - DbContext already uses `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` — new configurations are auto-discovered
  - No manual wiring needed

- [x] **Generate EF Core migration** — run `dotnet ef migrations add AddOrganisationsAndActivationTokens` from `apps/api/`

- [x] **Add Integration Test for migration** — `tests/api.integration/UsersSchemaTests.cs`
  - Verify all tables/columns exist after migration
  - Verify FKs are enforced
  - Verify existing users retain data after migration

## Dev Notes

### Epic context

**Epic 1: Vendor Backstage & Organisation Bootstrap** — this is the foundational data model story for the entire Role Management & Registration System. All subsequent stories in Epics 1-6 depend on the tables and columns introduced here.

### Architecture compliance

This story follows the architecture defined in `_bmad-output/planning-artifacts/architecture-role-management.md`:

| Rule | Application |
|------|------------|
| DB tables | `snake_case` plural — `organisations`, `activation_tokens` |
| DB columns | `snake_case` — `target_email`, `consumed_at_utc`, `token_version` |
| C# types | PascalCase — `Organisation`, `ActivationToken`, `User.IsSuspended` |
| IDs | UUID v4 (`Guid`) |
| Timestamps | ISO 8601 UTC (`DateTime` with `Kind = Utc`) |
| FK naming | FK `organisation_id` to `organisations.id` |
| Existing seed | Preserve Admin user — new columns default values |

### Existing User entity (read before coding)

The `User` entity at `apps/api/Domain/Entities/User.cs` already has these properties — **do NOT re-add them**:

```csharp
public Guid Id { get; set; }
public Guid OrganisationId { get; set; }       // Already exists, non-nullable
public string Email { get; set; } = string.Empty;
public string FirstName { get; set; } = string.Empty;
public string LastName { get; set; } = string.Empty;
public string? PhoneNumber { get; set; }
public string Role { get; set; } = string.Empty;           // Already exists
public int TokenVersion { get; set; }                       // Already exists, default 0
public string PasswordHash { get; set; } = string.Empty;
public bool IsActive { get; set; } = true;
public DateTime CreatedAtUtc { get; set; }
public DateTime UpdatedAtUtc { get; set; }
```

**Add only:**

```csharp
public bool IsSuspended { get; set; }          // NEW — default false
public string? TotpSecret { get; set; }        // NEW
public DateTime? TotpEnrolledAt { get; set; }  // NEW
public Organisation Organisation { get; set; } = null!;  // NEW navigation
```

### Existing UserConfiguration.cs (read before coding)

Located at `apps/api/Infrastructure/Persistence/UserConfiguration.cs`. Currently configures:

```csharp
builder.ToTable("users");
builder.HasKey(u => u.Id);
builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
builder.Property(u => u.FirstName).HasMaxLength(128).IsRequired();
builder.Property(u => u.LastName).HasMaxLength(128).IsRequired();
builder.Property(u => u.PhoneNumber).HasMaxLength(30);
builder.Property(u => u.Role).HasMaxLength(32).IsRequired();
builder.Property(u => u.PasswordHash).IsRequired();
builder.Property(u => u.TokenVersion).HasDefaultValue(0);
builder.Property(u => u.IsActive).HasDefaultValue(true);
builder.HasIndex(u => new { u.OrganisationId, u.Email }).IsUnique();
```

**Add to `Configure` method:**

```csharp
builder.Property(u => u.IsSuspended).HasColumnName("is_suspended").HasDefaultValue(false);
builder.Property(u => u.TotpSecret).HasColumnName("totp_secret");
builder.Property(u => u.TotpEnrolledAt).HasColumnName("totp_enrolled_at");

builder.HasOne(u => u.Organisation)
    .WithMany(o => o.Users)
    .HasForeignKey(u => u.OrganisationId)
    .OnDelete(DeleteBehavior.Restrict);  // Restrict — prevent org deletion if users exist
```

### EF Configuration patterns for new entities

```csharp
// OrganisationConfiguration.cs
public class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
    }
}
```

```csharp
// ActivationTokenConfiguration.cs
public class ActivationTokenConfiguration : IEntityTypeConfiguration<ActivationToken>
{
    public void Configure(EntityTypeBuilder<ActivationToken> builder)
    {
        builder.ToTable("activation_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OrganisationId).HasColumnName("organisation_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(64);
        builder.Property(x => x.TargetEmail).HasColumnName("target_email").IsRequired().HasMaxLength(320);
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne(x => x.Organisation)
            .WithMany(o => o.ActivationTokens)
            .HasForeignKey(x => x.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.OrganisationId, x.TokenHash });
    }
}
```

### Auto-configuration discovery

The DbContext uses `ApplyConfigurationsFromAssembly`, so new `*Configuration.cs` files in `Infrastructure/Persistence/` are automatically picked up. **No changes needed** to `AppDbContext.cs` for configuration loading. However, you **should** add `DbSet<Organisation>` and `DbSet<ActivationToken>` properties to `AppDbContext` for convenience:

```csharp
public DbSet<Organisation> Organisations => Set<Organisation>();
public DbSet<ActivationToken> ActivationTokens => Set<ActivationToken>();
```

### Column naming convention

The existing database uses `snake_case` column names. The UserConfiguration does NOT use `.HasColumnName()` for its core properties, yet the migration produces `snake_case` columns. This means a naming convention or interceptor is registered (check `AppDbContext.OnModelCreating` or `Infrastructure/Persistence/` for a convention plugin). For new entities, use explicit `.HasColumnName("snake_case")` to be safe.

### Existing unique index

The `users` table already has a unique index on `(organisation_id, email)`. Do NOT modify or duplicate it. The new FK from `users.organisation_id` → `organisations.id` should use **Restrict** delete behavior to prevent orphaned users.

### Scope boundaries

| In scope (1.10) | Out of scope |
|-----------------|--------------|
| `organisations` table + EF entity | Vendor activation link generation (Story 1.11) |
| `activation_tokens` table + EF entity | HMAC signing logic (Story 1.11) |
| `users` table extensions (`is_suspended`, `totp_secret`, `totp_enrolled_at`) | First Director registration flow (Story 1.12) |
| EF Core migration | Any API endpoints or controllers |
| FK relationships (CASCADE / Restrict) | Seed data changes beyond defaults |

### Testing requirements

| Suite | Command | Expectation |
|-------|---------|-------------|
| .NET | `dotnet test Midi-Kaval.slnx` | All existing tests pass unmodified |
| Migration | Verify via integration test | Schema created, FKs enforced |

### Previous story intelligence (1.9)

- **Entity location**: Models live in `apps/api/Domain/Entities/` — not `Models/`
- **DbContext**: `AppDbContext` in `Infrastructure/Persistence/` — uses `ApplyConfigurationsFromAssembly` (auto-discovery)
- **Migration location**: `apps/api/Migrations/` — naming pattern: `YYYYMMDDHHMMSS_Description.cs`
- **Testcontainers**: Integration tests use `WebApplicationFactory` with Testcontainers PostgreSQL — migrations auto-applied
- **Seed data**: `Infrastructure/Seed/DatabaseInitializer.cs` — runs in dev only; existing `AdminUserSeeder.cs` uses config values

### Validation note

This story was validated against the actual codebase. The `User` entity already has `OrganisationId`, `Role`, and `TokenVersion` — those columns exist from the initial migration. Only `IsSuspended`, `TotpSecret`, `TotpEnrolledAt` are genuinely new on the `users` table.

### File structure

```
apps/api/
├── Domain/Entities/
│   ├── Organisation.cs                      # NEW
│   ├── ActivationToken.cs                   # NEW
│   └── User.cs                              # UPDATE — add IsSuspended, TotpSecret, TotpEnrolledAt, Organisation nav
├── Infrastructure/Persistence/
│   ├── OrganisationConfiguration.cs         # NEW
│   ├── ActivationTokenConfiguration.cs      # NEW
│   ├── UserConfiguration.cs                 # UPDATE — add new column configs, FK to organisations
│   └── AppDbContext.cs                      # UPDATE — add DbSet<Organisation>, DbSet<ActivationToken>
├── Migrations/
│   └── {timestamp}_AddOrganisationsAndActivationTokens.cs  # GENERATED
tests/api.integration/
└── SchemaMigrationTests.cs                  # NEW — verify schema
```

### Definition of Done

- [ ] `Organisation` entity model created at `Domain/Entities/Organisation.cs`
- [ ] `ActivationToken` entity model created at `Domain/Entities/ActivationToken.cs`
- [ ] `User` entity extended with `IsSuspended`, `TotpSecret`, `TotpEnrolledAt`, `Organisation` nav
- [ ] EF Core configurations for all new/changed entities
- [ ] `AppDbContext` updated with new `DbSet<>` properties
- [ ] EF Core migration generated and verified
- [ ] All existing tests pass
- [ ] Integration test verifies new schema existence
- [ ] FKs enforced with correct delete behavior
- [ ] Verified against actual codebase — no duplicate columns

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 1.1]
- [Source: `_bmad-output/planning-artifacts/architecture-role-management.md` — §Data Architecture, §EF Configuration patterns]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — existing EF patterns]
- [Source: `_bmad-output/project-context.md` — snake_case, UUID, ISO 8601 conventions]
- [Source: `apps/api/Domain/Entities/User.cs` — existing entity, verified via codebase exploration]
- [Source: `apps/api/Infrastructure/Persistence/UserConfiguration.cs` — existing config, verified]
- [Source: `apps/api/Infrastructure/Persistence/AppDbContext.cs` — ApplyConfigurationsFromAssembly pattern, verified]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Created `Organisation` entity model at `Domain/Entities/Organisation.cs` with navigation collections for `ActivationTokens` and `Users`
- Created `ActivationToken` entity model at `Domain/Entities/ActivationToken.cs` with FK to `Organisation`
- Extended `User` entity with `IsSuspended`, `TotpSecret`, `TotpEnrolledAt`, and `Organisation` navigation property
- Created `OrganisationConfiguration.cs` — table `organisations`, PK, snake_case columns, defaults
- Created `ActivationTokenConfiguration.cs` — table `activation_tokens`, FK cascade to organisations, composite index on `(organisation_id, token_hash)`
- Updated `UserConfiguration.cs` — added `IsSuspended` (default false), `TotpSecret`, `TotpEnrolledAt` configs, FK to organisations with `Restrict` delete behavior
- Updated `AppDbContext.cs` — added `DbSet<Organisation>` and `DbSet<ActivationToken>`
- Generated EF Core migration `20260623182928_AddOrganisationsAndActivationTokens` — creates `organisations`, `activation_tokens` tables, adds `is_suspended`, `totp_secret`, `totp_enrolled_at` columns to `users`, adds FKs with cascade/restrict
- Modified the generated migration to include SQL seed for a default organisation and existing-user backfill, enabling safe FK application against existing data
- Updated `AdminUserSeeder.cs` to self-heal — creates the pilot organisation if missing before seeding the Director user (needed for new FK constraint)
- Updated `FieldWorkerUserSeeder.cs` to self-heal — same pattern for field worker accounts
- Updated `UsersSchemaTests.cs` — added 5 new tests for organisations/activation_tokens schema, new User columns with defaults, default org seed, FK enforcement, and Restrict on org deletion; updated existing table-list test and truncation list
- Build verified: API and test project both compile with 0 errors
- Integration tests skipped locally: Docker Desktop required for Testcontainers (PostgreSQL container)

### File List

- apps/api/Domain/Entities/Organisation.cs (NEW)
- apps/api/Domain/Entities/ActivationToken.cs (NEW)
- apps/api/Domain/Entities/User.cs (UPDATE)
- apps/api/Infrastructure/Persistence/OrganisationConfiguration.cs (NEW)
- apps/api/Infrastructure/Persistence/ActivationTokenConfiguration.cs (NEW)
- apps/api/Infrastructure/Persistence/UserConfiguration.cs (UPDATE)
- apps/api/Infrastructure/Persistence/AppDbContext.cs (UPDATE)
- apps/api/Infrastructure/Seed/AdminUserSeeder.cs (UPDATE)
- apps/api/Infrastructure/Seed/FieldWorkerUserSeeder.cs (UPDATE)
- apps/api/Migrations/20260623182928_AddOrganisationsAndActivationTokens.cs (GENERATED)
- apps/api/Migrations/20260623182928_AddOrganisationsAndActivationTokens.Designer.cs (GENERATED)
- tests/api.integration/UsersSchemaTests.cs (UPDATE)

### File List

### Review Findings (2026-06-24)

#### Patch

- [x] [Review][Patch] Organisation.IsActive default should be `false` per spec `apps/api/Domain/Entities/Organisation.cs:5`
- [x] [Review][Patch] Organisation.CreatedAtUtc needs `HasDefaultValueSql("NOW()")` to prevent `0001-01-01` timestamps `apps/api/Infrastructure/Persistence/OrganisationConfiguration.cs`
- [x] [Review][Patch] Migration default org UUID should match seeder pilot org UUID or vice versa `apps/api/Migrations/20260623182928_AddOrganisationsAndActivationTokens.cs`

#### Deferred

- [x] [Review][Defer] Auth middleware doesn't check `IsSuspended` — deferred to Story 2.4 (User Suspension and Reactivation)
- [x] [Review][Defer] Suspended users still receive notifications — deferred to Story 2.4
- [x] [Review][Defer] Staff DTO doesn't expose `IsSuspended` — deferred to Story 2.x
- [x] [Review][Defer] Deactivate/Reactivate don't interact with `IsSuspended` — separate concern
- [x] [Review][Defer] RbacTestData ordering dependency with new FK — pre-existing test infra concern
- [x] [Review][Defer] Plaintext TOTP secret storage — deferred to Story 2.7 (encryption)
- [x] [Review][Defer] Missing index on `activation_tokens.expires_at_utc` — deferred to Story 3.3 (cleanup job)

### Change Log

- 2026-06-23: Story 1.10 created and validated — data model for organisations, activation_tokens, and User extensions.
- 2026-06-23: Implementation complete — entities, configurations, migration, seeders, and tests. Status → review.
