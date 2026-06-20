using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class VisitGroupingTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public VisitGroupingTests(AuthWebApplicationFactory factory)
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
    public async Task TwoNearbyVerifiedVisits_ReturnSuggestedOrder()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var caseA = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var caseB = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseA,
            CaseTestData.BuildTransferRequest(workerId));
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseB,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(2);
        await VisitTestData.ScheduleVisitAsync(_client, coordinator.AccessToken, caseA, scheduledAt);
        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseB,
            scheduledAt.AddHours(1));

        await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseA,
            12.9716m,
            77.5946m,
            "Hall A");
        await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseB,
            12.9800m,
            77.5946m,
            "Hall B");

        var envelope = await VisitTestData.GetTodayGroupingSuggestionAsync(_client, worker.AccessToken);

        Assert.NotEmpty(envelope.Data.Clusters);
        Assert.Equal(2, envelope.Data.SuggestedVisitOrder.Count);
        Assert.Equal(2, envelope.Data.EligibleCount);
    }

    [Fact]
    public async Task UnverifiedVisit_IsExcludedFromGrouping()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var verifiedCase = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var unverifiedCase = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            verifiedCase,
            CaseTestData.BuildTransferRequest(workerId));
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            unverifiedCase,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(2);
        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            verifiedCase,
            scheduledAt);
        var unverifiedVisit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            unverifiedCase,
            scheduledAt.AddHours(1));

        await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            verifiedCase,
            12.9716m,
            77.5946m,
            "Hall A");

        var envelope = await VisitTestData.GetTodayGroupingSuggestionAsync(_client, worker.AccessToken);

        Assert.Contains(
            envelope.Data.Excluded,
            item => item.VisitId == unverifiedVisit.Id && item.Reason == "gps_unverified");
        Assert.Equal(1, envelope.Data.EligibleCount);
        Assert.NotNull(envelope.Data.Message);
    }

    [Fact]
    public async Task SingleEligibleVisit_ReturnsEmptyClustersAndMessage()
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

        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "Hall A");

        var envelope = await VisitTestData.GetTodayGroupingSuggestionAsync(_client, worker.AccessToken);

        Assert.Empty(envelope.Data.Clusters);
        Assert.Empty(envelope.Data.SuggestedVisitOrder);
        Assert.Equal(
            "At least two visits with verified GPS are required for route grouping",
            envelope.Data.Message);
    }

    [Fact]
    public async Task Coordinator_GetGroupingSuggestion_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var response = await VisitTestData.SendTodayGroupingSuggestionAsync(
            _client,
            coordinator.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
    }
}
