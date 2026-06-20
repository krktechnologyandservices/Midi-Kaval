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
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseDuplicateCheckTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseDuplicateCheckTests(AuthWebApplicationFactory factory)
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
    public async Task CrimeMatch_Returns200_WithMatchSummary_AndNoAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditCountBefore = await db.AuditEvents.CountAsync();

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = request.CrimeNumber });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
        var match = Assert.Single(envelope!.Data!.Matches);
        Assert.Equal(caseId, match.CaseId);
        Assert.Equal("CrimeNumber", match.MatchedOn);
        Assert.Equal(request.CrimeNumber!.ToUpperInvariant(), match.CrimeNumber);

        Assert.Equal(auditCountBefore, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task StMatch_Returns200_WithStMatchedOn()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { StNumber = request.StNumber });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
        Assert.Equal("StNumber", Assert.Single(envelope!.Data!.Matches).MatchedOn);
    }

    [Fact]
    public async Task BothFieldsSameCase_Returns200_WithBothMatchedOn()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest
            {
                CrimeNumber = request.CrimeNumber,
                StNumber = request.StNumber,
            });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
        Assert.Equal("Both", Assert.Single(envelope!.Data!.Matches).MatchedOn);
    }

    [Fact]
    public async Task CrimeAndStDifferentCases_Returns200_WithTwoMatches()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseA = CaseTestData.BuildValidRequest();
        var caseB = CaseTestData.BuildValidRequest();
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseA);
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseB);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest
            {
                CrimeNumber = caseA.CrimeNumber,
                StNumber = caseB.StNumber,
            });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
        Assert.Equal(2, envelope!.Data!.Matches.Count);
        Assert.Contains(envelope.Data.Matches, m => m.MatchedOn == "CrimeNumber");
        Assert.Contains(envelope.Data.Matches, m => m.MatchedOn == "StNumber");
    }

    [Fact]
    public async Task NoMatch_Returns200_HasMatchFalse()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest
            {
                CrimeNumber = $"CR-{Guid.NewGuid():N}"[..12],
                StNumber = $"ST-{Guid.NewGuid():N}"[..12],
            });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.False(envelope?.Data?.HasMatch);
        Assert.Empty(envelope!.Data!.Matches);
        Assert.False(envelope.Data.HasMatch);
    }

    [Fact]
    public async Task CaseInsensitiveInput_Returns200_WithMatch()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.CrimeNumber = "CR-DUPCHECK";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = "cr-dupcheck" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
    }

    [Fact]
    public async Task CrossOrganisationCase_Returns200_NoMatch()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var otherOrgId = Guid.NewGuid();
        const string crime = "CR-OTHERORG";
        const string st = "ST-OTHERORG";
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Cases.Add(new Case
            {
                Id = Guid.NewGuid(),
                OrganisationId = otherOrgId,
                CrimeNumber = crime,
                StNumber = st,
                BeneficiaryName = "Other Org",
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

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = crime, StNumber = st });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.False(envelope?.Data?.HasMatch);
    }

    [Fact]
    public async Task EmptyIdentifiers_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = "   " });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("crimeNumber or stNumber", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverMaxLengthIdentifier_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = new string('X', 65) });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("crimeNumber", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NullBody_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases/check-duplicate")
        {
            Content = JsonContent.Create<CheckCaseDuplicateRequest?>(null),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Director_CanCheckDuplicate()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = $"CR-{Guid.NewGuid():N}"[..12] });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SocialWorker_Check_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = "CR-TEST" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Check_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = "CR-TEST" });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task EmptyRequestObject_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, new CheckCaseDuplicateRequest());
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OverMaxLengthStNumber_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { StNumber = new string('Y', 65) });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("stNumber", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TerminationExclusionCase_StillReturnedAsMatch()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        foreach (var target in new[]
                 {
                     "MaintainAndDevelopment",
                     "InterSectoralApproach",
                     "Rehabilitation",
                     "Reintegration",
                     "TerminationExclusion",
                 })
        {
            using var patch = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/v1/cases/{caseId}/stage")
            {
                Content = JsonContent.Create(new TransitionCaseStageRequest { TargetStage = target }),
            };
            patch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            var patchResponse = await _client.SendAsync(patch);
            patchResponse.EnsureSuccessStatusCode();
        }

        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = request.CrimeNumber });
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CheckCaseDuplicateResultDto>>();
        Assert.True(envelope?.Data?.HasMatch);
        Assert.Equal("TerminationExclusion", Assert.Single(envelope!.Data!.Matches).CurrentStage);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Check_Returns403_DeactivatedMessage()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var probeClient = _factory.CreateClient();
        using var httpRequest = CreateAuthorizedPost(
            session.AccessToken,
            new CheckCaseDuplicateRequest { CrimeNumber = "CR-TEST" });
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
    public async Task Unauthenticated_Check_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/cases/check-duplicate",
            new CheckCaseDuplicateRequest { CrimeNumber = "CR-TEST" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDuplicate_StillReturns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseCountBefore = await db.Cases.CountAsync();
        var auditCountBefore = await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseCreated);

        var duplicate = CaseTestData.BuildValidRequest();
        duplicate.CrimeNumber = request.CrimeNumber;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases")
        {
            Content = JsonContent.Create(duplicate),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(caseCountBefore, await db.Cases.CountAsync());
        Assert.Equal(auditCountBefore, await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseCreated));
    }

    private static HttpRequestMessage CreateAuthorizedPost(
        string accessToken,
        CheckCaseDuplicateRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases/check-duplicate")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return httpRequest;
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
