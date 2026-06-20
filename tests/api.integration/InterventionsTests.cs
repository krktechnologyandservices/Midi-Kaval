using System.Net;
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
public class InterventionsTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public InterventionsTests(AuthWebApplicationFactory factory)
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
    public async Task Assignee_CreateNeededIntervention_Returns201_PersistsRow_AndAudit()
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

        var dueAtUtc = DateTime.UtcNow.AddDays(3);
        var request = CaseTestData.BuildInterventionRequest(
            direction: "Needed",
            assignedStaffUserId: caseWorkerId,
            dueAtUtc: dueAtUtc);

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<InterventionDto>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal("Needed", envelope.Data.Direction);
        Assert.Equal("Counselling", envelope.Data.CategoryName);
        Assert.Equal(caseWorkerId, envelope.Data.AssignedStaffUserId);
        Assert.Equal(RbacTestData.CaseWorkerEmail, envelope.Data.AssignedStaffEmail);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.Interventions.SingleAsync(i => i.Id == envelope.Data.Id);
        Assert.Equal(caseId, row.CaseId);
        Assert.Equal("Weekly counselling session for beneficiary.", row.Description);

        var caseIdText = caseId.ToString("D");
        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.InterventionCreated && e.ActorUserId == caseWorkerId)
            .ToListAsync();
        var audit = audits.Single(e => e.MetadataJson?.Contains(caseIdText, StringComparison.Ordinal) == true);
        Assert.Contains(envelope.Data.Id.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("description", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assignee_CreateProvidedIntervention_RequiresProvidedDate()
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

        var request = CaseTestData.BuildInterventionRequest(
            direction: "Provided",
            status: "Completed",
            dueAtUtc: null,
            providedAtUtc: DateTime.UtcNow.AddDays(-1),
            assignedStaffUserId: caseWorkerId);

        var created = await CaseTestData.CreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            request);

        Assert.Equal("Provided", created.Direction);
        Assert.NotNull(created.ProvidedAtUtc);
    }

    [Fact]
    public async Task Assignee_ListInterventions_Returns200_InCreationOrder()
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

        var first = await CaseTestData.CreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(
                categoryName: "First",
                assignedStaffUserId: caseWorkerId));
        var second = await CaseTestData.CreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(
                categoryName: "Second",
                assignedStaffUserId: caseWorkerId));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var secondRow = await db.Interventions.SingleAsync(i => i.Id == second.Id);
            secondRow.CreatedAtUtc = first.CreatedAtUtc.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var list = await CaseTestData.ListInterventionsAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId);

        Assert.Equal(2, list.Items.Count);
        Assert.Equal(second.Id, list.Items[0].Id);
        Assert.Equal(first.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task Assignee_UpdateInterventionStatus_Returns200_AndAudit()
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

        var created = await CaseTestData.CreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(assignedStaffUserId: caseWorkerId));

        var response = await CaseTestData.SendUpdateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            created.Id,
            new UpdateInterventionRequest
            {
                Status = "Completed",
                Outcome = "Counselling completed successfully.",
                ProvidedAtUtc = DateTime.UtcNow,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<InterventionDto>>();
        Assert.Equal("Completed", envelope!.Data!.Status);
        Assert.Equal("Counselling completed successfully.", envelope.Data.Outcome);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.InterventionUpdated)
            .ToListAsync();
        Assert.Contains(audits, e => e.MetadataJson?.Contains("Completed", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task UnassignedFieldWorker_CreateIntervention_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(assignedStaffUserId: caseWorkerId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem!.Detail);
    }

    [Fact]
    public async Task OtherWorker_ListInterventions_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var assigneeId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var otherWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(assigneeId));

        var otherSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListInterventionsAsync(
            _client,
            otherSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_CreateOnUnassignedCase_Returns201()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var created = await CaseTestData.CreateInterventionAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(assignedStaffUserId: caseWorkerId));

        Assert.Equal("Needed", created.Direction);
    }

    [Fact]
    public async Task CreateNeededIntervention_WithPastDueDate_Returns422()
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

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(
                assignedStaffUserId: caseWorkerId,
                dueAtUtc: DateTime.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntervention_WithInvalidPriority_Returns400()
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

        var request = CaseTestData.BuildInterventionRequest(
            priority: "Urgent",
            assignedStaffUserId: caseWorkerId);

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntervention_WithInvalidStatus_Returns400()
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

        var request = CaseTestData.BuildInterventionRequest(
            status: "Pending",
            assignedStaffUserId: caseWorkerId);

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntervention_InactiveAssignee_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == socialWorkerId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(assignedStaffUserId: socialWorkerId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_UnassignedCase_ListInterventions_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var socialSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListInterventionsAsync(
            _client,
            socialSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem!.Detail);
    }

    [Fact]
    public async Task ListInterventions_EmptyCase_Returns200WithEmptyItems()
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

        var list = await CaseTestData.ListInterventionsAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId);

        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task GetIntervention_UnknownId_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/cases/{caseId:D}/interventions/{Guid.NewGuid():D}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            coordinator.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
