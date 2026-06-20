using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Models.Sync;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class SyncPushTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public SyncPushTests(AuthWebApplicationFactory factory)
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
    public async Task VisitStart_AppliedOnce_ReplayReturnsDuplicate()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var visit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var mutationId = Guid.NewGuid();
        var request = BuildStartRequest(mutationId, visit.Id);

        var first = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Applied, first.Data.Results[0].Status);
        Assert.Equal("InProgress", first.Data.Results[0].Visit?.Status);

        var second = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Duplicate, second.Data.Results[0].Status);
        Assert.NotNull(second.Data.Results[0].Visit);
    }

    [Fact]
    public async Task VisitComplete_AppliedOnce_ReplayReturnsDuplicate()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var visit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.StartVisitAsync(_client, worker.AccessToken, visit.Id);

        var mutationId = Guid.NewGuid();
        var noteTime = DateTime.UtcNow;
        var request = BuildCompleteRequest(mutationId, visit.Id, "Offline completion note.", noteTime);

        var first = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Applied, first.Data.Results[0].Status);
        Assert.Equal("Completed", first.Data.Results[0].Visit?.Status);

        var second = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Duplicate, second.Data.Results[0].Status);
        Assert.NotNull(second.Data.Results[0].Visit);
    }

    [Fact]
    public async Task VisitStart_WhenAlreadyInProgress_ReturnsRejected()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var visit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.StartVisitAsync(_client, worker.AccessToken, visit.Id);

        var request = BuildStartRequest(Guid.NewGuid(), visit.Id);
        var result = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);

        Assert.Equal(SyncMutationStatuses.Rejected, result.Data.Results[0].Status);
        Assert.Contains("already in progress", result.Data.Results[0].ServerMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coordinator_PushSync_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = BuildStartRequest(Guid.NewGuid(), Guid.NewGuid());

        var response = await VisitTestData.SendPushSyncAsync(_client, coordinator.AccessToken, request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
    }

    private static SyncPushRequest BuildStartRequest(Guid mutationId, Guid visitId) =>
        new()
        {
            Mutations =
            [
                new SyncMutationDto
                {
                    ClientMutationId = mutationId,
                    Type = SyncMutationTypes.VisitStart,
                    ClientTimestampUtc = DateTime.UtcNow,
                    Payload = JsonSerializer.SerializeToElement(new { visitId }),
                },
            ],
        };

    private static SyncPushRequest BuildCompleteRequest(
        Guid mutationId,
        Guid visitId,
        string note,
        DateTime noteClientTimestampUtc) =>
        new()
        {
            Mutations =
            [
                new SyncMutationDto
                {
                    ClientMutationId = mutationId,
                    Type = SyncMutationTypes.VisitComplete,
                    ClientTimestampUtc = DateTime.UtcNow,
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        visitId,
                        note,
                        noteClientTimestampUtc,
                    }),
                },
            ],
        };
}
