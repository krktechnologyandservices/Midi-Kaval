using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Supervisor;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CourtMissEscalationJobTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CourtMissEscalationJobTests(AuthWebApplicationFactory factory)
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
    public async Task MissEscalationJob_CreatesNotificationsEmailsAudit_AndSuppressesDuplicates()
    {
        var (caseId, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: true);

        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.MissEscalatedAtUtc);

            var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
            Assert.NotNull(caseRow.CourtMissFlaggedAtUtc);

            var coordinatorId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CoordinatorEmail);
            var notifications = await db.InAppNotifications
                .Where(n => n.ResourceId == sitting.Id
                    && n.EventType == NotificationEventTypes.CourtMissEscalated)
                .ToListAsync();
            Assert.Single(notifications);
            Assert.Equal(coordinatorId, notifications[0].UserId);
            Assert.Equal(caseId, notifications[0].CaseId);

            var audit = await db.AuditEvents
                .SingleAsync(e => e.EventType == AuditEventTypes.CourtSittingMissEscalated
                    && e.MetadataJson!.Contains(sitting.Id.ToString("D"), StringComparison.Ordinal));
            Assert.Null(audit.ActorUserId);
        }

        var emails = _factory.EmailSender.Messages;
        Assert.Contains(emails, m => m.To == RbacTestData.CoordinatorEmail);
        Assert.DoesNotContain(emails, m => m.To == RbacTestData.CaseWorkerEmail);
        Assert.All(emails, m => Assert.DoesNotContain("beneficiary", m.Body, StringComparison.OrdinalIgnoreCase));

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        var item = Assert.Single(queue.Items);
        Assert.Equal("court_miss", item.RowType);
        Assert.Equal("critical", item.Severity);
        Assert.Equal("Court miss", item.BadgeLabel);
        Assert.Equal(sitting.Id, item.CourtSittingId);
        Assert.Equal(caseId, item.CaseId);

        var emailCount = _factory.EmailSender.Messages.Count;
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);
        Assert.Equal(emailCount, _factory.EmailSender.Messages.Count);
    }

    [Fact]
    public async Task CrisisQueue_Director_Returns200()
    {
        var (_, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: true);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, director.AccessToken);
        Assert.Contains(queue.Items, i => i.CourtSittingId == sitting.Id);
    }

    [Fact]
    public async Task CrisisQueue_FieldWorker_Returns403()
    {
        var (_, _, caseWorkerSession, _) = await CreatePastDueSittingAsync(assignCase: true);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var response = await CaseTestData.SendListCrisisQueueAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissEscalationJob_UnassignedCase_StillEscalates()
    {
        var (caseId, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: false);

        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
        Assert.NotNull(row.MissEscalatedAtUtc);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        var item = Assert.Single(queue.Items);
        Assert.Equal(caseId, item.CaseId);
        Assert.Null(item.AssignedWorkerUserId);
        Assert.Contains("Unassigned", item.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissEscalationJob_EmailFailure_DoesNotSetDedupFlag()
    {
        var (_, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: true);

        _factory.EmailSender.FailNextSend = true;
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await AssertNoEscalationAsync(sitting.Id);
    }

    [Fact]
    public async Task MissEscalationJob_AttendedStatus_DoesNotEscalate()
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

        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddDays(5)));

        var updateResponse = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            sitting.Id,
            new UpdateCourtSittingRequest
            {
                Status = "Attended",
                Outcome = "Hearing completed.",
            });
        updateResponse.EnsureSuccessStatusCode();

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(-1));

        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await AssertNoEscalationAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task MissEscalationJob_DeactivatedCoordinator_SkipsNotificationAndEmail()
    {
        var (_, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: true);

        var coordinatorId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CoordinatorEmail);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var coordinator = await db.Users.SingleAsync(u => u.Id == coordinatorId);
            coordinator.IsActive = false;
            await db.SaveChangesAsync();
        }

        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.MissEscalatedAtUtc);

            var notificationCount = await db.InAppNotifications
                .CountAsync(n => n.ResourceId == sitting.Id
                    && n.EventType == NotificationEventTypes.CourtMissEscalated);
            Assert.Equal(0, notificationCount);
        }

        Assert.Empty(_factory.EmailSender.Messages);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var coordinator = await db.Users.SingleAsync(u => u.Id == coordinatorId);
            coordinator.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PatchPostponed_RemovesCrisisQueueRow_AndClearsCaseFlag()
    {
        var (caseId, _, caseWorkerSession, sitting) = await CreatePastDueSittingAsync(assignCase: true);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var updateResponse = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            sitting.Id,
            new UpdateCourtSittingRequest
            {
                Status = "Postponed",
                ScheduledAtUtc = DateTime.UtcNow.AddDays(14),
            });
        updateResponse.EnsureSuccessStatusCode();

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        Assert.Empty(queue.Items);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Null(caseRow.CourtMissFlaggedAtUtc);
    }

    [Fact]
    public async Task MissEscalationJob_TerminalCase_DoesNotEscalate()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddDays(7)));

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(-1));

        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await AssertNoEscalationAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task MissEscalationJob_NotPastDue_DoesNotEscalate()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddDays(7)));

        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await AssertNoEscalationAsync(sitting.Id);
    }

    [Fact]
    public async Task PatchAttended_RemovesCrisisQueueRow_AndClearsCaseFlag()
    {
        var (caseId, _, caseWorkerSession, sitting) = await CreatePastDueSittingAsync(assignCase: true);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var updateResponse = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            sitting.Id,
            new UpdateCourtSittingRequest
            {
                Status = "Attended",
                Outcome = "Hearing completed.",
            });
        updateResponse.EnsureSuccessStatusCode();

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        Assert.Empty(queue.Items);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Null(caseRow.CourtMissFlaggedAtUtc);
    }

    [Fact]
    public async Task RescheduleToFuture_ClearsDedup_AndReEscalatesAfterPastDue()
    {
        var (caseId, _, _, sitting) = await CreatePastDueSittingAsync(assignCase: true);
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            sitting.Id,
            new UpdateCourtSittingRequest
            {
                ScheduledAtUtc = DateTime.UtcNow.AddDays(7),
            });
        response.EnsureSuccessStatusCode();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.Null(row.MissEscalatedAtUtc);

            var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
            Assert.Null(caseRow.CourtMissFlaggedAtUtc);
        }

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(-1));

        _factory.EmailSender.Clear();
        await CaseTestData.RunCourtMissEscalationJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.MissEscalatedAtUtc);

            var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
            Assert.NotNull(caseRow.CourtMissFlaggedAtUtc);
        }

        Assert.NotEmpty(_factory.EmailSender.Messages);
    }

    private async Task<(
        Guid CaseId,
        Guid CaseWorkerId,
        AuthSession CaseWorkerSession,
        CourtSittingDto Sitting)> CreatePastDueSittingAsync(bool assignCase)
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        AuthSession caseWorkerSession = coordinator;
        if (assignCase)
        {
            await CaseTestData.TransferCaseAsync(
                _client,
                coordinator.AccessToken,
                caseId,
                CaseTestData.BuildTransferRequest(caseWorkerId));

            caseWorkerSession = await AuthTestHelpers.LoginAndVerifyAsync(
                _client,
                _factory.EmailSender,
                RbacTestData.CaseWorkerEmail,
                AuthTestData.Password);
        }

        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddDays(7)));

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(-1));

        return (caseId, caseWorkerId, caseWorkerSession, sitting);
    }

    private async Task AssertNoEscalationAsync(Guid sittingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.CourtSittings.SingleAsync(s => s.Id == sittingId);
        Assert.Null(row.MissEscalatedAtUtc);

        var notificationCount = await db.InAppNotifications
            .CountAsync(n => n.ResourceId == sittingId
                && n.EventType == NotificationEventTypes.CourtMissEscalated);
        Assert.Equal(0, notificationCount);

        var auditCount = await db.AuditEvents
            .CountAsync(e => e.EventType == AuditEventTypes.CourtSittingMissEscalated
                && e.MetadataJson!.Contains(sittingId.ToString("D"), StringComparison.Ordinal));
        Assert.Equal(0, auditCount);
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
