using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CourtReminderJobTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CourtReminderJobTests(AuthWebApplicationFactory factory)
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
    public async Task ReminderJob_CreatesNotificationEmailsAudit_AndSuppressesDuplicates()
    {
        var (caseId, caseWorkerId, caseWorkerSession, sitting) =
            await CreateAssignedSittingInReminderWindowAsync();

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.ReminderSentAtUtc);

            var notifications = await db.InAppNotifications
                .Where(n => n.ResourceId == sitting.Id
                    && n.EventType == NotificationEventTypes.CourtReminder24h)
                .ToListAsync();
            Assert.Single(notifications);
            Assert.Equal(caseWorkerId, notifications[0].UserId);
            Assert.Equal(caseId, notifications[0].CaseId);

            var audit = await db.AuditEvents
                .SingleAsync(e => e.EventType == AuditEventTypes.CourtSittingReminderSent
                    && e.MetadataJson!.Contains(sitting.Id.ToString("D"), StringComparison.Ordinal));
            Assert.Null(audit.ActorUserId);
            Assert.Equal(caseWorkerId, audit.SubjectUserId);
        }

        var emails = _factory.EmailSender.Messages;
        Assert.Contains(emails, m => m.To == RbacTestData.CoordinatorEmail);
        Assert.Contains(emails, m => m.To == RbacTestData.CaseWorkerEmail);
        Assert.All(emails, m =>
        {
            Assert.Contains("Court sitting reminder", m.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("District Court", m.Body, StringComparison.Ordinal);
            Assert.Contains("Crime #", m.Body, StringComparison.Ordinal);
            Assert.Contains("ST #", m.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("beneficiary", m.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(EmailTemplateFooter.Line, m.Body, StringComparison.Ordinal);
        });

        var list = await CaseTestData.ListNotificationsAsync(_client, caseWorkerSession.AccessToken);
        var item = Assert.Single(list.Items);
        Assert.Equal(NotificationEventTypes.CourtReminder24h, item.EventType);
        Assert.Equal(caseId, item.CaseId);
        Assert.Equal(sitting.Id, item.ResourceId);

        var emailCount = _factory.EmailSender.Messages.Count;
        await CaseTestData.RunCourtReminderJobAsync(_factory);
        Assert.Equal(emailCount, _factory.EmailSender.Messages.Count);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.InAppNotifications
                .CountAsync(n => n.ResourceId == sitting.Id
                    && n.EventType == NotificationEventTypes.CourtReminder24h);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task ReminderJob_OutsideWindow_DoesNotSend()
    {
        var (_, _, _, sitting) = await CreateAssignedSittingAsync(DateTime.UtcNow.AddHours(48));

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ReminderJob_TerminalCase_DoesNotSend()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(caseWorkerId));

        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: DateTime.UtcNow.AddHours(24)));

        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);
        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ReminderJob_DeactivatedAssignee_DoesNotSend()
    {
        var (_, caseWorkerId, _, sitting) = await CreateAssignedSittingInReminderWindowAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == caseWorkerId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == caseWorkerId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ReminderJob_UnassignedCase_DoesNotSend()
    {
        var (_, _, _, sitting) = await CreateAssignedSittingInReminderWindowAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caseEntity = await db.Cases.SingleAsync(c => c.Id == sitting.CaseId);
            caseEntity.AssignedWorkerId = null;
            await db.SaveChangesAsync();
        }

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ReminderJob_PocsoCase_OmitsPurposeFreeTextInEmail()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var pocsoRequest = CaseTestData.BuildValidRequest();
        pocsoRequest.SensitivityLevel = "POCSO";
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken, pocsoRequest);
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

        const string sensitivePurpose = "Discuss beneficiary full name in court";
        var sitting = await CaseTestData.CreateCourtSittingAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildCourtSittingRequest(
                scheduledAtUtc: DateTime.UtcNow.AddHours(24),
                purpose: sensitivePurpose));

        _factory.EmailSender.Clear();
        await CaseTestData.RunCourtReminderJobAsync(_factory);

        Assert.NotEmpty(_factory.EmailSender.Messages);
        Assert.All(_factory.EmailSender.Messages, m =>
        {
            Assert.Contains(CourtSittingEmailBodyHelper.PocsoPurposeLine, m.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitivePurpose, m.Body, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ReminderJob_EmailFailure_DoesNotSetDedupFlag()
    {
        var (_, _, _, sitting) = await CreateAssignedSittingInReminderWindowAsync();

        _factory.EmailSender.FailNextSend = true;
        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
    }

    [Fact]
    public async Task ReminderJob_AttendedStatus_DoesNotSend()
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
            DateTime.UtcNow.AddHours(24));

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await AssertNoReminderSentAsync(sitting.Id);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ReminderJob_RescheduleClearsDedup_AndResendsInNewWindow()
    {
        var (_, _, _, sitting) = await CreateAssignedSittingInReminderWindowAsync();

        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.ReminderSentAtUtc);
        }

        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendUpdateCourtSittingAsync(
            _client,
            coordinator.AccessToken,
            sitting.CaseId,
            sitting.Id,
            new UpdateCourtSittingRequest
            {
                ScheduledAtUtc = DateTime.UtcNow.AddHours(48),
            });
        response.EnsureSuccessStatusCode();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.Null(row.ReminderSentAtUtc);
        }

        await CaseTestData.SetCourtSittingScheduledAtUtcAsync(
            _factory,
            sitting.Id,
            DateTime.UtcNow.AddHours(24));

        _factory.EmailSender.Clear();
        await CaseTestData.RunCourtReminderJobAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.CourtSittings.SingleAsync(s => s.Id == sitting.Id);
            Assert.NotNull(row.ReminderSentAtUtc);

            var count = await db.InAppNotifications
                .CountAsync(n => n.ResourceId == sitting.Id
                    && n.EventType == NotificationEventTypes.CourtReminder24h);
            Assert.Equal(2, count);
        }

        Assert.NotEmpty(_factory.EmailSender.Messages);
    }

    private async Task<(
        Guid CaseId,
        Guid CaseWorkerId,
        AuthSession CaseWorkerSession,
        CourtSittingDto Sitting)> CreateAssignedSittingInReminderWindowAsync() =>
        await CreateAssignedSittingAsync(DateTime.UtcNow.AddHours(24));

    private async Task<(
        Guid CaseId,
        Guid CaseWorkerId,
        AuthSession CaseWorkerSession,
        CourtSittingDto Sitting)> CreateAssignedSittingAsync(DateTime scheduledAtUtc)
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
            CaseTestData.BuildCourtSittingRequest(scheduledAtUtc: scheduledAtUtc));

        return (caseId, caseWorkerId, caseWorkerSession, sitting);
    }

    private async Task AssertNoReminderSentAsync(Guid sittingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.CourtSittings.SingleAsync(s => s.Id == sittingId);
        Assert.Null(row.ReminderSentAtUtc);

        var notificationCount = await db.InAppNotifications
            .CountAsync(n => n.ResourceId == sittingId
                && n.EventType == NotificationEventTypes.CourtReminder24h);
        Assert.Equal(0, notificationCount);

        var auditCount = await db.AuditEvents
            .CountAsync(e => e.EventType == AuditEventTypes.CourtSittingReminderSent
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
