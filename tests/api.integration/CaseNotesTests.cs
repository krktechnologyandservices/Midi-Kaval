using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseNotesTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseNotesTests(AuthWebApplicationFactory factory)
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
    public async Task Assignee_CreateNote_Returns201_PersistsRow_AndAudit()
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

        var request = CaseTestData.BuildCaseNoteRequest("Visit", "Completed home visit with beneficiary.");
        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            socialSession.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseNoteDto>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal("Visit", envelope.Data.NoteType);
        Assert.Equal(request.BodyText, envelope.Data.BodyText);
        Assert.Equal(socialWorkerId, envelope.Data.AuthorUserId);
        Assert.Equal(RbacTestData.SocialWorkerEmail, envelope.Data.AuthorEmail);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.CaseNotes.SingleAsync(n => n.Id == envelope.Data.Id);
        Assert.Equal(caseId, row.CaseId);
        Assert.Equal("Completed home visit with beneficiary.", row.BodyText);

        var caseIdText = caseId.ToString("D");
        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseNoteCreated && e.ActorUserId == socialWorkerId)
            .ToListAsync();
        var audit = audits.Single(e => e.MetadataJson?.Contains(caseIdText, StringComparison.Ordinal) == true);
        Assert.Contains(envelope.Data.Id.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("Visit", audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bodyText", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(socialWorkerId, audit.SubjectUserId);
    }

    [Fact]
    public async Task Assignee_ListNotes_Returns200_ChronologicalOrder()
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

        var first = await CaseTestData.CreateCaseNoteAsync(
            _client,
            socialSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("General", "First note"));
        var second = await CaseTestData.CreateCaseNoteAsync(
            _client,
            socialSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("Court", "Second note"));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var secondRow = await db.CaseNotes.SingleAsync(n => n.Id == second.Id);
            secondRow.CreatedAtUtc = first.CreatedAtUtc.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var list = await CaseTestData.ListCaseNotesAsync(_client, socialSession.AccessToken, caseId);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal(second.Id, list.Items[0].Id);
        Assert.Equal(first.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task Coordinator_CreateAndList_Returns201And200()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var created = await CaseTestData.CreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("Intervention", "Coordinator intervention note"));

        Assert.Equal("Intervention", created.NoteType);

        var list = await CaseTestData.ListCaseNotesAsync(_client, coordinator.AccessToken, caseId);
        Assert.Single(list.Items);
        Assert.Equal(created.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task Director_CreateAndList_Returns201And200()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var created = await CaseTestData.CreateCaseNoteAsync(
            _client,
            director.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("Court", "Director court prep note"));

        var list = await CaseTestData.ListCaseNotesAsync(_client, director.AccessToken, caseId);
        Assert.Single(list.Items);
        Assert.Equal(created.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task SocialWorker_OtherWorkersCase_Post_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task SocialWorker_OtherWorkersCase_Get_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListCaseNotesAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_AssignedCase_Returns201()
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

        var created = await CaseTestData.CreateCaseNoteAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest("Intervention", "Case worker intervention note"));

        Assert.Equal("Intervention", created.NoteType);
    }

    [Fact]
    public async Task CreateNote_MissingBody_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_WhitespaceBody_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(bodyText: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_BodyTooLong_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(bodyText: new string('x', 4001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_InvalidNoteType_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(noteType: "InvalidType"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Visit", problem?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateNote_NumericNoteType_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(noteType: "0"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_DeactivatedAuthor_OmitsEmail()
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

        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);

        var list = await CaseTestData.ListCaseNotesAsync(_client, director.AccessToken, caseId);
        var item = Assert.Single(list.Items, n => n.Id == note.Id);
        Assert.Null(item.AuthorEmail);
    }

    [Fact]
    public async Task CreateNote_ActionRequiredWithoutDueDate_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(actionRequired: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_PastDueDate_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest(
                actionDueAtUtc: DateTime.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Action due date must be in the future.", problem?.Detail);
    }

    [Fact]
    public async Task CreateNote_UnknownCase_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            Guid.NewGuid(),
            CaseTestData.BuildCaseNoteRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/cases/{Guid.NewGuid():D}/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_UnassignedCase_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListCaseNotesAsync(
            _client,
            socialSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_EmptyTimeline_Returns200_EmptyItems()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var list = await CaseTestData.ListCaseNotesAsync(_client, coordinator.AccessToken, caseId);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task DeactivatedCoordinator_CreateNote_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == coordinator.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await CaseTestData.SendCreateCaseNoteAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCaseNoteRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);
    }

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
