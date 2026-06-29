using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class OrganisationsControllerTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    // Vendor test user credentials
    private const string VendorEmail = "vendor@orglist.test";
    private const string VendorPassword = "VendorPass123!";

    public OrganisationsControllerTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await EnsureVendorUserAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task EnsureVendorUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        var normalizedEmail = VendorEmail.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(
            u => u.OrganisationId == AuthTestData.OrganisationId && u.Email == normalizedEmail);

        if (existing is not null)
        {
            existing.Role = UserRoles.Vendor;
            existing.IsActive = true;
            existing.TokenVersion = 0;
            existing.PasswordHash = passwordHasher.HashPassword(existing, VendorPassword);
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.TotpEnrolledAt = DateTime.UtcNow; // Enrolled in 2FA
            await db.SaveChangesAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = AuthTestData.OrganisationId,
            Email = normalizedEmail,
            FirstName = "Vendor",
            LastName = "User",
            Role = UserRoles.Vendor,
            TokenVersion = 0,
            IsActive = true,
            IsSuspended = false,
            PasswordHash = passwordHasher.HashPassword(new User(), VendorPassword),
            TotpEnrolledAt = DateTime.UtcNow, // Enrolled in 2FA
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<string> GetVendorAccessTokenAsync()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender, VendorEmail, VendorPassword);
        return session.AccessToken;
    }

    private static TokenService CreateTokenService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACTIVATION_LINK_SIGNING_KEY"] = SigningKey,
            })
            .Build());

    private async Task<Guid> SeedZeroDirectorOrganisationAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Zero-Director Org",
            IsActive = true,
            HasPendingRecovery = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync();

        // No Director users seeded — this org is in zero-Director state
        return org.Id;
    }

    private async Task<Guid> SeedOrganisationWithDirectorAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Active Org",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync();

        // Seed a Director user
        var director = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            Email = "director@activeorg.test",
            FirstName = "Active",
            LastName = "Director",
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            IsSuspended = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(director);
        await db.SaveChangesAsync();

        return org.Id;
    }

    [Fact]
    public async Task GetOrganisations_ReturnsResults()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/vendor/organisations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganisations_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/vendor/organisations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganisationDetail_ReturnsCorrectFields()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orgId = await SeedZeroDirectorOrganisationAsync();

        var response = await _client.GetAsync($"/api/v1/vendor/organisations/{orgId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganisationDetail_Returns404_ForNonExistentOrg()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/v1/vendor/organisations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReissueActivation_Succeeds_ForZeroDirectorOrg()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orgId = await SeedZeroDirectorOrganisationAsync();

        var request = new { targetDirectorEmail = "newdirector@example.org" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/vendor/organisations/{orgId}/reissue-activation", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReissueActivation_Returns409_WhenOrgHasDirectors()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var orgId = await SeedOrganisationWithDirectorAsync();

        var request = new { targetDirectorEmail = "newdirector@example.org" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/vendor/organisations/{orgId}/reissue-activation", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReissueActivation_Returns404_ForNonExistentOrg()
    {
        var token = await GetVendorAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { targetDirectorEmail = "newdirector@example.org" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/vendor/organisations/{Guid.NewGuid()}/reissue-activation", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReissueActivation_Unauthenticated_Returns401()
    {
        var orgId = await SeedZeroDirectorOrganisationAsync();

        var request = new { targetDirectorEmail = "newdirector@example.org" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/vendor/organisations/{orgId}/reissue-activation", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReissueActivation_WithoutVendorRole_Returns403()
    {
        // Login as Director (not Vendor)
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client, _factory.EmailSender);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var orgId = await SeedZeroDirectorOrganisationAsync();

        var request = new { targetDirectorEmail = "newdirector@example.org" };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/vendor/organisations/{orgId}/reissue-activation", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
