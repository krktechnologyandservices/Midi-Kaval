using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Migration;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.IntegrationTests;

/// <summary>Factory with DualAuth enabled and a dedicated Vendor seed section for testing.</summary>
public sealed class DualAuthEnabledWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();

        Environment.SetEnvironmentVariable("DualAuth__Enabled", "true");

        // Set up Vendor seed section for dual auth auto-migration tests.
        // Note: the VendorUserSeeder runs during startup and creates this user.
        // Tests should delete the pre-seeded user before testing dual auth auto-migration.
        Environment.SetEnvironmentVariable("Seed__Vendor__Email", "dual-auth-vendor@pilot.example");
        Environment.SetEnvironmentVariable("Seed__Vendor__Password", "TestDualAuthVendorPass123!");
    }
}

[Collection("AuthIntegration")]
public class DualAuthLoginTests : IClassFixture<DualAuthEnabledWebApplicationFactory>, IAsyncLifetime
{
    private readonly DualAuthEnabledWebApplicationFactory _factory;
    private HttpClient _client = null!;

    private const string VendorEmail = "dual-auth-vendor@pilot.example";
    private const string VendorPassword = "TestDualAuthVendorPass123!";

    public DualAuthLoginTests(DualAuthEnabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();

        // Remove any pre-seeded vendor user so dual auth auto-migration path is exercised
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.Users.FirstOrDefaultAsync(
            u => u.OrganisationId == AccountMigrationService.VendorOrganisationId && u.Email == VendorEmail);
        if (existing is not null)
        {
            db.Users.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DualAuth_VendorAccount_LogsInAndCreatesUser()
    {
        // Act: login with vendor seed credentials (DualAuth:Enabled is true)
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = VendorEmail, Password = VendorPassword });

        // Assert: 200 OK with challenge (OTP flow started)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.NotEqual(Guid.Empty, envelope.Data.ChallengeId);

        // Assert: user exists in DB with correct role and blank name
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == VendorEmail);
        Assert.NotNull(user);
        Assert.Equal(UserRoles.Vendor, user.Role);
        Assert.Equal("", user.FirstName);
        Assert.Equal("", user.LastName);
        Assert.Equal(AccountMigrationService.VendorOrganisationId, user.OrganisationId);

        // Assert: password is properly hashed (not plaintext)
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, VendorPassword);
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public async Task DualAuth_WrongPassword_Returns401()
    {
        // Act: login with correct email but wrong password
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = VendorEmail, Password = "wrong-password" });

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DualAuth_NonExistentUser_StillFails()
    {
        // Act: login with completely unknown email (not in any seed section)
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "unknown@pilot.example", Password = "any-password" });

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

[Collection("AuthIntegration")]
public class DualAuthFlagDisabledTests : IClassFixture<DualAuthDisabledWebApplicationFactory>, IAsyncLifetime
{
    private readonly DualAuthDisabledWebApplicationFactory _factory;
    private HttpClient _client = null!;

    private const string VendorEmail = "dual-auth-vendor@pilot.example";
    private const string VendorPassword = "TestDualAuthVendorPass123!";

    public DualAuthFlagDisabledTests(DualAuthDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DualAuth_FlagDisabled_FallsThroughToNormalAuth()
    {
        // Act: DualAuth is disabled (DualAuth:Enabled defaults to false)
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = VendorEmail, Password = VendorPassword });

        // Assert: 401 Unauthorized — user not in DB, dual auth disabled, no fallback
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Factory with DualAuth disabled (default) but vendor seed config populated.
/// The vendor user is seeded by VendorUserSeeder during startup.
/// Dual auth fallback is NOT active, so login with seed credentials fails.
/// </summary>
public sealed class DualAuthDisabledWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();

        // Populate the Vendor seed section so config has credentials,
        // but DualAuth:Enabled is NOT set (defaults to false).
        Environment.SetEnvironmentVariable("Seed__Vendor__Email", "dual-auth-vendor@pilot.example");
        Environment.SetEnvironmentVariable("Seed__Vendor__Password", "TestDualAuthVendorPass123!");
    }
}

[Collection("AuthIntegration")]
public class DualAuthMigratedUserTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    private const string MigratedEmail = "migrated-user@pilot.example";
    private const string MigratedPassword = "PreviouslyMigratedPass1!";

    public DualAuthMigratedUserTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();

        // Create a user in DB to simulate a previously auto-migrated account
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var now = DateTime.UtcNow;

        // Ensure the org exists
        if (!await db.Organisations.AnyAsync(o => o.Id == AuthTestData.OrganisationId))
        {
            db.Organisations.Add(new Organisation
            {
                Id = AuthTestData.OrganisationId,
                Name = "Test Org",
                IsActive = true,
                CreatedAtUtc = now,
            });
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = AuthTestData.OrganisationId,
            Email = MigratedEmail,
            FirstName = "",
            LastName = "",
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, MigratedPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DualAuth_MigratedUser_LogsInNormallyAfterCutover()
    {
        // Act: login without DualAuth (base factory has no DualAuth config)
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = MigratedEmail, Password = MigratedPassword });

        // Assert: 200 OK — normal DB path works for previously migrated user
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.NotEqual(Guid.Empty, envelope.Data.ChallengeId);
    }
}

/// <summary>Envelope deserialization helper (mirrors existing pattern in AuthLoginTests).</summary>
internal sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);

/// <summary>
/// Factory with DualAuth enabled but Vendor password missing — tests AC4.
/// </summary>
public sealed class DualAuthMissingPasswordWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();

        Environment.SetEnvironmentVariable("DualAuth__Enabled", "true");
        Environment.SetEnvironmentVariable("Seed__Vendor__Email", "missing-pw-vendor@pilot.example");
        // Deliberately omit Seed__Vendor__Password — simulates empty/missing password in config
    }
}

[Collection("AuthIntegration")]
public class DualAuthMissingPasswordTests : IClassFixture<DualAuthMissingPasswordWebApplicationFactory>, IAsyncLifetime
{
    private readonly DualAuthMissingPasswordWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public DualAuthMissingPasswordTests(DualAuthMissingPasswordWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DualAuth_SeedAccountWithMissingPassword_Returns401()
    {
        // AC4: seed section has a valid email but password is missing
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "missing-pw-vendor@pilot.example", Password = "any-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Factory with DualAuth enabled but invalid Seed:OrganisationId — tests AC6.
/// </summary>
public sealed class DualAuthInvalidOrgIdWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();

        Environment.SetEnvironmentVariable("DualAuth__Enabled", "true");
        Environment.SetEnvironmentVariable("Seed__OrganisationId", "not-a-guid");
        Environment.SetEnvironmentVariable("Seed__Admin__Email", "admin-with-bad-org@pilot.example");
        Environment.SetEnvironmentVariable("Seed__Admin__Password", "AdminPassword1!");
    }
}

[Collection("AuthIntegration")]
public class DualAuthInvalidOrgIdTests : IClassFixture<DualAuthInvalidOrgIdWebApplicationFactory>, IAsyncLifetime
{
    private readonly DualAuthInvalidOrgIdWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public DualAuthInvalidOrgIdTests(DualAuthInvalidOrgIdWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DualAuth_InvalidOrgId_Returns401()
    {
        // AC6: Seed:OrganisationId is invalid, so Admin auto-migration fails
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "admin-with-bad-org@pilot.example", Password = "AdminPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
