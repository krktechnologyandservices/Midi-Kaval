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
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CourtSittingsTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CourtSittingsTests(AuthWebApplicationFactory factory)
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
    public async Task Assignee_CreateCourtSitting_Returns201_PersistsRow_AndAudit()
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

        var scheduledAtUtc = DateTime.UtcNow.AddDays(5);
        var request = CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: scheduledAtUtc);

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<CourtSittingDto>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal("Upcoming", envelope.Data.Status);
        Assert.Equal("District Court Chennai", envelope.Data.CourtName);
        Assert.Equal("Bail hearing", envelope.Data.Purpose);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.CourtSittings.SingleAsync(s => s.Id == envelope.Data.Id);
        Assert.Equal(caseId, row.CaseId);
        Assert.Equal(scheduledAtUtc, row.ScheduledAtUtc, TimeSpan.FromSeconds(1));

        var audit = await db.AuditEvents
            .SingleAsync(e => e.EventType == AuditEventTypes.CourtSittingCreated);
        Assert.Contains(caseId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Bail hearing", audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUpcoming_WithPastDate_Returns422()
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

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(-1),
                status: "Upcoming"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreatePostponed_WithPastDate_Returns201()
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

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(-3),
                status: "Postponed"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithInvalidStatus_Returns400()
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

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(status: "Cancelled"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Upcoming", problem!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAttended_WithoutOutcome_Returns400()
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

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(status: "Attended"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TerminationExclusionCase_Create_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));
        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ListCourtSittings_ReturnsOrderedItems()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var later = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(10),
                courtName: "Later Court"));
        var earlier = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(3),
                courtName: "Earlier Court"));

        var list = await CaseTestData.ListCourtSittingsAsync(
            _client,
            coordinator.AccessToken,
            caseId);

        Assert.Equal(2, list.Items.Count);
        Assert.Equal(earlier.Id, list.Items[0].Id);
        Assert.Equal(later.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task PatchToAttended_RequiresOutcome_AndWritesAudit()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var created = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());

        var missingOutcome = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            created.Id,
            new UpdateCourtSittingRequest { Status = "Attended" });
        Assert.Equal(HttpStatusCode.BadRequest, missingOutcome.StatusCode);

        var response = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            created.Id,
            new UpdateCourtSittingRequest
            {
                Status = "Attended",
                Outcome = "Bail granted with conditions.",
                NextCourtAtUtc = DateTime.UtcNow.AddDays(30),
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<CourtSittingDto>>();
        Assert.Equal("Attended", envelope!.Data!.Status);
        Assert.Equal("Bail granted with conditions.", envelope.Data.Outcome);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditEvents
            .SingleAsync(e => e.EventType == AuditEventTypes.CourtSittingUpdated);
        Assert.Contains("Attended", audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Bail granted", audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnassignedFieldWorker_ListCourtSittings_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendListCourtSittingsAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem!.Detail);
    }

    [Fact]
    public async Task Upcoming_ReturnsOnlyAssigneeUpcoming_IncludesPastDue()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var assigneeId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var otherWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);

        var assigneeCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            assigneeCaseId,
            CaseTestData.BuildTransferRequest(assigneeId));

        var otherCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            otherCaseId,
            CaseTestData.BuildTransferRequest(otherWorkerId));

        await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            assigneeCaseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(2),
                courtName: "Future Sitting"));
        var pastDue = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            assigneeCaseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(5),
                courtName: "Past Due Sitting"));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == pastDue.Id);
            row.ScheduledAtUtc = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            otherCaseId,
            CaseTestData.BuildCourtSittingRequest(courtName: "Other Worker Sitting"));

        var assigneeSession = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var upcoming = await CaseTestData.ListUpcomingCourtSittingsAsync(
            _client,
            assigneeSession.AccessToken);

        Assert.Equal(2, upcoming.Data!.Items.Count);
        Assert.Equal(2, upcoming.Meta.TotalCount);
        Assert.All(upcoming.Data.Items, item => Assert.Equal(assigneeCaseId, item.CaseId));
        Assert.Contains(upcoming.Data.Items, i => i.IsPastDue);
        Assert.Contains(upcoming.Data.Items, i => !i.IsPastDue);
        Assert.DoesNotContain(upcoming.Data.Items, i => i.CourtName == "Other Worker Sitting");
    }

    [Fact]
    public async Task Upcoming_ExcludesTerminationExclusionCases()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var assigneeId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(assigneeId));

        await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());

        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);

        var assigneeSession = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var upcoming = await CaseTestData.ListUpcomingCourtSittingsAsync(
            _client,
            assigneeSession.AccessToken);

        Assert.Empty(upcoming.Data!.Items);
        Assert.Equal(0, upcoming.Meta.TotalCount);
    }

    [Fact]
    public async Task PocsoCase_UpcomingList_RedactsBeneficiaryName()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var request = CaseTestData.BuildValidRequest();
        request.SensitivityLevel = "POCSO";
        request.BeneficiaryName = "Ravi Kumar";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, request);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());

        var workerSession = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var upcoming = await CaseTestData.ListUpcomingCourtSittingsAsync(
            _client,
            workerSession.AccessToken);

        var item = Assert.Single(upcoming.Data!.Items);
        Assert.Equal("POCSO", item.Case.SensitivityLevel);
        Assert.Equal("R. K.", item.Case.BeneficiaryName);
    }

    [Fact]
    public async Task Coordinator_ListUpcoming_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        var response = await CaseTestData.SendListUpcomingCourtSittingsAsync(
            _client,
            coordinator.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedFieldWorker_ListUpcoming_Returns403()
    {
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == worker.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await CaseTestData.SendListUpcomingCourtSittingsAsync(_client, worker.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == worker.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UnassignedFieldWorker_CreateCourtSitting_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem!.Detail);
    }

    [Fact]
    public async Task Patch_StatusUpcomingWithPastScheduledAtUtc_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var created = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddDays(5),
                status: "Postponed"));

        var response = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            created.Id,
            new UpdateCourtSittingRequest
            {
                Status = "Upcoming",
                ScheduledAtUtc = DateTime.UtcNow.AddDays(-2),
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetCourtSitting_UnknownId_Returns404()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await CaseTestData.SendGetCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedFieldWorker_CreateCourtSitting_Returns403()
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

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == caseWorkerSession.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await CaseTestData.SendCreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == caseWorkerSession.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    private async Task TransitionCaseToTerminationExclusionAsync(string accessToken, Guid caseId)
    {
        foreach (var target in new[]
                 {
                     "MaintainAndDevelopment",
                     "InterSectoralApproach",
                     "Rehabilitation",
                     "Reintegration",
                     "TerminationExclusion",
                 })
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{caseId:D}/stage")
            {
                Content = JsonContent.Create(new TransitionCaseStageRequest { TargetStage = target }),
                Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken) },
            };

            var response = await _client.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }
    }
}
