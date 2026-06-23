using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class PiiEncryptionTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public PiiEncryptionTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await RbacTestData.EnsureRoleUsersAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BeneficiaryName_IsEncryptedAtRest()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var raw = await db.Database
            .SqlQueryRaw<byte[]>("SELECT beneficiary_name FROM cases WHERE id = {0}", caseId)
            .FirstAsync();

        var rawString = Encoding.UTF8.GetString(raw);
        Assert.DoesNotContain("Ravi", rawString);
    }

    [Fact]
    public async Task BeneficiaryContact_IsEncryptedAtRest()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var raw = await db.Database
            .SqlQueryRaw<byte[]>("SELECT beneficiary_contact FROM cases WHERE id = {0}", caseId)
            .FirstAsync();

        var rawString = Encoding.UTF8.GetString(raw);
        Assert.DoesNotContain("9876543210", rawString);
    }

    [Fact]
    public async Task Landmark_IsEncryptedAtRest()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        // Set landmark via EF Core (value converter transparently encrypts it)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseEntity = await db.Cases.FindAsync(caseId);
            Assert.NotNull(caseEntity);
            caseEntity.Landmark = "Near City Hospital, MG Road";
            await db.SaveChangesAsync();
        }

        // Verify raw SQL shows encrypted binary, not plaintext
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var raw = await db.Database
                .SqlQueryRaw<byte[]>("SELECT landmark FROM cases WHERE id = {0}", caseId)
                .FirstAsync();

            var rawString = Encoding.UTF8.GetString(raw);
            Assert.DoesNotContain("Hospital", rawString);
        }
    }

    [Fact]
    public async Task Latitude_IsEncryptedAtRest()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        // Set latitude via EF Core (value converter transparently encrypts it)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseEntity = await db.Cases.FindAsync(caseId);
            Assert.NotNull(caseEntity);
            caseEntity.Latitude = 12.9716m;
            await db.SaveChangesAsync();
        }

        // Verify raw SQL shows encrypted binary, not plaintext
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var raw = await db.Database
                .SqlQueryRaw<byte[]>("SELECT latitude FROM cases WHERE id = {0}", caseId)
                .FirstAsync();

            var rawString = Encoding.UTF8.GetString(raw);
            Assert.DoesNotContain("12.9716", rawString);
        }
    }

    [Fact]
    public async Task Longitude_IsEncryptedAtRest()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        // Set longitude via EF Core (value converter transparently encrypts it)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseEntity = await db.Cases.FindAsync(caseId);
            Assert.NotNull(caseEntity);
            caseEntity.Longitude = 77.5937m;
            await db.SaveChangesAsync();
        }

        // Verify raw SQL shows encrypted binary, not plaintext
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var raw = await db.Database
                .SqlQueryRaw<byte[]>("SELECT longitude FROM cases WHERE id = {0}", caseId)
                .FirstAsync();

            var rawString = Encoding.UTF8.GetString(raw);
            Assert.DoesNotContain("77.5937", rawString);
        }
    }

    [Fact]
    public async Task CaseRead_ReturnsDecryptedPlaintext()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<CaseDetailEnvelope>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal("Ravi Kumar", envelope.Data.BeneficiaryName);
    }

    [Fact]
    public async Task ExactNameSearch_ReturnsCorrectCase()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.BeneficiaryName = "SearchablePerson_" + Guid.NewGuid().ToString("N")[..8];
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var searchResult = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["q"] = request.BeneficiaryName });

        Assert.Contains(searchResult.Data.Items, c => c.Id == caseId);
    }

    [Fact]
    public async Task ExistingTests_PassWithEncryption()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var envelope = await getResponse.Content.ReadFromJsonAsync<CaseDetailEnvelope>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal(request.BeneficiaryName, envelope.Data.BeneficiaryName);
    }
}

internal sealed record CaseDetailEnvelope(CaseDetailDto Data, ApiMeta? Meta);

internal sealed record ApiMeta(string RequestId);

internal static class EncryptionTestData
{
    public static async Task<Guid> CreateCaseWithCustomNameAsync(
        HttpClient client,
        string accessToken,
        string name)
    {
        var request = CaseTestData.BuildValidRequest();
        request.BeneficiaryName = name;
        return await CaseTestData.CreateCaseAsync(client, accessToken, request);
    }
}
