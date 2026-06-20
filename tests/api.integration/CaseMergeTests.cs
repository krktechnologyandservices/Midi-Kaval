using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseMergeTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseMergeTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_MergeMatchingCrime_Returns200_WritesAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;
        draft.BeneficiaryName = "Different Intake Name";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseCountBefore = await db.Cases.CountAsync();

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDto>>();
        Assert.Equal(caseId, envelope!.Data!.Id);
        Assert.Equal(existing.BeneficiaryName!.Trim(), envelope.Data.BeneficiaryName);

        Assert.Equal(caseCountBefore, await db.Cases.CountAsync());

        var audit = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseMerged && e.ActorUserId == session.UserId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstAsync();
        Assert.Contains(caseId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("draftSnapshot", audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Director_Merge_Returns200()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var existing = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BothIdentifiersMatchTarget_Returns200()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FillEmptyBeneficiaryContact_UpdatesTarget()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        existing.BeneficiaryContact = null;
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;
        draft.BeneficiaryContact = "+911234567890";

        await CaseTestData.MergeCaseAsync(_client, session.AccessToken, caseId, draft);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal("+911234567890", row.BeneficiaryContact);
    }

    [Fact]
    public async Task FillEmptyBeneficiaryAge_UpdatesTarget()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        existing.BeneficiaryAge = null;
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;
        draft.BeneficiaryAge = 22;

        await CaseTestData.MergeCaseAsync(_client, session.AccessToken, caseId, draft);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(22, row.BeneficiaryAge);
    }

    [Fact]
    public async Task AuditOnlyMerge_DoesNotChangeUpdatedAtUtc()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        existing.BeneficiaryContact = "+919999999999";
        existing.BeneficiaryAge = 20;
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Cases.SingleAsync(c => c.Id == caseId);
        var updatedAtBefore = before.UpdatedAtUtc;

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;
        draft.BeneficiaryContact = "+918888888888";
        draft.BeneficiaryAge = 25;

        await CaseTestData.MergeCaseAsync(_client, session.AccessToken, caseId, draft);

        var after = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(updatedAtBefore, after.UpdatedAtUtc);
        Assert.Equal("+919999999999", after.BeneficiaryContact);
        Assert.Equal(20, after.BeneficiaryAge);

        Assert.True(await db.AuditEvents.AnyAsync(e => e.EventType == AuditEventTypes.CaseMerged));
    }

    [Fact]
    public async Task DraftStPointsToOtherCase_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseA = CaseTestData.BuildValidRequest();
        var caseB = CaseTestData.BuildValidRequest();
        var caseAId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseA);
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseB);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = caseA.CrimeNumber;
        draft.StNumber = caseB.StNumber;

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseAId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DraftCrimePointsToOtherCase_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseA = CaseTestData.BuildValidRequest();
        var caseB = CaseTestData.BuildValidRequest();
        var caseAId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseA);
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, caseB);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = caseB.CrimeNumber;
        draft.StNumber = caseA.StNumber;

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseAId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DraftIdentifiersDoNotMatchTarget_Returns422()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Draft identifiers do not match the target case.", problem?.Detail);
    }

    [Fact]
    public async Task UnknownTarget_Returns404()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var draft = CaseTestData.BuildValidRequest();

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(
            session.AccessToken,
            Guid.NewGuid(),
            draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SocialWorker_Merge_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var draft = CaseTestData.BuildValidRequest();

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, Guid.NewGuid(), draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Merge_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);
        var draft = CaseTestData.BuildValidRequest();

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, Guid.NewGuid(), draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Merge_Returns403()
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
        probeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(
            session.AccessToken,
            Guid.NewGuid(),
            CaseTestData.BuildValidRequest());
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
    public async Task MissingBeneficiaryName_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existing = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existing);

        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existing.CrimeNumber;
        draft.StNumber = existing.StNumber;
        draft.BeneficiaryName = "   ";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditCountBefore = await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseMerged);

        using var httpRequest = CaseTestData.CreateAuthorizedMergePost(session.AccessToken, caseId, draft);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(auditCountBefore, await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseMerged));
    }

    [Fact]
    public async Task Unauthenticated_Merge_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/cases/{Guid.NewGuid():D}/merge",
            CaseTestData.BuildValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
