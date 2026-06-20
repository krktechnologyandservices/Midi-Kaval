using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Attachments;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class TravelClaimApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public TravelClaimApiTests(AuthWebApplicationFactory factory)
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

    [Theory]
    [InlineData(RbacTestData.SocialWorkerEmail)]
    [InlineData(RbacTestData.CaseWorkerEmail)]
    public async Task HappyPath_CreatePresignConfirmSubmit_PersistsSubmittedClaim(string fieldWorkerEmail)
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            fieldWorkerEmail);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            fieldWorkerEmail,
            AuthTestData.Password);

        var content = "receipt-bytes"u8.ToArray();
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            fieldSession.AccessToken,
            CaseTestData.BuildPresignRequest(
                claim.Id,
                resourceType: "TravelClaim",
                fileSizeBytes: content.Length));

        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await CaseTestData.ConfirmAttachmentAsync(_client, fieldSession.AccessToken, presign.AttachmentId);

        var submitted = await CaseTestData.SubmitTravelClaimAsync(
            _client,
            fieldSession.AccessToken,
            claim.Id);

        Assert.Equal("Submitted", submitted.Status);
        Assert.NotNull(submitted.SubmittedAtUtc);
        Assert.Single(submitted.Attachments);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.AuditEvents
            .Where(e => e.OrganisationId == AuthTestData.OrganisationId)
            .ToListAsync();
        Assert.Contains(audits, e => e.EventType == AuditEventTypes.TravelClaimCreated);
        Assert.Contains(audits, e => e.EventType == AuditEventTypes.TravelClaimSubmitted);
    }

    [Fact]
    public async Task Submit_BusWithoutReceipt_Returns422()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Bus");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendSubmitTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            claim.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Receipt image is required", problem!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_OtherWithoutReceipt_Returns200()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var submitted = await CaseTestData.SubmitTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            claim.Id);

        Assert.Equal("Submitted", submitted.Status);
    }

    [Fact]
    public async Task Patch_SubmittedClaim_Returns422()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.SubmitTravelClaimAsync(_client, socialSession.AccessToken, claim.Id);

        var response = await CaseTestData.SendPatchTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            claim.Id,
            new UpdateTravelClaimRequest { Notes = "Updated after submit" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WrongClaimant_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var claim = await CaseTestData.CreateTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildCreateTravelClaimRequest(caseId));

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendPatchTravelClaimAsync(
            _client,
            caseWorkerSession.AccessToken,
            claim.Id,
            new UpdateTravelClaimRequest { Notes = "Not my claim" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_OtherFieldWorkerClaim_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var claim = await CaseTestData.CreateTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildCreateTravelClaimRequest(caseId));

        var caseWorkerCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseWorkerCaseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendGetTravelClaimAsync(
            _client,
            caseWorkerSession.AccessToken,
            claim.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Presign_SubmittedClaim_Returns422()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.SubmitTravelClaimAsync(_client, socialSession.AccessToken, claim.Id);

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(claim.Id, resourceType: "TravelClaim"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyTotals_Coordinator_ReturnsSubmittedTotals()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.SubmitTravelClaimAsync(_client, socialSession.AccessToken, claim.Id);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListTravelClaimMonthlyTotalsAsync(
            _client,
            coordinator.AccessToken,
            2026,
            6);

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<MonthlyTotalsApiEnvelope>();
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope!.Meta.TotalCount);
        Assert.Single(envelope.Data.Items);
        Assert.Equal(claim.Amount, envelope.Data.Items[0].TotalAmount);
        Assert.Equal(1, envelope.Data.Items[0].ClaimCount);
        Assert.Equal(RbacTestData.SocialWorkerEmail, envelope.Data.Items[0].StaffEmail);
    }

    [Fact]
    public async Task MonthlyTotals_InvalidMonth_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListTravelClaimMonthlyTotalsAsync(
            _client,
            coordinator.AccessToken,
            2026,
            13);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyTotals_FieldWorker_Returns403()
    {
        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListTravelClaimMonthlyTotalsAsync(
            _client,
            socialSession.AccessToken,
            2026,
            6);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Presign_CaseNoteAndTravelClaim_BothSucceed_InvalidResourceTypeReturns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            socialSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var notePresign = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, resourceType: "CaseNote"));
        Assert.Equal(HttpStatusCode.OK, notePresign.StatusCode);

        var claim = await CaseTestData.CreateTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildCreateTravelClaimRequest(caseId));

        var claimPresign = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(claim.Id, resourceType: "TravelClaim"));
        Assert.Equal(HttpStatusCode.OK, claimPresign.StatusCode);

        var invalidPresign = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(claim.Id, resourceType: "Invoice"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPresign.StatusCode);
    }

    [Fact]
    public async Task DownloadUrl_SubmittedClaim_CoordinatorCanRead()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var content = new byte[] { 9, 8, 7 };
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(
                claim.Id,
                resourceType: "TravelClaim",
                fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await CaseTestData.ConfirmAttachmentAsync(_client, socialSession.AccessToken, presign.AttachmentId);
        await CaseTestData.SubmitTravelClaimAsync(_client, socialSession.AccessToken, claim.Id);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var download = await CaseTestData.GetAttachmentDownloadUrlAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.False(string.IsNullOrWhiteSpace(download.DownloadUrl));
    }

    [Fact]
    public async Task Create_AutoWithoutAutoNumber_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreateTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildCreateTravelClaimRequest(caseId, transportMode: "Auto"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_SubmittedClaim_Returns422()
    {
        var (claim, _) = await CaseTestData.CreateAssignedCaseAndTravelClaimAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail,
            transportMode: "Other");

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var content = new byte[] { 1, 2, 3 };
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(
                claim.Id,
                resourceType: "TravelClaim",
                fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);

        await CaseTestData.SubmitTravelClaimAsync(_client, socialSession.AccessToken, claim.Id);

        var response = await CaseTestData.SendConfirmAttachmentAsync(
            _client,
            socialSession.AccessToken,
            presign.AttachmentId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyTotals_InvalidYear_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListTravelClaimMonthlyTotalsAsync(
            _client,
            coordinator.AccessToken,
            1999,
            6);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WrongClaimant_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var claim = await CaseTestData.CreateTravelClaimAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildCreateTravelClaimRequest(caseId, transportMode: "Other"));

        var caseWorkerCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseWorkerCaseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendSubmitTravelClaimAsync(
            _client,
            caseWorkerSession.AccessToken,
            claim.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record MonthlyTotalsApiMeta(string RequestId, int TotalCount);

    private sealed record MonthlyTotalsApiEnvelope(
        TravelClaimMonthlyTotalsResultDto Data,
        MonthlyTotalsApiMeta Meta);
}
