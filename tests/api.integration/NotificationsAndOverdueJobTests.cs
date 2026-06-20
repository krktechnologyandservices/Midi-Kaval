using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class NotificationsAndOverdueJobTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public NotificationsAndOverdueJobTests(AuthWebApplicationFactory factory)
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
    public async Task OverdueJob_CreatesNotification_AndSuppressesDuplicates()
    {
        var (caseId, caseWorkerId, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));

        await RunOverdueJobAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Interventions.SingleAsync(i => i.Id == intervention.Id);
            Assert.NotNull(row.OverdueNotifiedAtUtc);

            var notifications = await db.InAppNotifications
                .Where(n => n.ResourceId == intervention.Id
                    && n.EventType == NotificationEventTypes.InterventionOverdue)
                .ToListAsync();
            Assert.Single(notifications);
            Assert.Equal(caseWorkerId, notifications[0].UserId);
            Assert.Equal(caseId, notifications[0].CaseId);
        }

        await RunOverdueJobAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.InAppNotifications
                .CountAsync(n => n.ResourceId == intervention.Id
                    && n.EventType == NotificationEventTypes.InterventionOverdue);
            Assert.Equal(1, count);
        }

        var list = await CaseTestData.ListNotificationsAsync(_client, caseWorkerSession.AccessToken);
        var item = Assert.Single(list.Items);
        Assert.Equal(NotificationEventTypes.InterventionOverdue, item.EventType);
        Assert.Equal(caseId, item.CaseId);
        Assert.Equal(intervention.Id, item.ResourceId);
        Assert.False(item.IsRead);
    }

    [Fact]
    public async Task Notifications_MarkRead_SetsReadAtUtc()
    {
        var (_, _, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));
        await RunOverdueJobAsync();

        var list = await CaseTestData.ListNotificationsAsync(_client, caseWorkerSession.AccessToken);
        var notificationId = Assert.Single(list.Items).Id;

        var marked = await CaseTestData.MarkNotificationReadAsync(
            _client,
            caseWorkerSession.AccessToken,
            notificationId);

        Assert.True(marked.IsRead);
        Assert.NotNull(marked.ReadAtUtc);
    }

    [Fact]
    public async Task Intervention_CompletedWithOutcome_MarksOverdueNotificationsRead()
    {
        var (caseId, _, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));
        await RunOverdueJobAsync();

        var response = await CaseTestData.SendUpdateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            intervention.Id,
            new UpdateInterventionRequest
            {
                Status = "Completed",
                Outcome = "Support session completed.",
                ProvidedAtUtc = DateTime.UtcNow,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await CaseTestData.ListNotificationsAsync(_client, caseWorkerSession.AccessToken);
        var item = Assert.Single(list.Items);
        Assert.True(item.IsRead);
        Assert.NotNull(item.ReadAtUtc);
    }

    [Fact]
    public async Task UnreadCount_ReturnsCorrectCount_AfterOverdueJob()
    {
        var (caseId, _, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        // No notifications yet
        var initial = await CaseTestData.GetUnreadCountAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(0, initial);

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));
        await RunOverdueJobAsync();

        // One overdue notification
        var afterJob = await CaseTestData.GetUnreadCountAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(1, afterJob);
    }

    [Fact]
    public async Task UnreadCount_Decreases_AfterMarkRead()
    {
        var (caseId, _, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));
        await RunOverdueJobAsync();

        var beforeMark = await CaseTestData.GetUnreadCountAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(1, beforeMark);

        var list = await CaseTestData.ListNotificationsAsync(_client, caseWorkerSession.AccessToken);
        var notificationId = list.Items[0].Id;

        await CaseTestData.MarkNotificationReadAsync(_client, caseWorkerSession.AccessToken, notificationId);

        var afterMark = await CaseTestData.GetUnreadCountAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(0, afterMark);
    }

    [Fact]
    public async Task UnreadCount_IsUserScoped()
    {
        var (caseId, _, caseWorkerSession, intervention) =
            await CreateAssignedOpenNeededInterventionAsync();

        await CaseTestData.SetInterventionDueAtUtcAsync(
            _factory,
            intervention.Id,
            DateTime.UtcNow.AddDays(-1));
        await RunOverdueJobAsync();

        // case worker gets the notification
        var workerCount = await CaseTestData.GetUnreadCountAsync(_client, caseWorkerSession.AccessToken);
        Assert.Equal(1, workerCount);

        // coordinator sees 0
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var coordinatorCount = await CaseTestData.GetUnreadCountAsync(_client, coordinator.AccessToken);
        Assert.Equal(0, coordinatorCount);
    }

    private async Task RunOverdueJobAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<InterventionOverdueJobRunner>();
        await runner.RunAsync();
    }

    private async Task<(
        Guid CaseId,
        Guid CaseWorkerId,
        AuthSession CaseWorkerSession,
        InterventionDto Intervention)> CreateAssignedOpenNeededInterventionAsync()
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

        var intervention = await CaseTestData.CreateInterventionAsync(
            _client,
            caseWorkerSession.AccessToken,
            caseId,
            CaseTestData.BuildInterventionRequest(
                direction: "Needed",
                assignedStaffUserId: caseWorkerId,
                dueAtUtc: DateTime.UtcNow.AddDays(2)));

        return (caseId, caseWorkerId, caseWorkerSession, intervention);
    }
}
