using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Migration;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AccountMigrationServiceTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;

    private const string AdminEmail = "migrate-admin@test.com";
    private const string AdminPassword = "MigrateAdmin123!";
    private const string FwEmail = "migrate-fw@test.com";
    private const string FwPassword = "MigrateFw123!";
    private const string FwRole = "SocialWorker";
    private const string VendorEmail = "migrate-vendor@test.com";
    private const string VendorPassword = "MigrateVendor123!";

    public AccountMigrationServiceTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("Seed__Admin__Email", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__Admin__Password", AdminPassword);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Email", FwEmail);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Password", FwPassword);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", FwRole);
        Environment.SetEnvironmentVariable("Seed__Vendor__Email", VendorEmail);
        Environment.SetEnvironmentVariable("Seed__Vendor__Password", VendorPassword);
    }

    public Task DisposeAsync()
    {
        // Clear all Seed__ env vars to prevent leaking to other tests.
        // Tests in this class are serialized via [Collection("AuthIntegration")],
        // but clearing ensures no cross-collection contamination.
        Environment.SetEnvironmentVariable("Seed__OrganisationId", null);
        Environment.SetEnvironmentVariable("Seed__Admin__Email", null);
        Environment.SetEnvironmentVariable("Seed__Admin__Password", null);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Email", null);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Password", null);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", null);
        Environment.SetEnvironmentVariable("Seed__Vendor__Email", null);
        Environment.SetEnvironmentVariable("Seed__Vendor__Password", null);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Migrates_AdminAccount_WithDirectorRole()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.RunAsync();

        Assert.True(result.Created >= 1);
        var user = await FindUserByEmailAsync(db, AdminEmail,
            Guid.Parse("00000000-0000-4000-8000-000000000001"));
        Assert.NotNull(user);
        Assert.Equal(UserRoles.Director, user.Role);
        Assert.Equal("", user.FirstName);
        Assert.Equal("", user.LastName);
    }

    [Fact]
    public async Task Migrates_FieldWorker_WithConfiguredRole()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.RunAsync();

        Assert.True(result.Created >= 1);
        var user = await FindUserByEmailAsync(db, FwEmail,
            Guid.Parse("00000000-0000-4000-8000-000000000001"));
        Assert.NotNull(user);
        Assert.Equal(FwRole, user.Role);
        Assert.Equal("", user.FirstName);
        Assert.Equal("", user.LastName);
    }

    [Fact]
    public async Task Migrates_VendorAccount_UnderVendorOrg()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.RunAsync();

        Assert.True(result.Created >= 1);
        var vendorOrgId = AccountMigrationService.VendorOrganisationId;
        var user = await FindUserByEmailAsync(db, VendorEmail, vendorOrgId);
        Assert.NotNull(user);
        Assert.Equal(UserRoles.Vendor, user.Role);
    }

    [Fact]
    public async Task Idempotent_SecondRun_SkipsExistingAccounts()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();

        // First run — accounts created
        var firstResult = await service.RunAsync();

        // Second run — all accounts should be skipped (admin+vendor) or updated (field worker)
        var secondResult = await service.RunAsync();

        // The admin and vendor should be skipped; field worker is an update
        Assert.Equal(firstResult.Created, secondResult.Created + secondResult.Updated + secondResult.Skipped);
        // On second run, no new accounts should be created
        Assert.Equal(0, secondResult.Created);
        // Field worker upsert should count as an update
        Assert.Equal(1, secondResult.Updated);
    }

    [Fact]
    public async Task SelfHeals_MissingOrganisation()
    {
        var customOrgId = Guid.NewGuid().ToString();

        Environment.SetEnvironmentVariable("Seed__OrganisationId", customOrgId);
        Environment.SetEnvironmentVariable("Seed__Admin__Email", "self-heal-admin@test.com");
        Environment.SetEnvironmentVariable("Seed__Admin__Password", "SelfHeal123!");

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var result = await service.RunAsync();

            Assert.True(result.Created >= 1);

            // Verify org was created
            var org = await db.Organisations.FindAsync(Guid.Parse(customOrgId));
            Assert.NotNull(org);
            Assert.Equal("Primary Organisation", org.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__OrganisationId",
                "00000000-0000-4000-8000-000000000001");
            Environment.SetEnvironmentVariable("Seed__Admin__Email", AdminEmail);
            Environment.SetEnvironmentVariable("Seed__Admin__Password", AdminPassword);
        }
    }

    [Fact]
    public async Task MissingCredentials_SkipsThatAccount()
    {
        Environment.SetEnvironmentVariable("Seed__Admin__Email", "");
        Environment.SetEnvironmentVariable("Seed__Admin__Password", "");

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var result = await service.RunAsync();

            // Admin should skip, but field worker and vendor should process
            Assert.True(result.Skipped >= 1);

            // Admin user should NOT exist (credentials were empty, so it was skipped)
            var adminUser = await FindUserByEmailAsync(db, AdminEmail,
                Guid.Parse("00000000-0000-4000-8000-000000000001"));
            Assert.Null(adminUser);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__Admin__Email", AdminEmail);
            Environment.SetEnvironmentVariable("Seed__Admin__Password", AdminPassword);
        }
    }

    [Fact]
    public async Task InvalidFieldWorkerRole_SkipsThatAccount()
    {
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", "InvalidRole");

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();

            var result = await service.RunAsync();

            // Field worker should be skipped, admin and vendor should succeed
            Assert.True(result.Skipped >= 1);

            // Field worker should NOT exist
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fwUser = await FindUserByEmailAsync(db, FwEmail,
                Guid.Parse("00000000-0000-4000-8000-000000000001"));
            Assert.Null(fwUser);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", FwRole);
        }
    }

    [Fact]
    public async Task FieldWorkerUpsert_UpdatesExistingUser()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // First run — creates the field worker
        await service.RunAsync();

        // Change the password and role
        const string newPassword = "NewFwPass456!";
        const string newRole = "CaseWorker";
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Password", newPassword);
        Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", newRole);

        try
        {
            // Second run — should update the existing field worker
            var result = await service.RunAsync();

            var user = await FindUserByEmailAsync(db, FwEmail,
                Guid.Parse("00000000-0000-4000-8000-000000000001"));
            Assert.NotNull(user);
            Assert.Equal(newRole, user.Role);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__FieldWorker__Password", FwPassword);
            Environment.SetEnvironmentVariable("Seed__FieldWorker__Role", FwRole);
        }
    }

    [Fact]
    public async Task EmailNormalization_LowercasesEmail()
    {
        Environment.SetEnvironmentVariable("Seed__Admin__Email", "UPPERCASE-ADMIN@TEST.COM");

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var result = await service.RunAsync();

            Assert.True(result.Created >= 1);

            // Should be stored as lowercase
            var normalizedEmail = "uppercase-admin@test.com";
            var user = await FindUserByEmailAsync(db, normalizedEmail,
                Guid.Parse("00000000-0000-4000-8000-000000000001"));
            Assert.NotNull(user);
            Assert.Equal(normalizedEmail, user.Email);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__Admin__Email", AdminEmail);
        }
    }

    [Fact]
    public async Task MigratedUsers_HaveEmptyFirstAndLastName()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountMigrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.RunAsync();

        Assert.True(result.Created >= 1);
        var user = await FindUserByEmailAsync(db, AdminEmail,
            Guid.Parse("00000000-0000-4000-8000-000000000001"));
        Assert.NotNull(user);
        Assert.Equal("", user.FirstName);
        Assert.Equal("", user.LastName);
    }

    private static async Task<User?> FindUserByEmailAsync(AppDbContext db, string email, Guid organisationId)
    {
        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OrganisationId == organisationId && u.Email == email);
    }
}
