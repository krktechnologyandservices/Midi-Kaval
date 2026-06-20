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
            "TRUNCATE TABLE attachments, travel_claim_cases, travel_claims, visit_notes, visits, in_app_notifications, user_devices, court_sittings, interventions, case_notes, case_assignments, case_search_presets, case_stages, sync_mutations, audit_events, cases, users RESTART IDENTITY CASCADE");
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
