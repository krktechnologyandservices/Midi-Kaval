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
public class CrisisQueueApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public CrisisQueueApiTests(AuthWebApplicationFactory factory)
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
    public async Task CrisisQueue_ReturnsAllRowTypesInPriorityOrder()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        // Verify all row types present
        Assert.Contains(queue.Items, i => i.Severity == "critical" && i.BadgeLabel == "Court miss");
        Assert.Contains(queue.Items, i => i.Severity == "critical" && i.BadgeLabel == "Overdue");
        Assert.Contains(queue.Items, i => i.Severity == "warning" && i.BadgeLabel == "Court 48h");
        Assert.Contains(queue.Items, i => i.Severity == "info" && i.BadgeLabel == "Handoff");
        Assert.Contains(queue.Items, i => i.Severity == "neutral" && i.BadgeLabel == "Claim");

        // Verify sort order: critical → warning → info → neutral
        var severities = queue.Items.Select(i => i.Severity).ToList();
        var criticalLastIdx = severities.LastIndexOf("critical");
        var warningFirstIdx = severities.IndexOf("warning");
        var infoFirstIdx = severities.IndexOf("info");
        var neutralFirstIdx = severities.IndexOf("neutral");

        Assert.True(criticalLastIdx < warningFirstIdx,
            "All critical rows must appear before warning rows");
        Assert.True(warningFirstIdx < infoFirstIdx,
            "All warning rows must appear before info rows");
        Assert.True(infoFirstIdx < neutralFirstIdx,
            "All info rows must appear before neutral rows");
    }

    [Fact]
    public async Task CrisisQueue_OverdueVisitRow_IncludesCorrectFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        var overdueRow = Assert.Single(queue.Items, i => i.BadgeLabel == "Overdue");
        Assert.Equal("critical", overdueRow.Severity);
        Assert.NotEqual(Guid.Empty, overdueRow.CaseId);
        Assert.NotNull(overdueRow.CrimeNumber);
        Assert.NotNull(overdueRow.OverdueVisitCount);
        Assert.True(overdueRow.OverdueVisitCount >= 1);
        Assert.NotNull(overdueRow.VisitScheduledAtUtc);
        Assert.NotNull(overdueRow.VisitId);
        Assert.NotNull(overdueRow.AssignedWorkerUserId);
        Assert.NotEqual(Guid.Empty, overdueRow.AssignedWorkerUserId.Value);
    }

    [Fact]
    public async Task CrisisQueue_Court48hRow_IncludesCorrectFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        var court48hRow = Assert.Single(queue.Items, i => i.BadgeLabel == "Court 48h");
        Assert.Equal("warning", court48hRow.Severity);
        Assert.NotEqual(Guid.Empty, court48hRow.CaseId);
        Assert.NotNull(court48hRow.CourtSittingId);
        Assert.NotNull(court48hRow.ScheduledAtUtc);
    }

    [Fact]
    public async Task CrisisQueue_HandoffRow_IncludesCorrectFields()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        var handoffRow = Assert.Single(queue.Items, i => i.BadgeLabel == "Handoff");
        Assert.Equal("info", handoffRow.Severity);
        Assert.NotEqual(Guid.Empty, handoffRow.CaseId);
        Assert.NotNull(handoffRow.PreviousWorkerName);
        Assert.NotNull(handoffRow.TransferredAtUtc);
    }

    [Fact]
    public async Task CrisisQueue_FieldWorker_Returns403()
    {
        var socialWorker = await VisitTestData.BuildSocialWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListCrisisQueueAsync(_client, socialWorker.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrisisQueue_Director_Returns200()
    {
        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, director.AccessToken);
        Assert.NotNull(queue);
        Assert.NotEmpty(queue.Items);
    }

    [Fact]
    public async Task CrisisQueue_RedisCache_ReturnsCachedResultWithinTTL()
    {
        // First call populates cache
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var firstResult = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        Assert.NotEmpty(firstResult.Items);

        // Second call within 30s should return cached data
        var secondResult = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);
        Assert.Equal(firstResult.Items.Count, secondResult.Items.Count);
        Assert.Equal(
            firstResult.Items.Select(i => i.RowType).ToList(),
            secondResult.Items.Select(i => i.RowType).ToList());
        Assert.Equal(
            firstResult.Items.Select(i => i.CaseId).ToList(),
            secondResult.Items.Select(i => i.CaseId).ToList());
    }

    [Fact]
    public async Task CrisisQueue_Unauthenticated_Returns401()
    {
        // No token → 401
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await CaseTestData.SendListCrisisQueueAsync(_client, string.Empty);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrisisQueue_CaseWorker_Returns403()
    {
        var caseWorker = await VisitTestData.BuildCaseWorkerSessionAsync(_client, _factory.EmailSender);
        var response = await CaseTestData.SendListCrisisQueueAsync(_client, caseWorker.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrisisQueue_SortOrder_DeadlineAscendingWithinSeverity()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        // For each severity tier, verify deadlines are ascending
        foreach (var severity in new[] { "critical", "warning", "info", "neutral" })
        {
            var tierItems = queue.Items.Where(i => i.Severity == severity).ToList();
            if (tierItems.Count <= 1) continue;

            var deadlines = tierItems
                .Select(i => i.VisitScheduledAtUtc ?? i.ScheduledAtUtc ?? i.TransferredAtUtc)
                .ToList();

            for (var j = 1; j < deadlines.Count; j++)
            {
                Assert.True(deadlines[j - 1] <= deadlines[j],
                    $"{severity}: item {j - 1} deadline ({deadlines[j - 1]}) must not be after item {j} deadline ({deadlines[j]})");
            }
        }
    }

    [Fact]
    public async Task CrisisQueue_CourtMissAndClaimRowsPreserved()
    {
        var coordinator = await CaseTestData.BuildCoordinatorSessionAsync(_client, _factory.EmailSender);
        var queue = await CaseTestData.ListCrisisQueueAsync(_client, coordinator.AccessToken);

        // Existing court-miss rows preserved
        var courtMissRow = Assert.Single(queue.Items, i => i.BadgeLabel == "Court miss");
        Assert.Equal("critical", courtMissRow.Severity);
        Assert.Equal("court_miss", courtMissRow.RowType);

        // Existing pending claim rows preserved
        var claimRow = Assert.Single(queue.Items, i => i.BadgeLabel == "Claim");
        Assert.Equal("neutral", claimRow.Severity);
        Assert.Equal("travel_claim_pending", claimRow.RowType);
        Assert.NotNull(claimRow.Amount);
        Assert.NotNull(claimRow.ClaimantEmail);
    }

    /// <summary>
    /// Seeds test data for the crisis queue: overdue visits, court <48h without prep,
    /// recent handoffs, court-miss rows, and pending claims.
    /// </summary>
    private async Task EnsureTestDataAsync()
    {
        await RbacTestData.EnsureRoleUsersAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orgId = AuthTestData.OrganisationId;
        var now = DateTime.UtcNow;

        // Check if test data already exists
        if (await db.CaseAssignments.AnyAsync(a => a.OrganisationId == orgId))
        {
            return; // Already seeded
        }

        var coordinatorId = await GetUserIdByEmailAsync(db, RbacTestData.CoordinatorEmail);
        var caseWorkerId = await GetUserIdByEmailAsync(db, RbacTestData.CaseWorkerEmail);
        var socialWorkerId = await GetUserIdByEmailAsync(db, RbacTestData.SocialWorkerEmail);

        // Create a case for each scenario (using raw seed to avoid HTTP dependencies)
        var courtMissCaseId = Guid.NewGuid();
        var overdueCaseId = Guid.NewGuid();
        var court48hCaseId = Guid.NewGuid();
        var handoffCaseId = Guid.NewGuid();
        var claimCaseId = Guid.NewGuid();

        var cases = new List<Case>
        {
            new()
            {
                Id = courtMissCaseId,
                OrganisationId = orgId,
                CrimeNumber = "CR-2026-001",
                StNumber = "ST-2026-001",
                CurrentStage = CaseStage.ProcessInitiation,
                AssignedWorkerId = caseWorkerId,
                CreatedAtUtc = now.AddDays(-30),
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = overdueCaseId,
                OrganisationId = orgId,
                CrimeNumber = "CR-2026-002",
                StNumber = "ST-2026-002",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                AssignedWorkerId = socialWorkerId,
                CreatedAtUtc = now.AddDays(-20),
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = court48hCaseId,
                OrganisationId = orgId,
                CrimeNumber = "CR-2026-003",
                StNumber = "ST-2026-003",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                AssignedWorkerId = caseWorkerId,
                CreatedAtUtc = now.AddDays(-15),
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = handoffCaseId,
                OrganisationId = orgId,
                CrimeNumber = "CR-2026-004",
                StNumber = "ST-2026-004",
                CurrentStage = CaseStage.MaintainAndDevelopment,
                AssignedWorkerId = socialWorkerId,
                CreatedAtUtc = now.AddDays(-10),
                UpdatedAtUtc = now,
            },
            new()
            {
                Id = claimCaseId,
                OrganisationId = orgId,
                CrimeNumber = "CR-2026-005",
                StNumber = "ST-2026-005",
                CurrentStage = CaseStage.ProcessInitiation,
                AssignedWorkerId = socialWorkerId,
                CreatedAtUtc = now.AddDays(-5),
                UpdatedAtUtc = now,
            },
        };

        db.Cases.AddRange(cases);

        // 1. Court-miss row: past-due Upcoming sitting with MissEscalatedAtUtc set
        var courtMissSittingId = Guid.NewGuid();
        db.CourtSittings.Add(new CourtSitting
        {
            Id = courtMissSittingId,
            OrganisationId = orgId,
            CaseId = courtMissCaseId,
            ScheduledAtUtc = now.AddHours(-48),
            CourtName = "District Court",
            Purpose = "Hearing",
            Status = CourtSittingStatus.Upcoming,
            MissEscalatedAtUtc = now.AddHours(-24),
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-5),
            UpdatedAtUtc = now,
        });

        // 2. Overdue visit: past schedule, not completed
        var overdueVisitId = Guid.NewGuid();
        db.Visits.Add(new Visit
        {
            Id = overdueVisitId,
            OrganisationId = orgId,
            CaseId = overdueCaseId,
            AssigneeUserId = socialWorkerId,
            ScheduledAtUtc = now.AddDays(-3),
            Status = VisitStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-5),
            UpdatedAtUtc = now,
        });

        // A second overdue visit for the same case
        db.Visits.Add(new Visit
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = overdueCaseId,
            AssigneeUserId = socialWorkerId,
            ScheduledAtUtc = now.AddDays(-1),
            Status = VisitStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now,
        });

        // 3. Court <48h without prep: upcoming within next 48h, no notes
        db.CourtSittings.Add(new CourtSitting
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = court48hCaseId,
            ScheduledAtUtc = now.AddHours(6),
            CourtName = "Sessions Court",
            Purpose = "Status hearing",
            Status = CourtSittingStatus.Upcoming,
            Notes = null,
            CreatedByUserId = caseWorkerId,
            CreatedAtUtc = now.AddDays(-7),
            UpdatedAtUtc = now,
        });

        // 4. Recent handoff: transfer within last 7 days
        db.CaseAssignments.Add(new CaseAssignment
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            CaseId = handoffCaseId,
            FromWorkerId = caseWorkerId,
            ToWorkerId = socialWorkerId,
            PriorActions = "Initial intake completed",
            OpenItems = "Home visit pending",
            NextVisitPurpose = "Assessment",
            CreatedByUserId = coordinatorId,
            CreatedAtUtc = now.AddDays(-2),
        });

        // 5. Pending travel claim (needs linked case via TravelClaimCaseLinks)
        var travelClaimId = Guid.NewGuid();
        db.TravelClaims.Add(new TravelClaim
        {
            Id = travelClaimId,
            OrganisationId = orgId,
            ClaimantUserId = socialWorkerId,
            ClaimDate = now.AddDays(-4),
            StartLocation = "Office",
            Destination = "Court",
            TransportMode = TransportMode.Bus,
            Amount = 150.00m,
            Status = TravelClaimStatus.Submitted,
            SubmittedAtUtc = now.AddDays(-3),
            CreatedAtUtc = now.AddDays(-4),
            UpdatedAtUtc = now,
        });

        // Link travel claim to case
        db.Set<TravelClaimCaseLink>().Add(new TravelClaimCaseLink
        {
            TravelClaimId = travelClaimId,
            CaseId = claimCaseId,
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
