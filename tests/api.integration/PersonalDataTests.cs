using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class PersonalDataTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public PersonalDataTests(AuthWebApplicationFactory factory)
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
    public async Task Erase_Director_NullifiesPiiAndReturnsFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await SendEraseRequestAsync(director.AccessToken, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EraseResponse>();
        Assert.NotNull(body);

        var expectedFields = new[] { "beneficiaryName", "beneficiaryContact", "beneficiaryAge", "latitude", "longitude", "landmark" };
        Assert.Equal(expectedFields, body.NullifiedFields);

        // Verify DB values are null
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Null(row.BeneficiaryName);
        Assert.Null(row.BeneficiaryContact);
        Assert.Null(row.BeneficiaryAge);
        Assert.Null(row.Latitude);
        Assert.Null(row.Longitude);
        Assert.Null(row.Landmark);
    }

    [Fact]
    public async Task Erase_NonDirector_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await SendEraseRequestAsync(coordinator.AccessToken, caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Erase_Idempotent_SecondCallReturnsSameResult()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var first = await SendEraseRequestAsync(director.AccessToken, caseId);
        var firstBody = await first.Content.ReadFromJsonAsync<EraseResponse>();

        var second = await SendEraseRequestAsync(director.AccessToken, caseId);
        var secondBody = await second.Content.ReadFromJsonAsync<EraseResponse>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody.NullifiedFields, secondBody.NullifiedFields);
    }

    [Fact]
    public async Task Erase_NonExistentCase_Returns404()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var nonExistentId = Guid.NewGuid();

        var response = await SendEraseRequestAsync(director.AccessToken, nonExistentId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Export_Director_ReturnsPiiAsJson()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await SendExportRequestAsync(director.AccessToken, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Disposition"));
        var cd = response.Headers.GetValues("Content-Disposition").First();
        Assert.StartsWith("attachment", cd);
        Assert.Contains($"case-{caseId:D}-personal-data.json", cd);

        var body = await response.Content.ReadFromJsonAsync<ExportResponse>();
        Assert.NotNull(body);
        Assert.Equal("Ravi Kumar", body.BeneficiaryName);
        Assert.Equal("+919876543210", body.BeneficiaryContact);
        Assert.Equal(17, body.BeneficiaryAge);
        Assert.NotNull(body.Landmark);
    }

    [Fact]
    public async Task Export_NonDirector_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await SendExportRequestAsync(coordinator.AccessToken, caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_NonExistentCase_Returns404()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var nonExistentId = Guid.NewGuid();

        var response = await SendExportRequestAsync(director.AccessToken, nonExistentId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Export_AfterErasure_ReturnsNullFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        // Erase first
        await SendEraseRequestAsync(director.AccessToken, caseId);

        // Then export
        var response = await SendExportRequestAsync(director.AccessToken, caseId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ExportResponse>();
        Assert.NotNull(body);
        Assert.Null(body.BeneficiaryName);
        Assert.Null(body.BeneficiaryContact);
        Assert.Null(body.BeneficiaryAge);
        Assert.Null(body.Latitude);
        Assert.Null(body.Longitude);
        Assert.Null(body.Landmark);
    }

    [Fact]
    public async Task Erase_WritesAuditEvent()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await SendEraseRequestAsync(director.AccessToken, caseId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditEvents.SingleOrDefaultAsync(e =>
            e.EventType == AuditEventTypes.CasePersonalDataErased
            && e.ActorUserId == director.UserId);

        Assert.NotNull(audit);
        Assert.Null(audit.SubjectUserId);
        Assert.NotNull(audit.MetadataJson);
        Assert.Contains("beneficiaryName", audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("nullifiedFields", audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erase_PreservesOperationalFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await SendEraseRequestAsync(director.AccessToken, caseId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);

        Assert.NotNull(row.CrimeNumber);
        Assert.NotNull(row.StNumber);
        Assert.NotNull(row.TypeOfOffence);
        Assert.NotEqual(default, row.VisitCount);
        Assert.NotEqual(default, row.CurrentStage);
    }

    private async Task<HttpResponseMessage> SendEraseRequestAsync(string accessToken, Guid caseId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/cases/{caseId:D}/personal-data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendExportRequestAsync(string accessToken, Guid caseId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}/personal-data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private sealed record EraseResponse(string[] NullifiedFields);
    private sealed record ExportResponse(
        string? BeneficiaryName,
        string? BeneficiaryContact,
        int? BeneficiaryAge,
        decimal? Latitude,
        decimal? Longitude,
        string? Landmark);
}
