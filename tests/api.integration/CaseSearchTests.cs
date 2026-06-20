using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class CaseSearchTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseSearchTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_SearchByCrimeSubstring_Returns200_WithMatch()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.CrimeNumber = "CR-SEARCH-ABC";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["q"] = "search-abc" });

        Assert.NotNull(envelope?.Data);
        Assert.Contains(envelope.Data!.Items, i => i.CrimeNumber == "CR-SEARCH-ABC");
        Assert.True(envelope.Meta.TotalCount >= 1);
    }

    [Fact]
    public async Task Coordinator_SearchByDomicileInQ_Returns200_WithMatch()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.Domicile = "Urban";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["q"] = "urb" });

        Assert.Contains(envelope.Data!.Items, i => i.Domicile == "Urban");
    }

    [Fact]
    public async Task Coordinator_FilterByCurrentStage_Returns200_Filtered()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        await CaseTestData.TransitionStageAsync(
            _client,
            session.AccessToken,
            caseId,
            CaseStage.MaintainAndDevelopment);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?>
            {
                ["currentStage"] = "MaintainAndDevelopment",
                ["q"] = request.CrimeNumber,
            });

        Assert.All(envelope.Data!.Items, i => Assert.Equal("MaintainAndDevelopment", i.CurrentStage));
    }

    [Fact]
    public async Task Coordinator_FilterByDomicileParam_Returns200()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.Domicile = "Rural";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["domicile"] = "Rural" });

        Assert.Contains(envelope.Data!.Items, i => i.Domicile == "Rural");
    }

    [Fact]
    public async Task Coordinator_FilterByCreatedByUserId_Returns200()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["createdByUserId"] = session.UserId.ToString("D") });

        Assert.All(envelope.Data!.Items, i => Assert.Equal(session.UserId, i.CreatedByUserId));
    }

    [Fact]
    public async Task Coordinator_FilterOverdue_Returns200_IncludesOverdueCase()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        await CaseTestData.SetNextVisitDueAsync(_factory, caseId, DateTime.UtcNow.AddDays(-1));

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["overdue"] = "true" });

        Assert.Contains(envelope.Data!.Items, i => i.Id == caseId);
    }

    [Fact]
    public async Task Coordinator_FilterOverdue_ExcludesNullDueDate()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var caseId = await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["overdue"] = "true" });

        Assert.DoesNotContain(envelope.Data!.Items, i => i.Id == caseId);
    }

    [Fact]
    public async Task Coordinator_Pagination_Returns200_WithStableTotalCount()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        for (var i = 0; i < 3; i++)
        {
            await CaseTestData.CreateCaseAsync(_client, session.AccessToken);
        }

        var page1 = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["page"] = "1", ["pageSize"] = "2" });

        var page2 = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["page"] = "2", ["pageSize"] = "2" });

        Assert.Equal(2, page1.Data!.Items.Count);
        Assert.True(page2.Data!.Items.Count >= 1);
        Assert.Equal(page1.Meta.TotalCount, page2.Meta.TotalCount);
        Assert.True(page1.Meta.TotalCount >= 3);
    }

    [Fact]
    public async Task Director_Search_Returns200()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        var envelope = await CaseTestData.SearchCasesAsync(_client, session.AccessToken, null);
        Assert.NotEmpty(envelope.Data!.Items);
    }

    [Fact]
    public async Task SocialWorker_Search_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendSearchAsync(_client, session.AccessToken, null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task DeactivatedCoordinator_Search_Returns403()
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
        var response = await CaseTestData.SendSearchAsync(probeClient, session.AccessToken, null);
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
    public async Task Unauthenticated_Search_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/cases/search");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCurrentStage_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendSearchAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["currentStage"] = "InvalidStage" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOffenceClassification_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendSearchAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["offenceClassification"] = "Unknown" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("offenceClassification", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidDomicile_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendSearchAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["domicile"] = "Suburban" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("domicile", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchByBeneficiaryNameWithPercent_MatchesLiterally()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var request = CaseTestData.BuildValidRequest();
        request.BeneficiaryName = "100% Pure";
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);

        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["q"] = "100%" });

        Assert.Contains(envelope.Data!.Items, i => i.BeneficiaryName == "100% Pure");
    }

    [Fact]
    public async Task Search_DoesNotWriteAudit()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.CreateCaseAsync(_client, session.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditCountBefore = await db.AuditEvents.CountAsync();

        await CaseTestData.SearchCasesAsync(_client, session.AccessToken, null);

        Assert.Equal(auditCountBefore, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Search_With50PlusCases_CompletesUnder2Seconds()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var targetCrime = $"CR-PERF-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        for (var i = 0; i < 50; i++)
        {
            var request = CaseTestData.BuildValidRequest();
            if (i == 25)
            {
                request.CrimeNumber = targetCrime;
            }

            await CaseTestData.CreateCaseAsync(_client, session.AccessToken, request);
        }

        var sw = Stopwatch.StartNew();
        var envelope = await CaseTestData.SearchCasesAsync(
            _client,
            session.AccessToken,
            new Dictionary<string, string?> { ["q"] = targetCrime });
        sw.Stop();

        Assert.Contains(envelope.Data!.Items, i => i.CrimeNumber == targetCrime);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Search took {sw.Elapsed.TotalSeconds:F2}s");
    }
}

[Collection("AuthIntegration")]
public class CaseSearchPresetTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CaseSearchPresetTests(AuthWebApplicationFactory factory)
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
    public async Task Coordinator_SaveListDeletePreset_Succeeds()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var name = $"Preset-{Guid.NewGuid():N}"[..20];

        var created = await CaseTestData.CreateSearchPresetAsync(
            _client,
            session.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = name,
                Filters = new CaseSearchFiltersDto { Q = "CR-", Domicile = "Urban" },
            });

        Assert.Equal(name, created.Name);
        Assert.Equal("Urban", created.Filters.Domicile);

        var list = await CaseTestData.ListSearchPresetsAsync(_client, session.AccessToken);
        Assert.Contains(list, p => p.Id == created.Id);

        await CaseTestData.DeleteSearchPresetAsync(_client, session.AccessToken, created.Id);
        var listAfter = await CaseTestData.ListSearchPresetsAsync(_client, session.AccessToken);
        Assert.DoesNotContain(listAfter, p => p.Id == created.Id);
    }

    [Fact]
    public async Task InvalidPresetName_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendCreatePresetAsync(
            _client,
            session.AccessToken,
            new CreateCaseSearchPresetRequest { Name = "   ", Filters = new CaseSearchFiltersDto() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PresetNameTooLong_Returns400()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendCreatePresetAsync(
            _client,
            session.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = new string('P', 65),
                Filters = new CaseSearchFiltersDto(),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("name", problem?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicatePresetName_Returns409()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var name = $"Dup-{Guid.NewGuid():N}"[..16];
        var request = new CreateCaseSearchPresetRequest
        {
            Name = name,
            Filters = new CaseSearchFiltersDto(),
        };

        await CaseTestData.CreateSearchPresetAsync(_client, session.AccessToken, request);
        var response = await CaseTestData.SendCreatePresetAsync(_client, session.AccessToken, request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnotherUsersPreset_Returns404()
    {
        var coordinatorSession = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var created = await CaseTestData.CreateSearchPresetAsync(
            _client,
            coordinatorSession.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = $"Other-{Guid.NewGuid():N}"[..16],
                Filters = new CaseSearchFiltersDto(),
            });

        var directorSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            AuthTestData.Password);

        var response = await CaseTestData.SendDeletePresetAsync(
            _client,
            directorSession.AccessToken,
            created.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUnknownPreset_Returns404()
    {
        var session = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendDeletePresetAsync(
            _client,
            session.AccessToken,
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaseWorker_PresetPost_Returns403()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendCreatePresetAsync(
            _client,
            session.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = "blocked",
                Filters = new CaseSearchFiltersDto(),
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedCoordinator_PresetPost_Returns403()
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
        var response = await CaseTestData.SendCreatePresetAsync(
            probeClient,
            session.AccessToken,
            new CreateCaseSearchPresetRequest
            {
                Name = "blocked",
                Filters = new CaseSearchFiltersDto(),
            });

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
    public async Task DeactivatedCoordinator_PresetList_Returns403()
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
        var response = await CaseTestData.SendListPresetsAsync(probeClient, session.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Unauthenticated_PresetList_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/cases/search-presets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
