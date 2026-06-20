using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class VisitSchedulerTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public VisitSchedulerTests(AuthWebApplicationFactory factory)
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
    public async Task FieldWorker_ListToday_ReturnsScheduledVisitWithCaseSummary()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(2);
        var visit = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            scheduledAt);

        var envelope = await VisitTestData.ListVisitsTodayAsync(_client, worker.AccessToken);

        var item = Assert.Single(envelope.Data.Items, i => i.Case.Id == caseId);
        Assert.Equal(caseId, item.Case.Id);
        Assert.Equal("Scheduled", item.Status);
        Assert.False(item.IsOverdue);
    }

    [Fact]
    public async Task OverdueVisit_AppearsInTodayAndOverdueLists()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var pastToday = DateTime.UtcNow.AddMinutes(-30);
        if (pastToday.Date != DateTime.UtcNow.Date)
        {
            pastToday = DateTime.UtcNow.Date.AddHours(12);
        }

        await VisitTestData.SeedVisitAsync(_factory, caseId, workerId, pastToday);

        var todayResponse = await VisitTestData.SendListVisitsTodayAsync(_client, worker.AccessToken);
        todayResponse.EnsureSuccessStatusCode();
        var today = await todayResponse.Content.ReadFromJsonAsync<VisitTestData.VisitListEnvelope>();

        var overdueRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/visits/overdue");
        overdueRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", worker.AccessToken);
        var overdueResponse = await _client.SendAsync(overdueRequest);
        overdueResponse.EnsureSuccessStatusCode();
        var overdue = await overdueResponse.Content.ReadFromJsonAsync<VisitTestData.VisitListEnvelope>();

        Assert.Single(today!.Data.Items.Where(i => i.Case.Id == caseId));
        var todayItem = today.Data.Items.Single(i => i.Case.Id == caseId);
        Assert.True(todayItem.IsOverdue);
        Assert.Single(overdue!.Data.Items.Where(i => i.Case.Id == caseId));
        Assert.True(overdue.Data.Items.Single(i => i.Case.Id == caseId).IsOverdue);
    }

    [Fact]
    public async Task CompleteVisit_IncrementsVisitCount_ClearsNextVisitDue_AndAudits()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseRow = await db.Cases.SingleAsync(c => c.Id == caseId);
        Assert.Equal(1, caseRow.VisitCount);
        Assert.Null(caseRow.NextVisitDueAtUtc);

        var visitIdString = scheduled.Id.ToString("D");
        var completedAudits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.VisitCompleted && e.ActorUserId == worker.UserId)
            .ToListAsync();
        var audit = Assert.Single(
            completedAudits,
            e => e.MetadataJson?.Contains(visitIdString, StringComparison.Ordinal) == true);
        Assert.Contains(visitIdString, audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RescheduleVisit_PersistsReason_VisibleToCoordinator()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(3));

        var newTime = DateTime.UtcNow.AddDays(1);
        await VisitTestData.RescheduleVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            newTime,
            "Beneficiary unavailable today");

        var visits = await VisitTestData.ListCaseVisitsAsync(_client, coordinator.AccessToken, caseId);
        Assert.Equal(1, visits.Meta.TotalCount);
        Assert.Equal("Beneficiary unavailable today", visits.Data.Items[0].LastRescheduleReason);
    }

    [Fact]
    public async Task RescheduleCompletedVisit_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id);

        var response = await VisitTestData.SendRescheduleVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            DateTime.UtcNow.AddDays(1),
            "Too late");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleVisit_PastDate_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var response = await VisitTestData.SendScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(-1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleVisit_DuplicateActiveVisit_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var response = await VisitTestData.SendScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(4));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_ListToday_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await VisitTestData.SendListVisitsTodayAsync(_client, coordinator.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task Director_ListToday_Returns403()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var response = await VisitTestData.SendListVisitsTodayAsync(_client, director.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_ListCaseVisits_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);

        var response = await VisitTestData.SendListCaseVisitsAsync(_client, worker.AccessToken, caseId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompleteVisit_WrongAssignee_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var socialWorker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var caseWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.CaseWorkerEmail);
        var socialWorkerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(socialWorkerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var caseWorker = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await VisitTestData.SendCompleteVisitAsync(
            _client,
            caseWorker.AccessToken,
            scheduled.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(caseWorkerId, socialWorkerId);
    }

    [Fact]
    public async Task ListToday_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/visits/today");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FieldWorker_ListWeekly_ReturnsScheduledAndCompletedVisitsThisWeek()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);

        var scheduledCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            scheduledCaseId,
            CaseTestData.BuildTransferRequest(workerId));
        await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            scheduledCaseId,
            DateTime.UtcNow.AddHours(2));

        var completedCaseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            completedCaseId,
            CaseTestData.BuildTransferRequest(workerId));
        var completedAt = DateTime.UtcNow.AddHours(-2);
        if (completedAt < GetUtcWeekStart(DateTime.UtcNow))
        {
            completedAt = GetUtcWeekStart(DateTime.UtcNow).AddHours(12);
        }

        await VisitTestData.SeedVisitAsync(
            _factory,
            completedCaseId,
            workerId,
            completedAt,
            VisitStatus.Completed);

        var envelope = await VisitTestData.ListVisitsWeeklyAsync(_client, worker.AccessToken);

        Assert.Contains(envelope.Data.Items, i => i.Case.Id == scheduledCaseId && i.Status == "Scheduled");
        Assert.Contains(envelope.Data.Items, i => i.Case.Id == completedCaseId && i.Status == "Completed");
    }

    [Fact]
    public async Task ScheduleVisit_Returns201_AndWritesVisitScheduledAudit()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var response = await VisitTestData.SendScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseIdString = caseId.ToString("D");
        var scheduledAudits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.VisitScheduled && e.ActorUserId == coordinator.UserId)
            .ToListAsync();
        var audit = Assert.Single(
            scheduledAudits,
            e => e.MetadataJson?.Contains(caseIdString, StringComparison.Ordinal) == true);
        Assert.Contains(caseIdString, audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RescheduleVisit_WritesVisitRescheduledAudit()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(3));

        await VisitTestData.RescheduleVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            DateTime.UtcNow.AddDays(1),
            "Beneficiary unavailable today");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitIdString = scheduled.Id.ToString("D");
        var caseIdString = caseId.ToString("D");
        var rescheduledAudits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.VisitRescheduled && e.ActorUserId == worker.UserId)
            .ToListAsync();
        var audit = Assert.Single(
            rescheduledAudits,
            e => e.MetadataJson?.Contains(visitIdString, StringComparison.Ordinal) == true);
        Assert.Contains(visitIdString, audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains(caseIdString, audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScheduleVisit_TerminationExclusionCase_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));
        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);

        var response = await VisitTestData.SendScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleVisit_InvalidAssignee_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var response = await VisitTestData.SendScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2),
            coordinator.UserId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task TerminationExclusionCase_ExcludedFromFieldWorkerVisitLists()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(2);
        await VisitTestData.SeedVisitAsync(_factory, caseId, workerId, scheduledAt);
        await TransitionCaseToTerminationExclusionAsync(coordinator.AccessToken, caseId);

        var envelope = await VisitTestData.ListVisitsTodayAsync(_client, worker.AccessToken);

        Assert.DoesNotContain(envelope.Data.Items, i => i.Case.Id == caseId);
    }

    [Fact]
    public async Task CompleteAlreadyCompletedVisit_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id);

        var response = await VisitTestData.SendCompleteVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task StartVisit_ScheduledToInProgress_SetsStartedAtUtc_AndAudits()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var started = await VisitTestData.StartVisitAsync(_client, worker.AccessToken, scheduled.Id);

        Assert.Equal("InProgress", started.Status);
        Assert.NotNull(started.StartedAtUtc);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitRow = await db.Visits.SingleAsync(v => v.Id == scheduled.Id);
        Assert.Equal(VisitStatus.InProgress, visitRow.Status);
        Assert.NotNull(visitRow.StartedAtUtc);

        var visitIdString = scheduled.Id.ToString("D");
        var startedAudits = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.VisitStarted && e.ActorUserId == worker.UserId)
            .ToListAsync();
        Assert.Contains(startedAudits, e => e.MetadataJson?.Contains(visitIdString, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task StartVisit_AlreadyInProgress_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.StartVisitAsync(_client, worker.AccessToken, scheduled.Id);

        var response = await VisitTestData.SendStartVisitAsync(_client, worker.AccessToken, scheduled.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task StartVisit_AlreadyCompleted_Returns422()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id);

        var response = await VisitTestData.SendStartVisitAsync(_client, worker.AccessToken, scheduled.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CompleteVisit_WithNote_PersistsVisitNote_ReturnsCompletionNote()
    {
        const string note = "Met family at home. Discussed school attendance.";
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var completed = await VisitTestData.CompleteVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            note);

        Assert.Equal(note, completed.CompletionNote);
        Assert.Equal("Completed", completed.Status);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitNote = await db.VisitNotes.SingleAsync(n => n.VisitId == scheduled.Id);
        Assert.Equal(note, visitNote.BodyText);
        Assert.Equal(worker.UserId, visitNote.AuthorUserId);
    }

    [Fact]
    public async Task CompleteVisit_WithoutNote_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var response = await VisitTestData.SendCompleteVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            note: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteVisit_NoteTooLong_Returns400()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var response = await VisitTestData.SendCompleteVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            note: new string('x', 4001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Coordinator_ListCaseVisits_SeesCompletionNote_AndStartedAtUtc()
    {
        const string note = "Supervisor-visible completion note.";
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.StartVisitAsync(_client, worker.AccessToken, scheduled.Id);
        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id, note);

        var visits = await VisitTestData.ListCaseVisitsAsync(_client, coordinator.AccessToken, caseId);
        var item = Assert.Single(visits.Data.Items);
        Assert.Equal(note, item.CompletionNote);
        Assert.NotNull(item.StartedAtUtc);
        Assert.NotNull(item.CompletedAtUtc);
    }

    [Fact]
    public async Task WeeklyList_CompletedVisit_IncludesCompletionNote()
    {
        const string note = "Weekly list completion note.";
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduledAt = DateTime.UtcNow.AddHours(1);
        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            scheduledAt);
        await VisitTestData.CompleteVisitAsync(_client, worker.AccessToken, scheduled.Id, note);

        var weekly = await VisitTestData.ListVisitsWeeklyAsync(_client, worker.AccessToken);
        var item = Assert.Single(weekly.Data.Items, i => i.Id == scheduled.Id);
        Assert.Equal(note, item.CompletionNote);
        Assert.NotNull(item.CompletedAtUtc);
    }

    [Fact]
    public async Task Director_StartVisit_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var response = await VisitTestData.SendStartVisitAsync(_client, director.AccessToken, scheduled.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Director_CompleteVisit_Returns403()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));

        var response = await VisitTestData.SendCompleteVisitAsync(
            _client,
            director.AccessToken,
            scheduled.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleInProgressVisit_ClearsStartedAtUtc()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var workerId = await CaseTestData.GetUserIdByEmailAsync(_factory, RbacTestData.SocialWorkerEmail);
        var caseId = await CaseTestData.CreateCaseAsync(_client, coordinator.AccessToken);
        await CaseTestData.TransferCaseAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            CaseTestData.BuildTransferRequest(workerId));

        var scheduled = await VisitTestData.ScheduleVisitAsync(
            _client,
            coordinator.AccessToken,
            caseId,
            DateTime.UtcNow.AddHours(2));
        await VisitTestData.StartVisitAsync(_client, worker.AccessToken, scheduled.Id);

        await VisitTestData.RescheduleVisitAsync(
            _client,
            worker.AccessToken,
            scheduled.Id,
            DateTime.UtcNow.AddDays(1),
            "Beneficiary unavailable");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitRow = await db.Visits.SingleAsync(v => v.Id == scheduled.Id);
        Assert.Equal(VisitStatus.Scheduled, visitRow.Status);
        Assert.Null(visitRow.StartedAtUtc);
    }

    [Fact]
    public async Task DeactivatedFieldWorker_ListToday_Returns403()
    {
        var worker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == worker.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await VisitTestData.SendListVisitsTodayAsync(_client, worker.AccessToken);
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
                Headers =
                {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken),
                },
            };
            var response = await _client.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }
    }

    private static DateTime GetUtcWeekStart(DateTime utcNow)
    {
        var day = (int)utcNow.DayOfWeek;
        var mondayOffset = day == 0 ? -6 : 1 - day;
        return utcNow.Date.AddDays(mondayOffset);
    }
}
