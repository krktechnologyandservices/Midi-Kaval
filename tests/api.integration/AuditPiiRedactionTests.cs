using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AuditPiiRedactionTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuditPiiRedactionTests(AuthWebApplicationFactory factory)
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
    public async Task CaseCreated_DoesNotContainPiiInAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var audit = await db.AuditEvents.SingleAsync(
            e => e.EventType == AuditEventTypes.CaseCreated && e.ActorUserId == session.UserId);

        // Verify no PII fields in metadata
        Assert.DoesNotContain("beneficiaryName", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beneficiaryAge", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beneficiaryContact", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);

        // Verify expected operational fields are present
        Assert.Contains(caseId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("crimeNumber", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stNumber", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaseMerged_DoesNotContainPiiInAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var existingRequest = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, existingRequest);

        // Prepare a merge draft with matching crime number
        var draft = CaseTestData.BuildValidRequest();
        draft.CrimeNumber = existingRequest.CrimeNumber;
        draft.StNumber = existingRequest.StNumber;

        // Call merge endpoint directly
        using var mergeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/merge")
        {
            Content = JsonContent.Create(draft),
        };
        mergeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var mergeResponse = await _client.SendAsync(mergeRequest);
        Assert.Equal(HttpStatusCode.OK, mergeResponse.StatusCode);

        // Read the audit event scoped by CaseId to avoid ambient state pollution
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var audit = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CaseMerged
                && e.ActorUserId == session.UserId
                && e.MetadataJson.Contains(caseId.ToString("D")))
            .SingleAsync();

        // Verify draftSnapshot exists but does NOT contain PII fields
        Assert.Contains("draftSnapshot", audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("beneficiaryName", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beneficiaryAge", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beneficiaryContact", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);

        // Verify non-PII fields in draftSnapshot are preserved
        Assert.Contains("typeOfOffence", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("domicile", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CasePiiRevealed_RetainsMetadata()
    {
        // Create a POCSO case as coordinator, then transfer to social worker
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
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

        // Social worker reveals PII (they have step-up auth from login flow)
        using var revealRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/cases/{caseId:D}/reveal-pii");
        revealRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", worker.AccessToken);
        var revealResponse = await _client.SendAsync(revealRequest);
        Assert.Equal(HttpStatusCode.OK, revealResponse.StatusCode);

        // Read the audit event
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var audit = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.CasePiiRevealed && e.ActorUserId == worker.UserId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstAsync();

        // Verify metadata retains the expected format (caseId, userId) — no raw beneficiary data
        Assert.Contains(caseId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains(worker.UserId.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("beneficiaryName", audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }
}
