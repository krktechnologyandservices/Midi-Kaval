using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MidiKaval.Api.Models.Sync;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class SyncPushTravelClaimTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public SyncPushTravelClaimTests(AuthWebApplicationFactory factory)
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
    public async Task TravelClaimCreate_AppliedOnce_ReplayReturnsDuplicateWithTravelClaim()
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

        var mutationId = Guid.NewGuid();
        var request = BuildTravelClaimCreateRequest(mutationId, caseId);

        var first = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Applied, first.Data.Results[0].Status);
        Assert.NotNull(first.Data.Results[0].TravelClaim);
        Assert.Equal("Draft", first.Data.Results[0].TravelClaim?.Status);

        var second = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Duplicate, second.Data.Results[0].Status);
        Assert.NotNull(second.Data.Results[0].TravelClaim);
        Assert.Equal(first.Data.Results[0].TravelClaim?.Id, second.Data.Results[0].TravelClaim?.Id);
    }

    [Fact]
    public async Task TravelClaimCreate_InvalidPayload_ReturnsRejected()
    {
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var mutationId = Guid.NewGuid();

        var request = new SyncPushRequest
        {
            Mutations =
            [
                new SyncMutationDto
                {
                    ClientMutationId = mutationId,
                    Type = SyncMutationTypes.TravelClaimCreate,
                    ClientTimestampUtc = DateTime.UtcNow,
                    Payload = JsonDocument.Parse("{}").RootElement,
                },
            ],
        };

        var result = await VisitTestData.PushSyncAsync(_client, worker.AccessToken, request);
        Assert.Equal(SyncMutationStatuses.Rejected, result.Data.Results[0].Status);
    }

    private static SyncPushRequest BuildTravelClaimCreateRequest(Guid mutationId, Guid caseId) =>
        new()
        {
            Mutations =
            [
                new SyncMutationDto
                {
                    ClientMutationId = mutationId,
                    Type = SyncMutationTypes.TravelClaimCreate,
                    ClientTimestampUtc = DateTime.UtcNow,
                    Payload = JsonDocument.Parse(
                        $$"""
                        {
                          "claimDate": "2026-06-15T00:00:00Z",
                          "startLocation": "Office",
                          "destination": "District Court",
                          "transportMode": "Bus",
                          "amount": 45.5,
                          "notes": "Offline draft",
                          "caseIds": ["{{caseId}}"]
                        }
                        """).RootElement,
                },
            ],
        };
}
