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
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Attachments;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Auth;
using MidiKaval.Api.Models.Notifications;
using MidiKaval.Api.Models.Supervisor;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseCreateTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseCreateTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_Create_Returns201_PersistsRow_AndAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDto>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal(request.CrimeNumber!.Trim().ToUpperInvariant(), envelope.Data.CrimeNumber);
        Assert.Equal(request.StNumber!.Trim().ToUpperInvariant(), envelope.Data.StNumber);
        Assert.Equal("ProcessInitiation", envelope.Data.CurrentStage);
        Assert.Equal(0, envelope.Data.VisitCount);
        Assert.True(envelope.Data.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-5));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.Cases.SingleAsync(c => c.Id == envelope.Data.Id);
        Assert.Equal(AuthTestData.OrganisationId, row.OrganisationId);
        Assert.Equal(session.UserId, row.CreatedByUserId);
        Assert.Equal(CaseStage.ProcessInitiation, row.CurrentStage);

        var audit = await db.AuditEvents.SingleAsync(
            e => e.EventType == AuditEventTypes.CaseCreated && e.ActorUserId == session.UserId);
        Assert.Equal(AuthTestData.OrganisationId, audit.OrganisationId);
        Assert.Null(audit.SubjectUserId);
        Assert.Contains(envelope.Data.Id.ToString("D"), audit.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Director_Create_Returns201()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        var request = CaseTestData.BuildValidRequest();

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MissingBeneficiaryName_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.BeneficiaryName = "   ";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseCountBefore = await db.Cases.CountAsync();

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("beneficiaryName", problem?.Detail, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(caseCountBefore, await db.Cases.CountAsync());
    }

    [Fact]
    public async Task InvalidOffenceClassification_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.OffenceClassification = "Unknown";

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("offenceClassification", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateCrimeNumber_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caseCountBefore = await db.Cases.CountAsync();
        var auditCountBefore = await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseCreated);

        using (var first = CreateAuthorizedPost(session.AccessToken, request))
        {
            var firstResponse = await _client.SendAsync(first);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        }

        var duplicate = CaseTestData.BuildValidRequest();
        duplicate.CrimeNumber = request.CrimeNumber;
        duplicate.StNumber = $"ST-{Guid.NewGuid():N}"[..12];

        using var second = CreateAuthorizedPost(session.AccessToken, duplicate);
        var secondResponse = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Conflict", problem?.Title);
        Assert.Equal(caseCountBefore + 1, await db.Cases.CountAsync());
        Assert.Equal(auditCountBefore + 1, await db.AuditEvents.CountAsync(e => e.EventType == AuditEventTypes.CaseCreated));
    }

    [Fact]
    public async Task DuplicateCrimeNumber_CaseInsensitive_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.CrimeNumber = "CR-CASETEST";

        using (var first = CreateAuthorizedPost(session.AccessToken, request))
        {
            var firstResponse = await _client.SendAsync(first);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        }

        var duplicate = CaseTestData.BuildValidRequest();
        duplicate.CrimeNumber = "cr-casetest";
        duplicate.StNumber = $"ST-{Guid.NewGuid():N}"[..12];

        using var second = CreateAuthorizedPost(session.AccessToken, duplicate);
        var secondResponse = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task DuplicateStNumber_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();

        using (var first = CreateAuthorizedPost(session.AccessToken, request))
        {
            var firstResponse = await _client.SendAsync(first);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        }

        var duplicate = CaseTestData.BuildValidRequest();
        duplicate.StNumber = request.StNumber;
        duplicate.CrimeNumber = $"CR-{Guid.NewGuid():N}"[..12];

        using var second = CreateAuthorizedPost(session.AccessToken, duplicate);
        var secondResponse = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SocialWorker_Create_Returns403_ForbiddenByRoleMessage()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);
        var request = CaseTestData.BuildValidRequest();

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task CaseWorker_Create_Returns403_ForbiddenByRoleMessage()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);
        var request = CaseTestData.BuildValidRequest();

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task NullBody_Create_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases")
        {
            Content = JsonContent.Create<CreateCaseRequest?>(null),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NumericOffenceClassification_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.OffenceClassification = "3";

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, request);
        var response = await _client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("offenceClassification", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Create_Returns403_DeactivatedMessage()
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

        using var httpRequest = CreateAuthorizedPost(session.AccessToken, CaseTestData.BuildValidRequest());
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
    public async Task Unauthenticated_Create_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/cases",
            CaseTestData.BuildValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthorizedPost(string accessToken, CreateCaseRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return httpRequest;
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}

internal static class CaseTestData
{
    public static CreateCaseRequest BuildValidRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return new CreateCaseRequest
        {
            CrimeNumber = $"CR-{suffix}",
            StNumber = $"ST-{suffix}",
            BeneficiaryName = "Ravi Kumar",
            BeneficiaryAge = 17,
            BeneficiaryContact = "+919876543210",
            TypeOfOffence = "Theft",
            OffenceClassification = "Petty",
            Domicile = "Urban",
            IsFirstTimeOffender = true,
        };
    }

    public static Task<AuthSession> BuildCoordinatorSessionAsync(
        HttpClient client,
        FakeEmailSender emailSender) =>
        AuthTestHelpers.LoginAndVerifyAsync(
            client,
            emailSender,
            RbacTestData.CoordinatorEmail,
            AuthTestData.Password);

    public static async Task<Guid> CreateCaseAsync(HttpClient client, string accessToken) =>
        await CreateCaseAsync(client, accessToken, BuildValidRequest());

    public static async Task<Guid> CreateCaseAsync(
        HttpClient client,
        string accessToken,
        CreateCaseRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDto>>();
        return envelope!.Data!.Id;
    }

    public static async Task<CaseDto> MergeCaseAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateCaseRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/merge")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDto>>();
        return envelope!.Data!;
    }

    public static HttpRequestMessage CreateAuthorizedMergePost(
        string accessToken,
        Guid caseId,
        CreateCaseRequest request) =>
        new(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/merge")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };

    public static async Task<SearchApiEnvelope> SearchCasesAsync(
        HttpClient client,
        string accessToken,
        IReadOnlyDictionary<string, string?>? query)
    {
        var response = await SendSearchAsync(client, accessToken, query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchApiEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendSearchAsync(
        HttpClient client,
        string accessToken,
        IReadOnlyDictionary<string, string?>? query)
    {
        var url = BuildSearchUrl(query);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task SetNextVisitDueAsync(
        AuthWebApplicationFactory factory,
        Guid caseId,
        DateTime utc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Cases.SingleAsync(c => c.Id == caseId);
        entity.NextVisitDueAtUtc = utc;
        await db.SaveChangesAsync();
    }

    public static async Task TransitionStageAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CaseStage targetStage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cases/{caseId:D}/stage")
        {
            Content = JsonContent.Create(new TransitionCaseStageRequest { TargetStage = targetStage.ToString() }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<CaseSearchPresetDto> CreateSearchPresetAsync(
        HttpClient client,
        string accessToken,
        CreateCaseSearchPresetRequest request)
    {
        var response = await SendCreatePresetAsync(client, accessToken, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseSearchPresetDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCreatePresetAsync(
        HttpClient client,
        string accessToken,
        CreateCaseSearchPresetRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases/search-presets")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<IReadOnlyList<CaseSearchPresetDto>> ListSearchPresetsAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListPresetsAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseSearchPresetDto[]>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListPresetsAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases/search-presets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task DeleteSearchPresetAsync(
        HttpClient client,
        string accessToken,
        Guid presetId)
    {
        var response = await SendDeletePresetAsync(client, accessToken, presetId);
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> SendDeletePresetAsync(
        HttpClient client,
        string accessToken,
        Guid presetId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/cases/search-presets/{presetId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static string BuildSearchUrl(IReadOnlyDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return "/api/v1/cases/search";
        }

        var parts = query
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");
        return $"/api/v1/cases/search?{string.Join("&", parts)}";
    }

    public static async Task<HttpResponseMessage> ExportCasesAsync(
        HttpClient client,
        string accessToken,
        string format,
        IReadOnlyDictionary<string, string?>? query)
    {
        var response = await SendExportAsync(client, accessToken, format, query);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public static Task<HttpResponseMessage> SendExportAsync(
        HttpClient client,
        string accessToken,
        string format,
        IReadOnlyDictionary<string, string?>? query)
    {
        var url = BuildExportUrl(format, query);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static string BuildExportUrl(string format, IReadOnlyDictionary<string, string?>? query)
    {
        var parts = new List<string> { $"format={Uri.EscapeDataString(format)}" };
        if (query is not null)
        {
            parts.AddRange(query
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));
        }

        return $"/api/v1/cases/search/export?{string.Join("&", parts)}";
    }

    public static TransferCaseRequest BuildTransferRequest(Guid assigneeUserId) =>
        new()
        {
            AssigneeUserId = assigneeUserId,
            PriorActions = "Visited beneficiary home",
            OpenItems = "Confirm school enrollment",
            NextVisitPurpose = "Follow up on housing",
        };

    public static async Task<CaseDetailDto> TransferCaseAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        TransferCaseRequest request)
    {
        var response = await SendTransferAsync(client, accessToken, caseId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDetailDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendTransferAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        TransferCaseRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/transfer")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<CaseDetailDto> GetCaseDetailAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var response = await SendGetCaseDetailAsync(client, accessToken, caseId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseDetailDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendGetCaseDetailAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<SearchApiEnvelope> ListAssignedCasesAsync(
        HttpClient client,
        string accessToken,
        int page = 1,
        int pageSize = 25)
    {
        var response = await SendListAssignedAsync(client, accessToken, page, pageSize);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchApiEnvelope>())!;
    }

    public static Task<HttpResponseMessage> SendListAssignedAsync(
        HttpClient client,
        string accessToken,
        int page = 1,
        int pageSize = 25)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/cases/assigned?page={page}&pageSize={pageSize}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<CaseGpsDto> VerifyCaseGpsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        decimal latitude,
        decimal longitude,
        string landmark)
    {
        var response = await SendVerifyCaseGpsAsync(
            client,
            accessToken,
            caseId,
            latitude,
            longitude,
            landmark);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseGpsDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendVerifyCaseGpsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        decimal latitude,
        decimal longitude,
        string landmark)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/gps/verify")
        {
            Content = JsonContent.Create(new VerifyCaseGpsRequest
            {
                Latitude = latitude,
                Longitude = longitude,
                Landmark = landmark,
            }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(request);
    }

    public static async Task<IReadOnlyList<FieldWorkerUserDto>> ListFieldWorkersAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListFieldWorkersAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<FieldWorkerUserDto[]>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListFieldWorkersAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/field-workers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static CreateCaseNoteRequest BuildCaseNoteRequest(
        string noteType = "General",
        string bodyText = "Follow-up note for beneficiary.",
        bool actionRequired = false,
        DateTime? actionDueAtUtc = null) =>
        new()
        {
            NoteType = noteType,
            BodyText = bodyText,
            ActionRequired = actionRequired,
            ActionDueAtUtc = actionDueAtUtc,
        };

    public static async Task<CaseNoteDto> CreateCaseNoteAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateCaseNoteRequest request)
    {
        var response = await SendCreateCaseNoteAsync(client, accessToken, caseId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseNoteDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCreateCaseNoteAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateCaseNoteRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/notes")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<CaseNoteListResultDto> ListCaseNotesAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var response = await SendListCaseNotesAsync(client, accessToken, caseId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CaseNoteListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListCaseNotesAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}/notes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static CreateInterventionRequest BuildInterventionRequest(
        string direction = "Needed",
        string categoryName = "Counselling",
        string description = "Weekly counselling session for beneficiary.",
        string priority = "High",
        string? status = "Open",
        DateTime? dueAtUtc = null,
        DateTime? providedAtUtc = null,
        string? outcome = null,
        Guid? assignedStaffUserId = null) =>
        new()
        {
            Direction = direction,
            CategoryName = categoryName,
            Description = description,
            Priority = priority,
            Status = status,
            DueAtUtc = dueAtUtc ?? DateTime.UtcNow.AddDays(2),
            ProvidedAtUtc = providedAtUtc,
            Outcome = outcome,
            AssignedStaffUserId = assignedStaffUserId,
        };

    public static async Task<InterventionDto> CreateInterventionAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateInterventionRequest request)
    {
        var response = await SendCreateInterventionAsync(client, accessToken, caseId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InterventionDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCreateInterventionAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateInterventionRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/interventions")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<InterventionListResultDto> ListInterventionsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var response = await SendListInterventionsAsync(client, accessToken, caseId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InterventionListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListInterventionsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}/interventions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> SendUpdateInterventionAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        Guid interventionId,
        UpdateInterventionRequest request)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/cases/{caseId:D}/interventions/{interventionId:D}")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static CreateCourtSittingRequest BuildCourtSittingRequest(
        DateTime? scheduledAtUtc = null,
        string courtName = "District Court Chennai",
        string purpose = "Bail hearing",
        string? status = "Upcoming",
        string? notes = null,
        string? outcome = null) =>
        new()
        {
            ScheduledAtUtc = scheduledAtUtc ?? DateTime.UtcNow.AddDays(7),
            CourtName = courtName,
            Purpose = purpose,
            Status = status,
            Notes = notes,
            Outcome = outcome,
        };

    public static async Task<CourtSittingDto> CreateCourtSittingAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateCourtSittingRequest request)
    {
        var response = await SendCreateCourtSittingAsync(client, accessToken, caseId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CourtSittingDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCreateCourtSittingAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        CreateCourtSittingRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId:D}/court-sittings")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<CourtSittingListResultDto> ListCourtSittingsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var response = await SendListCourtSittingsAsync(client, accessToken, caseId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CourtSittingListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListCourtSittingsAsync(
        HttpClient client,
        string accessToken,
        Guid caseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{caseId:D}/court-sittings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> SendGetCourtSittingAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        Guid sittingId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/cases/{caseId:D}/court-sittings/{sittingId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> SendUpdateCourtSittingAsync(
        HttpClient client,
        string accessToken,
        Guid caseId,
        Guid sittingId,
        UpdateCourtSittingRequest request)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/cases/{caseId:D}/court-sittings/{sittingId:D}")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<ApiResponse<CourtSittingUpcomingListResultDto>> ListUpcomingCourtSittingsAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListUpcomingCourtSittingsAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<CourtSittingUpcomingListResultDto>>())!;
    }

    public static Task<HttpResponseMessage> SendListUpcomingCourtSittingsAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/court-sittings/upcoming");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static AttachmentPresignRequest BuildPresignRequest(
        Guid noteId,
        string fileName = "evidence.jpg",
        string contentType = "image/jpeg",
        long fileSizeBytes = 2048,
        string resourceType = "CaseNote") =>
        new()
        {
            ResourceType = resourceType,
            ResourceId = noteId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
        };

    public static async Task<AttachmentPresignResultDto> PresignAttachmentAsync(
        HttpClient client,
        string accessToken,
        AttachmentPresignRequest request)
    {
        var response = await SendPresignAttachmentAsync(client, accessToken, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AttachmentPresignResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendPresignAttachmentAsync(
        HttpClient client,
        string accessToken,
        AttachmentPresignRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/attachments/presign")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<AttachmentDto> ConfirmAttachmentAsync(
        HttpClient client,
        string accessToken,
        Guid attachmentId)
    {
        var response = await SendConfirmAttachmentAsync(client, accessToken, attachmentId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AttachmentDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendConfirmAttachmentAsync(
        HttpClient client,
        string accessToken,
        Guid attachmentId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/attachments/confirm")
        {
            Content = JsonContent.Create(new AttachmentConfirmRequest { AttachmentId = attachmentId }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task PutBlobAsync(
        string uploadUrl,
        IReadOnlyDictionary<string, string> requiredHeaders,
        byte[] content)
    {
        using var uploadClient = new HttpClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = new ByteArrayContent(content),
        };

        foreach (var header in requiredHeaders)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                httpRequest.Content!.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(header.Value);
            }
            else
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var response = await uploadClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<AttachmentDownloadUrlDto> GetAttachmentDownloadUrlAsync(
        HttpClient client,
        string accessToken,
        Guid attachmentId)
    {
        var response = await SendGetAttachmentDownloadUrlAsync(client, accessToken, attachmentId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AttachmentDownloadUrlDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendGetAttachmentDownloadUrlAsync(
        HttpClient client,
        string accessToken,
        Guid attachmentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/attachments/{attachmentId:D}/download-url");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<Guid> GetUserIdByEmailAsync(
        AuthWebApplicationFactory factory,
        string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleAsync(
            u => u.OrganisationId == AuthTestData.OrganisationId && u.Email == normalized);
        return user.Id;
    }

    public static async Task SetAssignedAtUtcAsync(
        AuthWebApplicationFactory factory,
        Guid caseId,
        DateTime assignedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Cases.SingleAsync(c => c.Id == caseId);
        entity.AssignedAtUtc = assignedAtUtc;
        await db.SaveChangesAsync();
    }

    public static async Task SetInterventionDueAtUtcAsync(
        AuthWebApplicationFactory factory,
        Guid interventionId,
        DateTime dueAtUtc,
        DateTime? overdueNotifiedAtUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var intervention = await db.Interventions.SingleAsync(i => i.Id == interventionId);
        intervention.DueAtUtc = dueAtUtc;
        intervention.OverdueNotifiedAtUtc = overdueNotifiedAtUtc;
        await db.SaveChangesAsync();
    }

    public static async Task SetCourtSittingScheduledAtUtcAsync(
        AuthWebApplicationFactory factory,
        Guid sittingId,
        DateTime scheduledAtUtc,
        DateTime? reminderSentAtUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sitting = await db.CourtSittings.SingleAsync(s => s.Id == sittingId);
        sitting.ScheduledAtUtc = scheduledAtUtc;
        sitting.ReminderSentAtUtc = reminderSentAtUtc;
        await db.SaveChangesAsync();
    }

    public static async Task RunCourtReminderJobAsync(AuthWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<CourtReminderJobRunner>();
        await runner.RunAsync();
    }

    public static async Task RunCourtMissEscalationJobAsync(AuthWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<CourtMissEscalationJobRunner>();
        await runner.RunAsync();
    }

    public static async Task<CrisisQueueListResultDto> ListCrisisQueueAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListCrisisQueueAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CrisisQueueListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListCrisisQueueAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/supervisor/crisis-queue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<NotificationListResultDto> ListNotificationsAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListNotificationsAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<NotificationListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListNotificationsAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static async Task<NotificationDto> MarkNotificationReadAsync(
        HttpClient client,
        string accessToken,
        Guid notificationId)
    {
        var response = await SendMarkNotificationReadAsync(client, accessToken, notificationId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<NotificationDto>>();
        return envelope!.Data!;
    }

    public static async Task<int> GetUnreadCountAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendGetUnreadCountAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UnreadCountDto>>();
        return envelope!.Data!.Count;
    }

    public static Task<HttpResponseMessage> SendGetUnreadCountAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/unread-count");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> SendMarkNotificationReadAsync(
        HttpClient client,
        string accessToken,
        Guid notificationId)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/notifications/{notificationId:D}/read")
        {
            Content = JsonContent.Create(new { }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<UserDeviceDto> RegisterUserDeviceAsync(
        HttpClient client,
        string accessToken,
        RegisterUserDeviceRequest request)
    {
        var response = await SendRegisterUserDeviceAsync(client, accessToken, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserDeviceDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendRegisterUserDeviceAsync(
        HttpClient client,
        string accessToken,
        RegisterUserDeviceRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/devices/me")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static Task<HttpResponseMessage> LogoutWithDeviceAsync(
        HttpClient client,
        string refreshToken,
        string? deviceInstallId = null)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest
            {
                RefreshToken = refreshToken,
                DeviceInstallId = deviceInstallId,
            });
    }

    public static async Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<NotificationPreferencesDto>>();
        return envelope!.Data!;
    }

    public static CreateTravelClaimRequest BuildCreateTravelClaimRequest(
        Guid caseId,
        string transportMode = "Bus",
        decimal amount = 45.50m,
        DateTime? claimDate = null) =>
        new()
        {
            ClaimDate = claimDate ?? new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            StartLocation = "Office",
            Destination = "District Court",
            TransportMode = transportMode,
            Amount = amount,
            Notes = "Client visit travel",
            CaseIds = [caseId],
        };

    public static async Task<TravelClaimDto> CreateTravelClaimAsync(
        HttpClient client,
        string accessToken,
        CreateTravelClaimRequest request)
    {
        var response = await SendCreateTravelClaimAsync(client, accessToken, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendCreateTravelClaimAsync(
        HttpClient client,
        string accessToken,
        CreateTravelClaimRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/travel-claims")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<TravelClaimDto> SubmitTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId)
    {
        var response = await SendSubmitTravelClaimAsync(client, accessToken, claimId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendSubmitTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/travel-claims/{claimId:D}/submit")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static Task<HttpResponseMessage> SendGetTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/travel-claims/{claimId:D}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static Task<HttpResponseMessage> SendPatchTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId,
        UpdateTravelClaimRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/travel-claims/{claimId:D}")
        {
            Content = JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static Task<HttpResponseMessage> SendListTravelClaimMonthlyTotalsAsync(
        HttpClient client,
        string accessToken,
        int year,
        int month)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/supervisor/travel-claims/monthly-totals?year={year}&month={month}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<TravelClaimMonthlyTotalsResultDto> ListTravelClaimMonthlyTotalsAsync(
        HttpClient client,
        string accessToken,
        int year,
        int month)
    {
        var response = await SendListTravelClaimMonthlyTotalsAsync(client, accessToken, year, month);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimMonthlyTotalsResultDto>>();
        return envelope!.Data!;
    }

    public static async Task<TravelClaimDto> CreateSubmittedTravelClaimWithReceiptAsync(
        HttpClient client,
        AuthWebApplicationFactory factory,
        string fieldWorkerEmail,
        string transportMode = "Bus")
    {
        var (claim, _) = await CreateAssignedCaseAndTravelClaimAsync(
            client,
            factory,
            fieldWorkerEmail,
            transportMode);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            client,
            factory.EmailSender,
            fieldWorkerEmail,
            AuthTestData.Password);

        var content = "receipt-bytes"u8.ToArray();
        var presign = await PresignAttachmentAsync(
            client,
            fieldSession.AccessToken,
            BuildPresignRequest(
                claim.Id,
                resourceType: "TravelClaim",
                fileSizeBytes: content.Length));

        await PutBlobAsync(presign.UploadUrl, presign.RequiredHeaders, content);
        await ConfirmAttachmentAsync(client, fieldSession.AccessToken, presign.AttachmentId);

        return await SubmitTravelClaimAsync(client, fieldSession.AccessToken, claim.Id);
    }

    public static async Task<TravelClaimListResultDto> ListDirectorPendingTravelClaimsAsync(
        HttpClient client,
        string accessToken)
    {
        var response = await SendListDirectorPendingTravelClaimsAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimListResultDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendListDirectorPendingTravelClaimsAsync(
        HttpClient client,
        string accessToken)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/director/travel-claims/pending")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<TravelClaimDto> GetSupervisorTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId)
    {
        var response = await SendGetSupervisorTravelClaimAsync(client, accessToken, claimId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendGetSupervisorTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/supervisor/travel-claims/{claimId:D}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<TravelClaimDto> ApproveTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId,
        ApproveTravelClaimRequest? request = null)
    {
        var response = await SendApproveTravelClaimAsync(client, accessToken, claimId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendApproveTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId,
        ApproveTravelClaimRequest? request = null)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/travel-claims/{claimId:D}/approve")
        {
            Content = JsonContent.Create(request ?? new ApproveTravelClaimRequest()),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<TravelClaimDto> ReturnTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId,
        ReturnTravelClaimRequest request)
    {
        var response = await SendReturnTravelClaimAsync(client, accessToken, claimId, request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TravelClaimDto>>();
        return envelope!.Data!;
    }

    public static Task<HttpResponseMessage> SendReturnTravelClaimAsync(
        HttpClient client,
        string accessToken,
        Guid claimId,
        ReturnTravelClaimRequest? request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/travel-claims/{claimId:D}/return")
        {
            Content = request is null ? null : JsonContent.Create(request),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        return client.SendAsync(httpRequest);
    }

    public static async Task<(TravelClaimDto Claim, Guid CaseId)> CreateAssignedCaseAndTravelClaimAsync(
        HttpClient client,
        AuthWebApplicationFactory factory,
        string fieldWorkerEmail,
        string transportMode = "Bus")
    {
        var coordinator = await BuildCoordinatorSessionAsync(client, factory.EmailSender);
        var fieldWorkerId = await GetUserIdByEmailAsync(factory, fieldWorkerEmail);
        var caseId = await CreateCaseAsync(client, coordinator.AccessToken);
        await TransferCaseAsync(
            client,
            coordinator.AccessToken,
            caseId,
            BuildTransferRequest(fieldWorkerId));

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            client,
            factory.EmailSender,
            fieldWorkerEmail,
            AuthTestData.Password);

        var claim = await CreateTravelClaimAsync(
            client,
            fieldSession.AccessToken,
            BuildCreateTravelClaimRequest(caseId, transportMode));

        return (claim, caseId);
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);

    public sealed record SearchApiMeta(string RequestId, int TotalCount);

    public sealed record SearchApiEnvelope(CaseSearchResultDto Data, SearchApiMeta Meta);
}
