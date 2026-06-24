using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Seed;
using Testcontainers.PostgreSql;

namespace MidiKaval.Api.IntegrationTests;

public class UsersSchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private static readonly Guid PilotOrganisationId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private const string SeedEmail = "director@pilot.example";
    private const string SeedPassword = "TestDirectorPassword123!";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    private async Task<AppDbContext> CreateMigratedDbContextAsync()
    {
        var db = CreateDbContext();
        await db.Database.MigrateAsync();
        return db;
    }

    private static async Task ClearUsersAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE activation_tokens, organisations, attachments, travel_claim_cases, travel_claims, visit_notes, visits, in_app_notifications, user_devices, court_sittings, interventions, case_notes, case_assignments, case_search_presets, case_stages, sync_mutations, audit_events, cases, users RESTART IDENTITY CASCADE");
    }

    private static IConfiguration CreateSeedConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Admin:Email"] = SeedEmail,
                ["Seed:Admin:Password"] = SeedPassword,
                ["Seed:OrganisationId"] = PilotOrganisationId.ToString(),
            })
            .Build();
    }

    [Fact]
    public async Task Migration_CreatesUsersTable_WithoutUnrelatedTables()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var tables = await db.Database
            .SqlQueryRaw<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name")
            .ToListAsync();

        var applicationTables = tables
            .Where(t => t != "__EFMigrationsHistory")
            .ToList();

        Assert.Equal(
            [
                "activation_tokens",
                "attachments",
                "audit_events",
                "case_assignments",
                "case_notes",
                "case_search_presets",
                "case_stages",
                "cases",
                "court_sittings",
                "in_app_notifications",
                "interventions",
                "organisations",
                "sync_mutations",
                "travel_claim_cases",
                "travel_claims",
                "user_devices",
                "users",
                "visit_notes",
                "visits",
            ],
            applicationTables);
    }

    [Fact]
    public async Task Migration_AddsNewColumns_ToUsersTable()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var columns = await db.Database
            .SqlQueryRaw<string>(
                "SELECT column_name FROM information_schema.columns WHERE table_name = 'users' ORDER BY column_name")
            .ToListAsync();

        Assert.Contains("is_suspended", columns);
        Assert.Contains("totp_secret", columns);
        Assert.Contains("totp_enrolled_at", columns);
    }

    [Fact]
    public async Task Migration_NewColumns_HaveExpectedDefaults()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var defaultOrgId = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = defaultOrgId,
            Email = "test@example.com",
            Role = UserRoles.Director,
            PasswordHash = "placeholder",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var saved = await db.Users.SingleAsync(u => u.Email == "test@example.com");
        Assert.False(saved.IsSuspended);
        Assert.Null(saved.TotpSecret);
        Assert.Null(saved.TotpEnrolledAt);
    }

    [Fact]
    public async Task Migration_SeedsDefaultOrganisation()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var defaultOrg = await db.Organisations.SingleOrDefaultAsync(o => o.Name == "Pilot Organisation");
        Assert.NotNull(defaultOrg);
        Assert.True(defaultOrg.IsActive);
    }

    [Fact]
    public async Task Migration_EnforcesOrganisationForeignKey_OnActivationTokens()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync();

        var token = new ActivationToken
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            TokenHash = "abc123",
            TargetEmail = "user@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.ActivationTokens.Add(token);
        await db.SaveChangesAsync();

        var saved = await db.ActivationTokens.SingleAsync(t => t.Id == token.Id);
        Assert.Equal(org.Id, saved.OrganisationId);
    }

    [Fact]
    public async Task Migration_PreventsOrgDeletion_WhenUsersExist()
    {
        await using var db = await CreateMigratedDbContextAsync();

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Org With Users",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            Email = "user@org.example",
            Role = UserRoles.SocialWorker,
            PasswordHash = "placeholder",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Organisations.Remove(org);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Seed_CreatesDirectorUser_WithExpectedFields()
    {
        await using var db = await CreateMigratedDbContextAsync();
        await ClearUsersAsync(db);

        var hasher = new PasswordHasher<User>();
        var seeder = new AdminUserSeeder(
            db,
            CreateSeedConfiguration(),
            hasher,
            NullLogger<AdminUserSeeder>.Instance,
            new TestHostEnvironment());

        await seeder.SeedAsync();

        var user = await db.Users.SingleAsync();

        Assert.Equal(SeedEmail, user.Email);
        Assert.Equal(UserRoles.Director, user.Role);
        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(PilotOrganisationId, user.OrganisationId);
        Assert.True(user.IsActive);
        Assert.NotEmpty(user.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.PasswordHash, SeedPassword));
    }

    [Fact]
    public async Task Seed_IsIdempotent_AcrossSecondRun()
    {
        await using var db = await CreateMigratedDbContextAsync();
        await ClearUsersAsync(db);

        var seeder = new AdminUserSeeder(
            db,
            CreateSeedConfiguration(),
            new PasswordHasher<User>(),
            NullLogger<AdminUserSeeder>.Instance,
            new TestHostEnvironment());

        await seeder.SeedAsync();

        var userAfterFirstSeed = await db.Users.SingleAsync();
        var passwordHash = userAfterFirstSeed.PasswordHash;
        var tokenVersion = userAfterFirstSeed.TokenVersion;

        await seeder.SeedAsync();

        Assert.Equal(1, await db.Users.CountAsync());

        var userAfterSecondSeed = await db.Users.SingleAsync();
        Assert.Equal(passwordHash, userAfterSecondSeed.PasswordHash);
        Assert.Equal(tokenVersion, userAfterSecondSeed.TokenVersion);
    }

    [Fact]
    public async Task Seed_NormalizesMixedCaseEmail_ToLowercase()
    {
        await using var db = await CreateMigratedDbContextAsync();
        await ClearUsersAsync(db);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Admin:Email"] = "Director@PILOT.Example",
                ["Seed:Admin:Password"] = SeedPassword,
                ["Seed:OrganisationId"] = PilotOrganisationId.ToString(),
            })
            .Build();

        var seeder = new AdminUserSeeder(
            db,
            configuration,
            new PasswordHasher<User>(),
            NullLogger<AdminUserSeeder>.Instance,
            new TestHostEnvironment());

        await seeder.SeedAsync();

        var user = await db.Users.SingleAsync();
        Assert.Equal("director@pilot.example", user.Email);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "MidiKaval.Api.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
