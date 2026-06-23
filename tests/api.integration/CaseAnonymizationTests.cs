using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseAnonymizationTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseAnonymizationTests(AuthWebApplicationFactory factory)
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
    public async Task AnonymizationJob_AnonymizesCasePastRetention()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        await TransitionToTerminationExclusionAsync(session.AccessToken, caseId);
        await SetCaseUpdatedAtPastRetentionAsync(caseId);

        await RunAnonymizationJobAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);

        Assert.Null(row.BeneficiaryContact);
        Assert.Null(row.BeneficiaryName);
        Assert.Null(row.BeneficiaryAge);
        Assert.Null(row.Latitude);
        Assert.Null(row.Longitude);
        Assert.Null(row.Landmark);
        Assert.NotNull(row.CrimeNumber);
        Assert.NotNull(row.StNumber);
        Assert.NotEqual(0, row.VisitCount);
    }

    [Fact]
    public async Task AnonymizationJob_SkipsCaseWithActiveLegalStay()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        await TransitionToTerminationExclusionAsync(session.AccessToken, caseId);
        await SetCaseUpdatedAtPastRetentionAsync(caseId);
        await SetActiveLegalStayAsync(caseId, true);

        await RunAnonymizationJobAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);

        Assert.NotNull(row.BeneficiaryContact);
        Assert.NotEqual(string.Empty, row.BeneficiaryName);
    }

    [Fact]
    public async Task AnonymizationJob_SkipsCaseWithinRetentionPeriod()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        await TransitionToTerminationExclusionAsync(session.AccessToken, caseId);
        // Leave UpdatedAtUtc as is (recent) — not past retention cutoff

        await RunAnonymizationJobAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);

        Assert.NotNull(row.BeneficiaryContact);
        Assert.NotEqual(string.Empty, row.BeneficiaryName);
    }

    [Fact]
    public async Task AnonymizationJob_PreservesOperationalFields()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        await using (var initScope = _factory.Services.CreateAsyncScope())
        {
            var initDb = initScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseRow = await initDb.Cases.SingleAsync(c => c.Id == caseId);
            caseRow.CrimeNumber = "CRIME-001";
            caseRow.StNumber = "ST-001";
            caseRow.TypeOfOffence = "Test Offence";
            await initDb.SaveChangesAsync();
        }

        await TransitionToTerminationExclusionAsync(session.AccessToken, caseId);
        await SetCaseUpdatedAtPastRetentionAsync(caseId);

        await RunAnonymizationJobAsync();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifyRow = await verifyDb.Cases.SingleAsync(c => c.Id == caseId);

        Assert.Equal("CRIME-001", verifyRow.CrimeNumber);
        Assert.Equal("ST-001", verifyRow.StNumber);
        Assert.Equal("Test Offence", verifyRow.TypeOfOffence);
        Assert.Equal(CaseStage.TerminationExclusion, verifyRow.CurrentStage);
    }

    [Fact]
    public async Task AnonymizationJob_WritesAuditEvent()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        await TransitionToTerminationExclusionAsync(session.AccessToken, caseId);
        await SetCaseUpdatedAtPastRetentionAsync(caseId);

        await RunAnonymizationJobAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditEvents.SingleOrDefaultAsync(e => e.EventType == AuditEventTypes.CaseAnonymized);

        Assert.NotNull(audit);
        Assert.Null(audit.ActorUserId);
        Assert.Null(audit.SubjectUserId);
        Assert.Contains("\"count\"", audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("\"cutoffDate\"", audit.MetadataJson, StringComparison.Ordinal);
    }

    private async Task RunAnonymizationJobAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<CaseAnonymizationJobRunner>();
        await runner.RunAsync();
    }

    private async Task TransitionToTerminationExclusionAsync(string accessToken, Guid caseId)
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
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            };
            using var response = await _client.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task SetCaseUpdatedAtPastRetentionAsync(Guid caseId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        row.UpdatedAtUtc = DateTime.UtcNow.AddYears(-10);
        await db.SaveChangesAsync();
    }

    private async Task SetActiveLegalStayAsync(Guid caseId, bool active)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Cases.SingleAsync(c => c.Id == caseId);
        row.ActiveLegalStay = active;
        await db.SaveChangesAsync();
    }
}
