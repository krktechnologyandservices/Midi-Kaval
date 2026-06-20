using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class TravelClaimDirectorApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public TravelClaimDirectorApiTests(AuthWebApplicationFactory factory)
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
    public async Task Approve_SubmittedClaim_Returns200_AndNotifiesClaimant()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var approved = await CaseTestData.ApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ApproveTravelClaimRequest { Comment = "Approved for June visit" });

        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Approved for June visit", approved.DecisionComment);
        Assert.NotNull(approved.DecidedAtUtc);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.InAppNotifications.SingleAsync(
            n => n.UserId == submitted.ClaimantUserId
                && n.EventType == NotificationEventTypes.TravelClaimApproved
                && n.ResourceId == submitted.Id);
        Assert.Equal("TravelClaim", notification.ResourceType);
        Assert.NotEqual(Guid.Empty, notification.CaseId);

        var audits = await db.AuditEvents
            .Where(e => e.OrganisationId == AuthTestData.OrganisationId)
            .ToListAsync();
        Assert.Contains(audits, e => e.EventType == AuditEventTypes.TravelClaimApproved);
    }

    [Fact]
    public async Task Return_SubmittedClaim_RequiresComment()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendReturnTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ReturnTravelClaimRequest { Comment = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Return_SubmittedClaim_Returns200_AndNotifiesClaimant()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var returned = await CaseTestData.ReturnTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ReturnTravelClaimRequest { Comment = "Receipt unclear" });

        Assert.Equal("Returned", returned.Status);
        Assert.Equal("Receipt unclear", returned.DecisionComment);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.InAppNotifications.SingleAsync(
            n => n.UserId == submitted.ClaimantUserId
                && n.EventType == NotificationEventTypes.TravelClaimReturned);
        Assert.Contains("Receipt unclear", notification.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Returns422()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(_client, director.AccessToken, submitted.Id);

        var response = await CaseTestData.SendApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Approve_Coordinator_Returns403()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendApproveTravelClaimAsync(
            _client,
            coordinator.AccessToken,
            submitted.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_Director_ReturnsSubmittedClaims()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var pending = await CaseTestData.ListDirectorPendingTravelClaimsAsync(_client, director.AccessToken);

        Assert.Contains(pending.Items, c => c.Id == submitted.Id);
        Assert.All(pending.Items, c => Assert.Equal("Submitted", c.Status));
        Assert.All(pending.Items, c => Assert.False(string.IsNullOrWhiteSpace(c.ClaimantEmail)));
    }

    [Fact]
    public async Task GetSupervisorTravelClaim_Coordinator_Returns200()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dto = await CaseTestData.GetSupervisorTravelClaimAsync(
            _client,
            coordinator.AccessToken,
            submitted.Id);

        Assert.Equal(submitted.Id, dto.Id);
        Assert.Equal("Submitted", dto.Status);
    }

    [Fact]
    public async Task CrisisQueue_IncludesPendingClaimRow_RemovedAfterApprove()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queueBefore = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        var claimRow = Assert.Single(queueBefore.Items, i => i.TravelClaimId == submitted.Id);
        Assert.Equal("travel_claim_pending", claimRow.RowType);
        Assert.Equal("neutral", claimRow.Severity);
        Assert.Null(claimRow.CourtSittingId);
        Assert.Null(claimRow.CrimeNumber);
        Assert.Null(claimRow.StNumber);
        Assert.Null(claimRow.ScheduledAtUtc);
        Assert.Contains("receipt(s)", claimRow.Detail, StringComparison.Ordinal);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(_client, director.AccessToken, submitted.Id);

        var queueAfter = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        Assert.DoesNotContain(queueAfter.Items, i => i.TravelClaimId == submitted.Id);
    }

    [Fact]
    public async Task Claimant_GetAfterApprove_IncludesDecisionComment()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id,
            new ApproveTravelClaimRequest { Comment = "Looks good" });

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendGetTravelClaimAsync(
            _client,
            fieldSession.AccessToken,
            submitted.Id);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        Assert.Equal("Looks good", envelope!.Data!.DecisionComment);
        Assert.Null(envelope.Data.ClaimantEmail);
    }

    [Fact]
    public async Task Approve_SelfAsClaimant_Returns422()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var directorId = await CaseTestData.GetUserIdByEmailAsync(_factory, AuthTestData.Email);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var claim = await db.TravelClaims.SingleAsync(c => c.Id == submitted.Id);
            claim.ClaimantUserId = directorId;
            await db.SaveChangesAsync();
        }

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendApproveTravelClaimAsync(
            _client,
            director.AccessToken,
            submitted.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_Coordinator_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListDirectorPendingTravelClaimsAsync(
            _client,
            coordinator.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_FieldWorker_Returns403()
    {
        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListDirectorPendingTravelClaimsAsync(
            _client,
            fieldSession.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSupervisorTravelClaim_FieldWorker_Returns403()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendGetSupervisorTravelClaimAsync(
            _client,
            fieldSession.AccessToken,
            submitted.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DownloadUrl_SubmittedClaim_DirectorCanRead()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var attachmentId = submitted.Attachments.Single().Id;
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var download = await CaseTestData.GetAttachmentDownloadUrlAsync(
            _client,
            director.AccessToken,
            attachmentId);

        Assert.False(string.IsNullOrWhiteSpace(download.DownloadUrl));
    }

    [Fact]
    public async Task CrisisQueue_CourtMissRowsSortBeforeClaimRows()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddDays(7)));

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(-1));
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        var courtIndex = queue.Items.ToList().FindIndex(i => i.RowType == "court_miss");
        var claimIndex = queue.Items.ToList().FindIndex(i => i.TravelClaimId == submitted.Id);

        Assert.True(courtIndex >= 0);
        Assert.True(claimIndex >= 0);
        Assert.True(courtIndex < claimIndex);
    }

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);

    private sealed record ApiMeta(string RequestId);
}
