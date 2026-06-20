using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseStageTransitionTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseStageTransitionTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_Transition_Returns200_PersistsHistory_AndAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest
            {
                TargetStage = "MaintainAndDevelopment",
                Notes = "ICP draft completed.",
            });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDto>>();
        Assert.Equal("MaintainAndDevelopment", envelope?.Data?.CurrentStage);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStage.MaintainAndDevelopment, row.CurrentStage);
        Assert.True(row.UpdatedAtUtc >= row.CreatedAtUtc);

        var history = await db.CaseStageTransitions.SingleAsync(t => t.CaseId == caseId);
        Assert.Equal(CaseStage.ProcessInitiation, history.FromStage);
        Assert.Equal(CaseStage.MaintainAndDevelopment, history.ToStage);
        Assert.Equal("ICP draft completed.", history.Notes);
        Assert.Equal(session.UserId, history.CreatedByUserId);

        var caseIdString = caseId.ToString("D");
        var stageAudits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseStageChanged && e.ActorUserId == session.UserId)
            .ToListAsync();
        var audit = Assert.Single(stageAudits, e => e.MetadataJson?.Contains(caseIdString, StringComparison.Ordinal) == true);
        Assert.Contains(caseIdString, audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("ProcessInitiation", audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("MaintainAndDevelopment", audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Director_CanTransitionStage()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FullLifecycle_TransitionsThroughAllSixStages()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        foreach (var target in new[]
                 {
                     "MaintainAndDevelopment",
                     "InterSectoralApproach",
                     "Rehabilitation",
                     "Reintegration",
                     "TerminationExclusion",
                 })
        {
            using var httpRequest = CreateAuthorizedPatch(
                session.AccessToken,
                caseId,
                new TransitionCaseStageRequest { TargetStage = target });
            var response = await _client.SendAsync(httpRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStage.TerminationExclusion, row.CurrentStage);
        Assert.Equal(5, await db.CaseStageTransitions.CountAsync(t => t.CaseId == caseId));
    }

    [Fact]
    public async Task SkipStage_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "InterSectoralApproach" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Unprocessable Entity", problem?.Title);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStage.ProcessInitiation, row.CurrentStage);
        Assert.Equal(0, await db.CaseStageTransitions.CountAsync(t => t.CaseId == caseId));
    }

    [Fact]
    public async Task BackwardTransition_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using (var first = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" }))
        {
            var firstResponse = await _client.SendAsync(first);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }

        using var second = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "ProcessInitiation" });
        var secondResponse = await _client.SendAsync(second);

        Assert.Equal((HttpStatusCode)422, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SameStage_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "ProcessInitiation" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task TerminalCase_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await AdvanceToTerminationAsync(session.AccessToken, caseId: null);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "TerminationExclusion" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task UnknownCase_Returns404()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            Guid.NewGuid(),
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTargetStage_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "NotAStage" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("targetStage", problem?.Detail, StringComparison.OrdinalIgnoreCase);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.CaseStageTransitions.CountAsync(t => t.CaseId == caseId));
    }

    [Fact]
    public async Task EmptyTargetStage_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "   " });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NumericTargetStage_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "1" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("targetStage", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotesTooLong_Returns400_AndDoesNotPersist()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest
            {
                TargetStage = "MaintainAndDevelopment",
                Notes = new string('x', 2001),
            });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("notes", problem?.Detail, StringComparison.OrdinalIgnoreCase);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(CaseStage.ProcessInitiation, row.CurrentStage);
        Assert.Equal(0, await db.CaseStageTransitions.CountAsync(t => t.CaseId == caseId));
    }

    [Fact]
    public async Task NullBody_Transition_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{caseId}/stage")
        {
            Content = JsonContent.Create<TransitionCaseStageRequest?>(null),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CrossOrganisationCase_Returns404()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var otherOrgId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = caseId,
                OrganisationId = otherOrgId,
                CrimeNumber = $"CR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                StNumber = $"ST-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                BeneficiaryName = "Other Org Case",
                TypeOfOffence = "Theft",
                OffenceClassification = OffenceClassification.Petty,
                Domicile = Domicile.Urban,
                CurrentStage = CaseStage.ProcessInitiation,
                CreatedByUserId = session.UserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            await db.SaveChangesAsync();
        }

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SocialWorker_Transition_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var caseId = await CaseTestData.CreateCaseAsync(
            _client,
            (await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender)).AccessToken);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Transition_Returns403()
    {
        var coordinatorSession = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinatorSession.AccessToken);

        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Transition_Returns403_DeactivatedMessage()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var probeClient = _factory.CreateClient();
        using var httpRequest = CreateAuthorizedPatch(
            session.AccessToken,
            caseId,
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });
        var response = await probeClient.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Unauthenticated_Transition_Returns401()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/cases/{Guid.NewGuid()}/stage",
            new TransitionCaseStageRequest { TargetStage = "MaintainAndDevelopment" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> AdvanceToTerminationAsync(string accessToken, Guid? caseId)
    {
        caseId ??= await CaseTestData.CreateCaseAsync(_client, accessToken);
        foreach (var target in new[]
                 {
                     "MaintainAndDevelopment",
                     "InterSectoralApproach",
                     "Rehabilitation",
                     "Reintegration",
                     "TerminationExclusion",
                 })
        {
            using var httpRequest = CreateAuthorizedPatch(
                accessToken,
                caseId.Value,
                new TransitionCaseStageRequest { TargetStage = target });
            var response = await _client.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }

        return caseId.Value;
    }

    private static HttpRequestMessage CreateAuthorizedPatch(
        string accessToken,
        Guid caseId,
        TransitionCaseStageRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{caseId}/stage")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return httpRequest;
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
