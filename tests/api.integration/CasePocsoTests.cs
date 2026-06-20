using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Sync;
using MidiKaval.Api.Models.Visits;
using StackExchange.Redis;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CasePocsoTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CasePocsoTests(AuthWebApplicationFactory factory)
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
    public async Task PocsoCase_FieldWorkerListEndpoints_RedactBeneficiaryName()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        request.BeneficiaryName = "Ravi Kumar";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(2);
        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            scheduledAt,
            workerId);

        var today = await VisitTestData.ListVisitsTodayAsync(_client, worker.AccessToken);
        var visit = Assert.Single(today.Data.Items);
        Assert.Equal("POCSO", visit.Case!.SensitivityLevel);
        Assert.Equal("R. K.", visit.Case.BeneficiaryName);

        var detail = await CaseTestData.GetCaseDetailAsync(_client, worker.AccessToken, caseId);
        Assert.Equal("POCSO", detail.SensitivityLevel);
        Assert.Equal("R. K.", detail.BeneficiaryName);

        var assigned = await CaseTestData.ListAssignedCasesAsync(_client, worker.AccessToken);
        var assignedCase = Assert.Single(assigned.Data.Items, c => c.Id == caseId);
        Assert.Equal("R. K.", assignedCase.BeneficiaryName);
        Assert.Equal("POCSO", assignedCase.SensitivityLevel);
    }

    [Fact]
    public async Task PocsoCase_CoordinatorSearch_ReturnsFullBeneficiaryName()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        request.BeneficiaryName = "Ravi Kumar";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var search = await CaseTestData.SearchCasesAsync(
            _client,
            coordinator.AccessToken,
            new Dictionary<string, string?> { ["q"] = request.CrimeNumber });
        var item = Assert.Single(search.Data!.Items);
        Assert.Equal("Ravi Kumar", item.BeneficiaryName);
        Assert.Equal("POCSO", item.SensitivityLevel);
    }

    [Fact]
    public async Task RevealPii_AfterLoginOtp_WritesAuditAndReturnsFullName()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        request.BeneficiaryName = "Ravi Kumar";
        request.BeneficiaryAge = 14;
        request.BeneficiaryContact = "9876543210";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var reveal = await SendRevealPiiAsync(_client, worker.AccessToken, caseId);
        reveal.EnsureSuccessStatusCode();
        var envelope = await reveal.Content.ReadFromJsonAsync<ApiResponse<RevealCasePiiResponse>>();
        Assert.Equal("Ravi Kumar", envelope!.Data!.BeneficiaryName);
        Assert.Equal(14, envelope.Data.BeneficiaryAge);
        Assert.Equal("9876543210", envelope.Data.BeneficiaryContact);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CasePiiRevealed && e.ActorUserId == worker.UserId)
            .ToListAsync();
        Assert.Contains(audits, e => e.MetadataJson.Contains(caseId.ToString("D"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevealPii_WithoutRecentOtp_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync($"auth:otp-verified:{worker.UserId:D}");
            await db.KeyDeleteAsync($"auth:step-up-verified:{worker.UserId:D}");
        }

        var response = await SendRevealPiiAsync(_client, worker.AccessToken, caseId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncPush_PocsoCase_RedactsBeneficiaryNameInVisitPayload()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        request.BeneficiaryName = "Ravi Kumar";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var visit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2),
            workerId);

        var mutationId = Guid.NewGuid();
        var pushRequest = new SyncPushRequest
        {
            Mutations =
            [
                new SyncMutationDto
                {
                    ClientMutationId = mutationId,
                    Type = SyncMutationTypes.VisitStart,
                    ClientTimestampUtc = DateTime.UtcNow,
                    Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { visitId = visit.Id }),
                },
            ],
        };

        var push = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, pushRequest);
        Assert.Equal(SyncMutationStatuses.Applied, push.Data.Results[0].Status);
        Assert.Equal("POCSO", push.Data.Results[0].Visit!.Case!.SensitivityLevel);
        Assert.Equal("R. K.", push.Data.Results[0].Visit.Case.BeneficiaryName);
    }

    private static Task<HttpResponseMessage> SendRevealPiiAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/reveal-pii");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
