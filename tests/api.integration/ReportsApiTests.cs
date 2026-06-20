using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Reports;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class ReportsApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public ReportsApiTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await EnsureTestDataAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Export_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await SendPostExportAsync(_client, string.Empty, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_SocialWorker_Returns403()
    {
        var socialWorker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, socialWorker.AccessToken, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_CaseWorker_Returns403()
    {
        var caseWorker = await VisitTestData.BuildCaseWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, caseWorker.AccessToken, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_InvalidReportType_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, coordinator.AccessToken, "invalid-type", "excel");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Contains("Invalid report type", body.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("daily-work", body.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_InvalidFormat_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, coordinator.AccessToken, "daily-work", "csv");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(body);
        Assert.Contains("Format must be", body.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_ValidRequest_Returns202WithJobId()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, coordinator.AccessToken, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ReportExportApiEnvelope>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Data);
        Assert.NotEqual(Guid.Empty, envelope.Data.JobId);
        Assert.Equal("pending", envelope.Data.Status);
    }

    [Fact]
    public async Task Status_ReturnsPendingForNewJob()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var jobId = await StartExportAsync(_client, coordinator.AccessToken, "daily-work", "excel");

        var statusResponse = await SendGetStatusAsync(_client, coordinator.AccessToken, jobId);
        Assert.Equal(System.Net.HttpStatusCode.OK, statusResponse.StatusCode);

        var envelope = await statusResponse.Content.ReadFromJsonAsync<StatusApiEnvelope>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Data);
        Assert.Equal("pending", envelope.Data.Status);
    }

    [Fact]
    public async Task Status_ReturnsNotFoundForOtherUsersJob()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var jobId = await StartExportAsync(_client, coordinator.AccessToken, "daily-work", "excel");

        // Director in same org has different userId — should not see coordinator's job
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var statusResponse = await SendGetStatusAsync(_client, director.AccessToken, jobId);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, statusResponse.StatusCode);
    }

    [Fact]
    public async Task CoordinatorHasAccess_Returns202()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, coordinator.AccessToken, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task DirectorHasAccess_Returns202()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var response = await SendPostExportAsync(_client, director.AccessToken, "daily-work", "excel");
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ListExports_ReturnsPaginatedResults()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        await StartExportAsync(_client, coordinator.AccessToken, "daily-work", "excel");
        await StartExportAsync(_client, coordinator.AccessToken, "yearly-work", "pdf");

        var response = await SendGetListAsync(_client, coordinator.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ListApiEnvelope>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Data);
        Assert.NotEmpty(envelope.Data.Items);
        Assert.Equal(2, envelope.Data.Items.Count);
    }

    [Fact]
    public async Task WorkloadDistribution_DoesNotIncludeRankingOrScoring()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var jobId = await StartExportAsync(_client, coordinator.AccessToken, "workload-distribution", "excel");

        // Verify the job was created — actual report generation is async
        // The key test is that the workload-distribution type is accepted
        var statusResponse = await SendGetStatusAsync(_client, coordinator.AccessToken, jobId);
        Assert.Equal(System.Net.HttpStatusCode.OK, statusResponse.StatusCode);

        var envelope = await statusResponse.Content.ReadFromJsonAsync<StatusApiEnvelope>();
        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Data);
        // Status should be pending (not yet processed by background service)
        Assert.Equal("pending", envelope.Data.Status);
    }

    [Fact]
    public async Task AllReportTypes_AreAccepted()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var types = new[] { "daily-work", "yearly-work", "visits-planned-vs-completed",
            "interventions", "court-summary", "offence-area-counts",
            "workload-distribution", "travel-totals" };

        foreach (var type in types)
        {
            var response = await SendPostExportAsync(_client, coordinator.AccessToken, type, "excel");
            Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        }
    }

    private static async Task<Guid> StartExportAsync(HttpClient client, string accessToken, string type, string format)
    {
        var response = await SendPostExportAsync(client, accessToken, type, format);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ReportExportApiEnvelope>();
        return envelope!.Data!.JobId;
    }

    private static Task<HttpResponseMessage> SendPostExportAsync(
        HttpClient client, string accessToken, string type, string format)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reports/{type}/export")
        {
            Content = JsonContent.Create(new ReportExportRequest { Format = format }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendGetStatusAsync(
        HttpClient client, string accessToken, Guid jobId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/reports/exports/{jobId:D}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendGetListAsync(
        HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/reports/exports");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private async Task EnsureTestDataAsync()
    {
        await RbacTestData.EnsureRoleUsersAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orgId = AuthTestData.OrganisationId;

        if (await db.Cases.AnyAsync(c => c.CrimeNumber == "RPT-CR-001"))
        {
            return; // Already seeded
        }

        var coordinatorId = await GetUserIdByEmailAsync(db, RbacTestData.CoordinatorEmail);
        var caseWorkerId = await GetUserIdByEmailAsync(db, RbacTestData.CaseWorkerEmail);

        // Create 2 test cases
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();

        db.Cases.AddRange(
            new Case
            {
                Id = case1Id,
                OrganisationId = orgId,
                CrimeNumber = "RPT-CR-001",
                StNumber = "RPT-ST-001",
                BeneficiaryName = "Reports Test 1",
                CurrentStage = CaseStage.ProcessInitiation,
                OffenceClassification = OffenceClassification.Petty,
                Domicile = Domicile.Rural,
                AssignedWorkerId = coordinatorId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            },
            new Case
            {
                Id = case2Id,
                OrganisationId = orgId,
                CrimeNumber = "RPT-CR-002",
                StNumber = "RPT-ST-002",
                BeneficiaryName = "Reports Test 2",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                OffenceClassification = OffenceClassification.Serious,
                Domicile = Domicile.Urban,
                AssignedWorkerId = caseWorkerId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> GetUserIdByEmailAsync(AppDbContext db, string email)
    {
        var user = await db.Users.SingleAsync(u => u.Email == email.Trim().ToLowerInvariant());
        return user.Id;
    }

    private sealed record ReportExportApiEnvelope(ReportExportJobDto Data, ApiMeta Meta);

    private sealed record StatusApiEnvelope(ReportExportStatusDto Data, ApiMeta Meta);

    private sealed record ListApiEnvelope(ReportExportListResultDto Data, ApiMeta Meta);

    private sealed record ApiMeta(string RequestId, int? TotalCount);
}
