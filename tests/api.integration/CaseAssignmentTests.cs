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
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseAssignmentTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseAssignmentTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_Transfer_Returns200_PersistsAssignment_AuditAndHistory()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var request = CaseTestData.BuildTransferRequest(socialWorkerId);

        var detail = await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            request);

        Assert.Equal(socialWorkerId, detail.AssignedWorkerUserId);
        Assert.NotNull(detail.AssignedAtUtc);
        Assert.Null(detail.HandoffWhisper);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(socialWorkerId, row.AssignedWorkerId);
        Assert.NotNull(row.AssignedAtUtc);

        var assignment = await db.CaseAssignments.SingleAsync(a => a.CaseId == caseId);
        Assert.Null(assignment.FromWorkerId);
        Assert.Equal(socialWorkerId, assignment.ToWorkerId);
        Assert.Equal(request.PriorActions, assignment.PriorActions);
        Assert.Equal(request.OpenItems, assignment.OpenItems);
        Assert.Equal(request.NextVisitPurpose, assignment.NextVisitPurpose);
        Assert.Equal(coordinator.UserId, assignment.CreatedByUserId);

        var caseIdText = caseId.ToString("D");
        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseTransferred && e.ActorUserId == coordinator.UserId)
            .ToListAsync();
        var audit = audits.Single(e => e.MetadataJson.Contains(caseIdText, StringComparison.Ordinal));
        Assert.Contains(caseId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transfer_UnknownCase_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var response = await CaseTestData.SendTransferAsync(
            _client,
            coordinator.AccessToken,
            Guid.NewGuid(),
            CaseTestData.BuildTransferRequest(socialWorkerId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_ReTransfer_UpdatesAssignee_AndRecordsNewAssignmentRow()
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

        var detail = await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        Assert.Equal(caseWorkerId, detail.AssignedWorkerUserId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignments = await db.CaseAssignments
            .Where(a => a.CaseId == caseId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Null(assignments[0].FromWorkerId);
        Assert.Equal(socialWorkerId, assignments[0].ToWorkerId);
        Assert.Equal(socialWorkerId, assignments[1].FromWorkerId);
        Assert.Equal(caseWorkerId, assignments[1].ToWorkerId);
    }

    [Fact]
    public async Task Assignee_GetDetail_WithinSevenDays_ReturnsWhisper()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var socialWorkerId = socialSession.UserId;
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var request = CaseTestData.BuildTransferRequest(socialWorkerId);

        await CaseTestData.TransferCaseAsync(_client, coordinator.AccessToken, caseId, request);

        var detail = await CaseTestData.GetCaseDetailAsync(_client, socialSession.AccessToken, caseId);

        Assert.NotNull(detail.HandoffWhisper);
        Assert.Equal(request.PriorActions, detail.HandoffWhisper.PriorActions);
        Assert.Equal(request.OpenItems, detail.HandoffWhisper.OpenItems);
        Assert.Equal(request.NextVisitPurpose, detail.HandoffWhisper.NextVisitPurpose);
    }

    [Fact]
    public async Task Assignee_GetDetail_OnDayEight_ReturnsNullWhisper()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialSession.UserId));

        await CaseTestData.SetAssignedAtUtcAsync(
            _factory,
            caseId,
            DateTime.UtcNow.Date.AddDays(-7));

        var detail = await CaseTestData.GetCaseDetailAsync(_client, socialSession.AccessToken, caseId);
        Assert.Null(detail.HandoffWhisper);
    }

    [Fact]
    public async Task Coordinator_GetDetail_Returns200_WithoutWhisper()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var detail = await CaseTestData.GetCaseDetailAsync(_client, coordinator.AccessToken, caseId);
        Assert.Null(detail.HandoffWhisper);
        Assert.Equal(socialWorkerId, detail.AssignedWorkerUserId);
    }

    [Fact]
    public async Task Director_GetDetail_Returns200()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var detail = await CaseTestData.GetCaseDetailAsync(_client, director.AccessToken, caseId);
        Assert.Equal(caseId, detail.Id);
    }

    [Fact]
    public async Task SocialWorker_AssignedList_ReturnsOwnCases()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialSession.UserId));

        var envelope = await CaseTestData.ListAssignedCasesAsync(_client, socialSession.AccessToken);
        Assert.Contains(envelope.Data!.Items, i => i.Id == caseId);
        Assert.True(envelope.Meta.TotalCount >= 1);
    }

    [Fact]
    public async Task SocialWorker_OtherWorkerCase_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var response = await CaseTestData.SendGetCaseDetailAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Parity_AssignedList_DetailWhisper_OtherCase403_Transfer403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var request = CaseTestData.BuildTransferRequest(caseWorkerSession.UserId);

        await CaseTestData.TransferCaseAsync(_client, coordinator.AccessToken, caseId, request);

        var list = await CaseTestData.ListAssignedCasesAsync(_client, caseWorkerSession.AccessToken);
        Assert.Contains(list.Data!.Items, i => i.Id == caseId);

        var detail = await CaseTestData.GetCaseDetailAsync(_client, caseWorkerSession.AccessToken, caseId);
        Assert.NotNull(detail.HandoffWhisper);

        var otherCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            otherCaseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var forbidden = await CaseTestData.SendGetCaseDetailAsync(
            _client,
            caseWorkerSession.AccessToken,
            otherCaseId);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var transferForbidden = await CaseTestData.SendTransferAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));
        Assert.Equal(HttpStatusCode.Forbidden, transferForbidden.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_UnassignedCase_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendGetCaseDetailAsync(
            _client,
            socialSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_AssignedList_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var response = await CaseTestData.SendListAssignedAsync(_client, coordinator.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_SearchByAssignedWorkerUserId_ReturnsFiltered()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            coordinator.AccessToken,
            new Dictionary<string, string?>
            {
                ["assignedWorkerUserId"] = socialWorkerId.ToString("D"),
            });

        Assert.Contains(envelope.Data!.Items, i => i.Id == caseId);
        Assert.All(envelope.Data.Items, i => Assert.Equal(socialWorkerId, i.AssignedWorkerUserId));
    }

    [Fact]
    public async Task Search_AssignedWorkerUserId_TakesPrecedenceOverCreatedByUserId()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            coordinator.AccessToken,
            new Dictionary<string, string?>
            {
                ["assignedWorkerUserId"] = socialWorkerId.ToString("D"),
                ["createdByUserId"] = Guid.NewGuid().ToString("D"),
            });

        Assert.Contains(envelope.Data!.Items, i => i.Id == caseId);
    }

    [Fact]
    public async Task Preset_SaveAndLoad_WithAssignedWorkerUserId()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var created = await CaseTestData.CreateSearchPresetAsync(
            _client,
            coordinator.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = "Assigned to social",
                Filters = new CaseSearchFiltersDto
                {
                    AssignedWorkerUserId = socialWorkerId,
                },
            });

        var presets = await CaseTestData.ListSearchPresetsAsync(_client, coordinator.AccessToken);
        var loaded = presets.Single(p => p.Id == created.Id);
        Assert.Equal(socialWorkerId, loaded.Filters.AssignedWorkerUserId);
    }

    [Fact]
    public async Task Export_RespectsAssignedWorkerUserIdFilter()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var response = await CaseTestData.ExportCasesAsync(
            _client,
            coordinator.AccessToken,
            "xlsx",
            new Dictionary<string, string?>
            {
                ["assignedWorkerUserId"] = socialWorkerId.ToString("D"),
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task Transfer_InvalidAssigneeRole_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendTransferAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(coordinator.UserId));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_SameAssignee_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var request = CaseTestData.BuildTransferRequest(socialWorkerId);

        await CaseTestData.TransferCaseAsync(_client, coordinator.AccessToken, caseId, request);

        var response = await CaseTestData.SendTransferAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("already assigned", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_MissingHandoffField_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var request = CaseTestData.BuildTransferRequest(socialWorkerId);
        request.PriorActions = "   ";

        var response = await CaseTestData.SendTransferAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Transfer_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == coordinator.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await CaseTestData.SendTransferAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);
    }

    [Fact]
    public async Task Unauthenticated_GetDetail_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/cases/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_ListFieldWorkers_Returns200()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var workers = await CaseTestData.ListFieldWorkersAsync(_client, coordinator.AccessToken);

        Assert.Contains(workers, w => w.Email == RbacTestData.SocialWorkerEmail);
        Assert.Contains(workers, w => w.Email == RbacTestData.CaseWorkerEmail);
        Assert.All(workers, w => Assert.True(w.Role is UserRoles.SocialWorker or UserRoles.CaseWorker));
    }

    [Fact]
    public async Task TransferCase_EmailsCoordinatorAndAssignee()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        _factory.EmailSender.Clear();
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        Assert.Contains(
            _factory.EmailSender.Messages,
            m => m.To == RbacTestData.CoordinatorEmail
                && m.Subject.Contains("Case assigned", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            _factory.EmailSender.Messages,
            m => m.To == RbacTestData.SocialWorkerEmail);
        Assert.All(_factory.EmailSender.Messages, m =>
            Assert.DoesNotContain("Prior actions", m.Body, StringComparison.OrdinalIgnoreCase));
    }
}
