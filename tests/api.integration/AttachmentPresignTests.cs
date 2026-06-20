using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Attachments;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AttachmentPresignTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AttachmentPresignTests(AuthWebApplicationFactory factory)
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
    public async Task HappyPath_PresignPutConfirm_PersistsConfirmedAttachmentAndAudits()
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
            CaseTestData.BuildCaseNoteRequest("Visit", "Note with attachment."));

        var content = "attachment-bytes"u8.ToArray();
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: content.Length));

        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);

        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            socialSession.AccessToken,
            presign.AttachmentId);

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.False(string.IsNullOrWhiteSpace(confirmed.DownloadUrl));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attachment = await db.Attachments.SingleAsync(a => a.Id == presign.AttachmentId);
        Assert.Equal(AttachmentStatus.Confirmed, attachment.Status);
        Assert.NotNull(attachment.ConfirmedAtUtc);

        var audits = await db.AuditEvents
            .Where(e => e.OrganisationId == AuthTestData.OrganisationId)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync();
        Assert.Contains(audits, e => e.EventType == AuditEventTypes.AttachmentPresignIssued);
        Assert.Contains(audits, e => e.EventType == AuditEventTypes.AttachmentConfirmed);
    }

    [Fact]
    public async Task ListNotes_AfterConfirm_IncludesAttachmentSummary()
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

        var content = new byte[] { 1, 2, 3, 4 };
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await CaseTestData.ConfirmAttachmentAsync(_client, socialSession.AccessToken, presign.AttachmentId);

        var list = await CaseTestData.ListCaseNotesAsync(_client, socialSession.AccessToken, caseId);
        var listedNote = Assert.Single(list.Items, item => item.Id == note.Id);
        var summary = Assert.Single(listedNote.Attachments);
        Assert.Equal(presign.AttachmentId, summary.Id);
        Assert.Equal("evidence.jpg", summary.OriginalFileName);
        Assert.Equal("image/jpeg", summary.ContentType);
    }

    [Fact]
    public async Task GetDownloadUrl_ReturnsFetchableBlob()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var content = "download-test"u8.ToArray();
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        var download = await CaseTestData.GetAttachmentDownloadUrlAsync(
            _client,
            coordinator.AccessToken,
            confirmed.Id);

        using var downloadClient = new HttpClient();
        var blobResponse = await downloadClient.GetAsync(download.DownloadUrl);
        blobResponse.EnsureSuccessStatusCode();
        var downloaded = await blobResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task Coordinator_PresignConfirm_OnAnyOrgCaseNote_Succeeds()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var content = new byte[] { 9, 8, 7 };
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);

        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.Equal("Confirmed", confirmed.Status);
    }

    [Fact]
    public async Task SocialWorker_AssignedCase_PresignConfirm_Succeeds()
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

        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));
        var content = new byte[] { 1 };
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            socialSession.AccessToken,
            presign.AttachmentId);

        Assert.Equal("Confirmed", confirmed.Status);
    }

    [Fact]
    public async Task CaseWorker_AssignedCase_PresignConfirm_Succeeds()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
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

        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("Intervention", "Case worker note"));

        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            caseWorkerSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, [5]);
        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            caseWorkerSession.AccessToken,
            presign.AttachmentId);

        Assert.Equal("Confirmed", confirmed.Status);
    }

    [Fact]
    public async Task SocialWorker_OtherWorkersNote_PresignConfirmDownload_Return403()
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
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            socialSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var presignResponse = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            caseWorkerSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));
        Assert.Equal(HttpStatusCode.Forbidden, presignResponse.StatusCode);

        var coordinatorPresign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));
        await CaseTestData.PutBlobAsync(coordinatorPresign.UploadUrl, coordinatorPresign.RequiredHeaders, [1]);

        var confirmResponse = await CaseTestData.SendConfirmAttachmentAsync(
            _client,
            caseWorkerSession.AccessToken,
            coordinatorPresign.AttachmentId);
        Assert.Equal(HttpStatusCode.Forbidden, confirmResponse.StatusCode);

        var confirmed = await CaseTestData.ConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            coordinatorPresign.AttachmentId);

        var downloadResponse = await CaseTestData.SendGetAttachmentDownloadUrlAsync(
            _client,
            caseWorkerSession.AccessToken,
            confirmed.Id);
        Assert.Equal(HttpStatusCode.Forbidden, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_UnassignedCaseNote_Presign_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            socialSession.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Presign_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == coordinator.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);
    }

    [Fact]
    public async Task Presign_InvalidContentType_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, contentType: "application/x-msdownload"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Presign_OversizeFileSizeBytes_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: 10_485_761));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Presign_NumericResourceType_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, resourceType: "0"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithoutBlobPut_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));

        var response = await CaseTestData.SendConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_Twice_Returns409()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var content = new byte[] { 7, 7, 7 };
        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: content.Length));
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await CaseTestData.ConfirmAttachmentAsync(_client, coordinator.AccessToken, presign.AttachmentId);

        var response = await CaseTestData.SendConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Presign_MissingNote_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_OversizeBlobPut_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileSizeBytes: 100));

        var oversizeContent = new byte[101];
        Array.Fill(oversizeContent, (byte)'x');
        await CaseTestData.PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, oversizeContent);

        var response = await CaseTestData.SendConfirmAttachmentAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_PendingAttachment_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var presign = await CaseTestData.PresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id));

        var response = await CaseTestData.SendGetAttachmentDownloadUrlAsync(
            _client,
            coordinator.AccessToken,
            presign.AttachmentId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Presign_FileNameTooLong_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var note = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        var response = await CaseTestData.SendPresignAttachmentAsync(
            _client,
            coordinator.AccessToken,
            CaseTestData.BuildPresignRequest(note.Id, fileName: new string('a', 256)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Presign_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/attachments/presign",
            CaseTestData.BuildPresignRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
