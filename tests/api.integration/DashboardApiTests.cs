using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Supervisor;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class DashboardApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public DashboardApiTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await EnsureTestDataAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await SendGetDashboardAsync(_client, string.Empty);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_SocialWorker_Returns403()
    {
        var socialWorker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await SendGetDashboardAsync(_client, socialWorker.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_CaseWorker_Returns403()
    {
        var caseWorker = await VisitTestData.BuildCaseWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await SendGetDashboardAsync(_client, caseWorker.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_CoordinatorHasAccess_Returns200()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var response = await SendGetDashboardAsync(_client, coordinator.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_DirectorHasAccess_Returns200()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var response = await SendGetDashboardAsync(_client, director.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsAllExpectedWidgets()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        Assert.NotNull(dashboard.CasesByStage);
        Assert.NotNull(dashboard.CasesByOffenceClassification);
        Assert.NotNull(dashboard.CasesByDomicile);
        Assert.NotNull(dashboard.CasesByStaff);
        Assert.NotNull(dashboard.OverdueVisits);
        Assert.NotNull(dashboard.InterventionsGauge);
        Assert.NotNull(dashboard.CourtThisWeek);
        Assert.NotNull(dashboard.PendingClaims);
        Assert.NotNull(dashboard.IntakeTrend);
    }

    [Fact]
    public async Task Dashboard_CasesByStage_ReturnsAllStagesWithCorrectCounts()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        var stageMap = dashboard.CasesByStage.ToDictionary(s => s.Stage, s => s.Count);

        Assert.Contains("ProcessInitiation", stageMap.Keys);
        Assert.Contains("MaintainAndDevelopment", stageMap.Keys);

        // stage 1 has 2 cases
        Assert.Equal(2, stageMap["ProcessInitiation"]);
        // stage 2 has 2 cases
        Assert.Equal(2, stageMap["MaintainAndDevelopment"]);

        // TerminationExclusion should not appear
        Assert.DoesNotContain("TerminationExclusion", stageMap.Keys);
    }

    [Fact]
    public async Task Dashboard_CasesByOffenceClassification_ReturnsGroupedCounts()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        var offenceMap = dashboard.CasesByOffenceClassification.ToDictionary(o => o.OffenceClassification, o => o.Count);

        // 3 cases with Petty, 1 with Serious
        Assert.Equal(3, offenceMap.GetValueOrDefault("Petty", 0));
        Assert.Equal(1, offenceMap.GetValueOrDefault("Serious", 0));
    }

    [Fact]
    public async Task Dashboard_CasesByDomicile_ReturnsGroupedCounts()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        var domicileMap = dashboard.CasesByDomicile.ToDictionary(d => d.Domicile, d => d.Count);

        // 2 cases with Rural, 2 with Urban
        Assert.Equal(2, domicileMap.GetValueOrDefault("Rural", 0));
        Assert.Equal(2, domicileMap.GetValueOrDefault("Urban", 0));
    }

    [Fact]
    public async Task Dashboard_CasesByStaff_IncludesWorkersWithCases()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        Assert.NotEmpty(dashboard.CasesByStaff);

        var coordinatorEntry = dashboard.CasesByStaff.FirstOrDefault(s => s.WorkerName.Contains("coordinator", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(coordinatorEntry);
        Assert.True(coordinatorEntry.CaseCount >= 2);
    }

    [Fact]
    public async Task Dashboard_OverdueVisits_CountsCorrectly()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        // 2 overdue visits for the same case
        Assert.Equal(2, dashboard.OverdueVisits.TotalOverdue);
        Assert.Equal(1, dashboard.OverdueVisits.UniqueCasesAffected);
    }

    [Fact]
    public async Task Dashboard_InterventionsGauge_CountsCorrectly()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        // 1 Open + 1 InProgress = 2 in-progress
        Assert.Equal(2, dashboard.InterventionsGauge.InProgress);
        // 1 overdue (past DueAtUtc, not Completed/Cancelled)
        Assert.Equal(1, dashboard.InterventionsGauge.Overdue);
        // 1 completed this month
        Assert.Equal(1, dashboard.InterventionsGauge.CompletedThisMonth);
    }

    [Fact]
    public async Task Dashboard_CourtThisWeek_CountsCurrentWeek()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        // 1 Upcoming sitting this week
        Assert.Equal(1, dashboard.CourtThisWeek.TotalUpcoming);
        // 1 Attended sitting this week
        Assert.Equal(1, dashboard.CourtThisWeek.AttendedSoFar);
        // 2 distinct cases with sittings this week (case3 + case4)
        Assert.Equal(2, dashboard.CourtThisWeek.TotalCasesWithSittings);
    }

    [Fact]
    public async Task Dashboard_PendingClaims_CountsCorrectly()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        Assert.Equal(1, dashboard.PendingClaims.PendingCount);
        Assert.True(dashboard.PendingClaims.TotalAmountPending > 0);
        Assert.True(dashboard.PendingClaims.OldestPendingDays >= 0);
    }

    [Fact]
    public async Task Dashboard_IntakeTrend_Returns12Months()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var dashboard = await GetDashboardAsync(_client, coordinator.AccessToken);

        Assert.Equal(12, dashboard.IntakeTrend.Count);
        // Current month should have 4 cases
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var currentEntry = dashboard.IntakeTrend.FirstOrDefault(i => i.Month == currentMonth);
        Assert.NotNull(currentEntry);
        Assert.Equal(4, currentEntry.Count);
    }

    [Fact]
    public async Task Dashboard_RedisCache_ReturnsCachedResultWithinTTL()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var firstResult = await GetDashboardAsync(_client, coordinator.AccessToken);

        // Second call within 60s should return cached data
        var secondResult = await GetDashboardAsync(_client, coordinator.AccessToken);

        Assert.Equal(firstResult.CasesByStage.Count, secondResult.CasesByStage.Count);
        Assert.Equal(firstResult.OverdueVisits.TotalOverdue, secondResult.OverdueVisits.TotalOverdue);
        Assert.Equal(firstResult.CasesByOffenceClassification.Count, secondResult.CasesByOffenceClassification.Count);
    }

    private static async Task<DashboardResultDto> GetDashboardAsync(HttpClient client, string accessToken)
    {
        var response = await SendGetDashboardAsync(client, accessToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<DashboardApiEnvelope>();
        return envelope!.Data!;
    }

    private static Task<HttpResponseMessage> SendGetDashboardAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/supervisor/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private sealed record DashboardApiMeta(string RequestId, int? TotalCount);

    private sealed record DashboardApiEnvelope(DashboardResultDto Data, DashboardApiMeta Meta);

    /// <summary>
    /// Seeds test data for all dashboard widgets: cases across stages/offences/domiciles,
    /// overdue visits, interventions, court this week, pending claims, and intake history.
    /// </summary>
    private async Task EnsureTestDataAsync()
    {
        await RbacTestData.EnsureRoleUsersAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orgId = AuthTestData.OrganisationId;
        var now = DateTime.UtcNow;

        // Check if dashboard test data already exists
        if (await db.Cases.AnyAsync(c => c.CrimeNumber == "DASH-CR-001"))
        {
            return; // Already seeded
        }

        var coordinatorId = await GetUserIdByEmailAsync(db, RbacTestData.CoordinatorEmail);
        var caseWorkerId = await GetUserIdByEmailAsync(db, RbacTestData.CaseWorkerEmail);
        var socialWorkerId = await GetUserIdByEmailAsync(db, RbacTestData.SocialWorkerEmail);

        // Create 4 cases with different stage/offence/domicile combinations
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();
        var case3Id = Guid.NewGuid();
        var case4Id = Guid.NewGuid();

        var cases = new List<Case>
        {
            new()
            {
                Id = case1Id,
                OrganisationId = orgId,
                CrimeNumber = "DASH-CR-001",
                StNumber = "DASH-ST-001",
                BeneficiaryName = "Dashboard Test 1",
                CurrentStage = CaseStage.ProcessInitiation,
                OffenceClassification = OffenceClassification.Petty,
                Domicile = Domicile.Rural,
                AssignedWorkerId = coordinatorId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = case2Id,
                OrganisationId = orgId,
                CrimeNumber = "DASH-CR-002",
                StNumber = "DASH-ST-002",
                BeneficiaryName = "Dashboard Test 2",
                CurrentStage = CaseStage.ProcessInitiation,
                OffenceClassification = OffenceClassification.Petty,
                Domicile = Domicile.Rural,
                AssignedWorkerId = coordinatorId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = case3Id,
                OrganisationId = orgId,
                CrimeNumber = "DASH-CR-003",
                StNumber = "DASH-ST-003",
                BeneficiaryName = "Dashboard Test 3",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                OffenceClassification = OffenceClassification.Petty,
                Domicile = Domicile.Urban,
                AssignedWorkerId = caseWorkerId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = case4Id,
                OrganisationId = orgId,
                CrimeNumber = "DASH-CR-004",
                StNumber = "DASH-ST-004",
                BeneficiaryName = "Dashboard Test 4",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                OffenceClassification = OffenceClassification.Serious,
                Domicile = Domicile.Urban,
                AssignedWorkerId = socialWorkerId,
                CreatedByUserId = coordinatorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
        };

        db.Cases.AddRange(cases);

        // Overdue visits: 2 overdue visits on case1
        db.Visits.Add(new Visit
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case1Id,
            AssigneeUserId = socialWorkerId,
            ScheduledAtUtc = now.AddDays(-5),
            Status = VisitStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-7),
            UpdatedAtUtc = now,
        });
        db.Visits.Add(new Visit
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case1Id,
            AssigneeUserId = socialWorkerId,
            ScheduledAtUtc = now.AddDays(-3),
            Status = VisitStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-5),
            UpdatedAtUtc = now,
        });

        // Interventions: 1 Open, 1 InProgress, 1 overdue (past due, not completed)
        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case2Id,
            Direction = InterventionDirection.Needed,
            CategoryName = "Counseling",
            Description = "Open intervention",
            Priority = InterventionPriority.High,
            Status = InterventionStatus.Open,
            DueAtUtc = now.AddDays(5),
            AssignedStaffUserId = caseWorkerId,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-3),
            UpdatedAtUtc = now,
        });
        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case3Id,
            Direction = InterventionDirection.Needed,
            CategoryName = "Medical",
            Description = "In-progress intervention",
            Priority = InterventionPriority.Medium,
            Status = InterventionStatus.InProgress,
            DueAtUtc = now.AddDays(10),
            AssignedStaffUserId = socialWorkerId,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now,
        });
        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case4Id,
            Direction = InterventionDirection.Needed,
            CategoryName = "Legal aid",
            Description = "Overdue intervention",
            Priority = InterventionPriority.High,
            Status = InterventionStatus.Open,
            DueAtUtc = now.AddDays(-2),
            AssignedStaffUserId = caseWorkerId,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now,
        });
        // Completed intervention this month (for CompletedThisMonth assertion)
        db.Interventions.Add(new Intervention
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case2Id,
            Direction = InterventionDirection.Provided,
            CategoryName = "Health check",
            Description = "Completed intervention this month",
            Priority = InterventionPriority.Medium,
            Status = InterventionStatus.Completed,
            ProvidedAtUtc = now.AddDays(-1),
            DueAtUtc = now.AddDays(-2),
            AssignedStaffUserId = socialWorkerId,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now,
        });

        // Court sittings this week: 1 Upcoming on case3, 1 Attended on case4
        var daysSinceMonday = (int)now.DayOfWeek - (int)DayOfWeek.Monday;
        if (daysSinceMonday < 0) daysSinceMonday += 7;
        var weekStart = now.Date.AddDays(-daysSinceMonday);
        var midWeek = weekStart.AddDays(2);

        db.CourtSittings.Add(new CourtSitting
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case3Id,
            ScheduledAtUtc = midWeek.AddHours(10),
            CourtName = "District Court",
            Purpose = "Hearing",
            Status = CourtSittingStatus.Upcoming,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-5),
            UpdatedAtUtc = now,
        });
        // Attended sitting this week on case4
        db.CourtSittings.Add(new CourtSitting
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = case4Id,
            ScheduledAtUtc = weekStart.AddHours(9),
            CourtName = "Sessions Court",
            Purpose = "Trial",
            Status = CourtSittingStatus.Attended,
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-3),
            UpdatedAtUtc = now,
        });

        // Pending travel claim linked to case4
        var claimId = Guid.NewGuid();
        db.TravelClaims.Add(new TravelClaim
        {
            Id = claimId,
            OrganisationId = orgId,
            ClaimantUserId = socialWorkerId,
            ClaimDate = now.AddDays(-3),
            StartLocation = "Field Office",
            Destination = "Court Complex",
            TransportMode = TransportMode.Bus,
            Amount = 250.00m,
            Status = TravelClaimStatus.Submitted,
            SubmittedAtUtc = now.AddDays(-2),
            CreatedAtUtc = now.AddDays(-3),
            UpdatedAtUtc = now,
        });
        db.Set<TravelClaimCaseLink>().Add(new TravelClaimCaseLink
        {
            TravelClaimId = claimId,
            CaseId = case4Id,
            OrganisationId = orgId,
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> GetUserIdByEmailAsync(AppDbContext db, string email)
    {
        var user = await db.Users.SingleAsync(u => u.Email == email.Trim().ToLowerInvariant());
        return user.Id;
    }
}
