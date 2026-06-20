using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
public class CaseGpsTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseGpsTests(AuthWebApplicationFactory factory)
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
    public async Task VerifyGps_SetsFields_Audits_AndReturnsDto()
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

        var gps = await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "Near community hall");

        Assert.True(gps.GpsVerified);
        Assert.Equal(caseId, gps.CaseId);
        Assert.Equal(12.9716m, gps.Latitude);
        Assert.Equal(77.5946m, gps.Longitude);
        Assert.Equal("Near community hall", gps.Landmark);
        Assert.NotNull(gps.GpsVerifiedAtUtc);
        Assert.Equal(worker.UserId, gps.GpsVerifiedByUserId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.True(row.GpsVerified);

        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseGpsVerified && e.ActorUserId == worker.UserId)
            .ToListAsync();
        Assert.Contains(audits, e => e.MetadataJson.Contains(caseId.ToString("D"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyGps_WithoutLandmark_Returns400()
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

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGps_InvalidLatitude_Returns400()
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

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            91m,
            77.5946m,
            "Invalid lat");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGps_InvalidLongitude_Returns400()
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

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            12.9716m,
            181m,
            "Invalid lng");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGps_UnknownCase_Returns404()
    {
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            Guid.NewGuid(),
            12.9716m,
            77.5946m,
            "Near community hall");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGps_WrongAssignee_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var caseWorker = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            caseWorker.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "Near community hall");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Director_VerifyGps_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var response = await CaseTestData.SendVerifyCaseGpsAsync(
            _client,
            director.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "Near community hall");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListToday_IncludesGpsVerifiedFlag_BeforeAndAfterVerify()
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

        var before = await VisitTestData.ListVisitsTodayAsync(_client, worker.AccessToken);
        var beforeItem = Assert.Single(before.Data.Items, i => i.Case.Id == caseId);
        Assert.False(beforeItem.Case.GpsVerified);

        await CaseTestData.VerifyCaseGpsAsync(
            _client,
            worker.AccessToken,
            caseId,
            12.9716m,
            77.5946m,
            "Near community hall");

        var after = await VisitTestData.ListVisitsTodayAsync(_client, worker.AccessToken);
        var afterItem = Assert.Single(after.Data.Items, i => i.Case.Id == caseId);
        Assert.True(afterItem.Case.GpsVerified);
        Assert.Equal(12.9716m, afterItem.Case.Latitude);
    }
}
