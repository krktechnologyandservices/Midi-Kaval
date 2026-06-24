using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class RegistrationControllerTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    public RegistrationControllerTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static TokenService CreateTokenService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACTIVATION_LINK_SIGNING_KEY"] = SigningKey,
            })
            .Build());

    private async Task<(string rawToken, string signature, string tokenHash, Guid orgId)> SeedActivationData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tokenService = CreateTokenService();
        var (rawToken, tokenHash, signature) = tokenService.GenerateActivationToken();

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Integration Test Org",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);

        var activationToken = new ActivationToken
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            TokenHash = tokenHash,
            TargetEmail = "director@example.org",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            DeliveryAttempts = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.ActivationTokens.Add(activationToken);
        await db.SaveChangesAsync();

        return (rawToken, signature, tokenHash, org.Id);
    }

    [Fact]
    public async Task GetValidateLink_WithValidParams_ReturnsEmail()
    {
        var (rawToken, signature, _, _) = await SeedActivationData();

        var response = await _client.GetAsync($"/api/v1/auth/activate?token={rawToken}&signature={signature}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ValidateActivationLinkResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal("director@example.org", envelope.Data.Email);
        Assert.Equal("Integration Test Org", envelope.Data.OrganisationName);
    }

    [Fact]
    public async Task GetValidateLink_WithInvalidSignature_Returns404()
    {
        var (rawToken, _, _, _) = await SeedActivationData();

        var response = await _client.GetAsync($"/api/v1/auth/activate?token={rawToken}&signature=tampered-sig");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetValidateLink_WithNonexistentHash_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/auth/activate?token=nonexistent-token&signature=nonexistent-sig");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostActivate_WithValidRequest_Returns200()
    {
        var (rawToken, signature, _, _) = await SeedActivationData();

        var request = new ActivateOrganisationRequest
        {
            Token = rawToken,
            Signature = signature,
            FullName = "Jane Smith",
            Password = "StrongPass1",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/activate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ActivateOrganisationResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.NotEqual(Guid.Empty, envelope.Data.UserId);
        Assert.NotEqual(Guid.Empty, envelope.Data.OrganisationId);
        Assert.Contains("Welcome to Kaval", envelope.Data.Message);
    }

    [Fact]
    public async Task PostActivate_WithInvalidSignature_Returns400()
    {
        var request = new ActivateOrganisationRequest
        {
            Token = "tampered-token",
            Signature = "tampered-sig",
            FullName = "Jane Smith",
            Password = "StrongPass1",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/activate", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostActivate_WithAlreadyConsumedToken_Returns409()
    {
        var (rawToken, signature, tokenHash, _) = await SeedActivationData();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.ActivationTokens.FirstAsync(t => t.TokenHash == tokenHash);
            token.ConsumedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var request = new ActivateOrganisationRequest
        {
            Token = rawToken,
            Signature = signature,
            FullName = "Jane Smith",
            Password = "StrongPass1",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/activate", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostActivate_AllowsUnauthenticatedAccess()
    {
        var (rawToken, signature, _, _) = await SeedActivationData();

        var request = new ActivateOrganisationRequest
        {
            Token = rawToken,
            Signature = signature,
            FullName = "Jane Smith",
            Password = "StrongPass1",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/activate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);

    private sealed record ApiMeta(string RequestId);
}
