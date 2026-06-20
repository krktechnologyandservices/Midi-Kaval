using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Sync;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.IntegrationTests;

internal static class VisitTestData
{
    public static async Task<VisitListItemDto> ScheduleVisitAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        DateTime scheduledAtUtc,
        Guid? assigneeUserId = null)
    {
        var response = await SendScheduleVisitAsync(
            client,
            accessToken,
            caseId,
            scheduledAtUtc,
            assigneeUserId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<VisitItemEnvelope>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendScheduleVisitAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        DateTime scheduledAtUtc,
        Guid? assigneeUserId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/visits")
        {
            Content = JsonContent.Create(new ScheduleVisitRequest
            {
                ScheduledAtUtc = scheduledAtUtc,
                AssigneeUserId = assigneeUserId,
            }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(request);
    }

    public static async Task<VisitListItemDto> CompleteVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId,
        string note = "Visit completed during integration test.")
    {
        var response = await SendCompleteVisitAsync(client, accessToken, visitId, note);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<VisitItemEnvelope>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCompleteVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId,
        string? note = "Visit completed during integration test.")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/visits/{visitId:D}/complete")
        {
            Content = JsonContent.Create(new CompleteVisitRequest { Note = note }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(request);
    }

    public static async Task<VisitListItemDto> StartVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId)
    {
        var response = await SendStartVisitAsync(client, accessToken, visitId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<VisitItemEnvelope>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendStartVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/visits/{visitId:D}/start")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(request);
    }

    public static async Task<VisitListItemDto> RescheduleVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId,
        DateTime scheduledAtUtc,
        string reason)
    {
        var response = await SendRescheduleVisitAsync(client, accessToken, visitId, scheduledAtUtc, reason);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<VisitItemEnvelope>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendRescheduleVisitAsync(
        HttpClient client,
        string accessToken,
        Guid visitId,
        DateTime scheduledAtUtc,
        string reason)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/visits/{visitId:D}/reschedule")
        {
            Content = JsonContent.Create(new RescheduleVisitRequest
            {
                ScheduledAtUtc = scheduledAtUtc,
                Reason = reason,
            }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(request);
    }

    public static async Task<VisitListEnvelope> ListVisitsTodayAsync(HttpClient client, string accessToken)
    {
        var response = await SendListVisitsTodayAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VisitListEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendListVisitsTodayAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/visits/today");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<VisitGroupingEnvelope> GetTodayGroupingSuggestionAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendTodayGroupingSuggestionAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VisitGroupingEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendTodayGroupingSuggestionAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/visits/today/grouping-suggestion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<VisitListEnvelope> ListVisitsWeeklyAsync(HttpClient client, string accessToken)
    {
        var response = await SendListVisitsWeeklyAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VisitListEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendListVisitsWeeklyAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/visits/weekly");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<VisitListEnvelope> ListCaseVisitsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var response = await SendListCaseVisitsAsync(client, accessToken, caseId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VisitListEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendListCaseVisitsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}/visits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<Guid> SeedVisitAsync(
        AuthWebApplicationFactory factory,
        Guid caseId,
        Guid assigneeUserId,
        DateTime scheduledAtUtc,
        VisitStatus status = VisitStatus.Scheduled,
        DateTime? startedAtUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseEntity = await db.Cases.SingleAsync(c => c.Id == caseId);
        var now = DateTime.UtcNow;
        var visitId = Guid.NewGuid();

        db.Visits.Add(new Visit
        {
            Id = visitId,
            OrganisationId = caseEntity.OrganisationId,
            CaseId = caseId,
            AssigneeUserId = assigneeUserId,
            ScheduledAtUtc = scheduledAtUtc,
            Status = status,
            StartedAtUtc = status == VisitStatus.InProgress ? startedAtUtc ?? now : null,
            CompletedAtUtc = status == VisitStatus.Completed ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        caseEntity.NextVisitDueAtUtc = scheduledAtUtc;
        caseEntity.UpdatedAtUtc = now;
        await db.SaveChangesAsync();
        return visitId;
    }

    public static async Task<AuthSession> BuildSocialWorkerSessionAsync(
        HttpClient client,
        FakeEmailSender emailSender) =>
        await AuthTestHelpers.LoginAndVerifyAsync(
            client,
            emailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

    public static async Task<AuthSession> BuildDirectorSessionAsync(
        HttpClient client,
        FakeEmailSender emailSender) =>
        await AuthTestHelpers.LoginAndVerifyAsync(
            client,
            emailSender,
            AuthTestData.Email,
            AuthTestData.Password);

    public static async Task<AuthSession> BuildCaseWorkerSessionAsync(
        HttpClient client,
        FakeEmailSender emailSender) =>
        await AuthTestHelpers.LoginAndVerifyAsync(
            client,
            emailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

    public static async Task<SyncPushEnvelope> PushSyncAsync(
        HttpClient client,
        string accessToken,
        SyncPushRequest request)
    {
        var response = await SendPushSyncAsync(client, accessToken, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncPushEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendPushSyncAsync(
        HttpClient client,
        string accessToken,
        SyncPushRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sync/push")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(message);
    }

    public sealed record VisitListMeta(string RequestId, int TotalCount);

    public sealed record VisitListEnvelope(VisitListResultDto Data, VisitListMeta Meta);

    public sealed record VisitGroupingMeta(string RequestId);

    public sealed record VisitGroupingEnvelope(VisitGroupingSuggestionDto Data, VisitGroupingMeta Meta);

    public sealed record VisitItemMeta(string RequestId);

    public sealed record VisitItemEnvelope(VisitListItemDto Data, VisitItemMeta Meta);

    public sealed record SyncPushMeta(string RequestId);

    public sealed record SyncPushEnvelope(SyncPushResultDto Data, SyncPushMeta Meta);
}
