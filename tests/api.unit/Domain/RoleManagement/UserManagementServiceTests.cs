using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Admin;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class UserManagementServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static Organisation SeedOrganisation(AppDbContext db)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        db.SaveChanges();
        return org;
    }

    private static User SeedUser(
        AppDbContext db,
        Guid orgId,
        string email,
        DateTime createdAtUtc,
        string role = UserRoles.Director,
        bool isActive = true,
        bool isSuspended = false,
        string firstName = "Test",
        string lastName = "User")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            TokenVersion = 0,
            IsActive = isActive,
            IsSuspended = isSuspended,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    /// <summary>
    /// Testable subclass that overrides ILike search filter to use Contains,
    /// since EF.Functions.ILike is not supported by the InMemory provider.
    /// Also captures enqueued background jobs instead of running Hangfire.
    /// </summary>
    private sealed class TestableUserManagementService : UserManagementService
    {
        private static readonly ZeroDirectorTriggerService NoopTrigger =
            new(null!, null!, NullLogger<ZeroDirectorTriggerService>.Instance);

        public List<(Guid userId, string email, string name, string actionType, string? reason)> EnqueuedJobs { get; } = [];

        public List<Guid> NotifiedRemovedUserIds { get; } = [];

        public TestableUserManagementService(AppDbContext db, IAuditService auditService, LastDirectorGuard lastDirectorGuard, ZeroDirectorTriggerService? zeroDirectorTrigger = null)
            : base(db, auditService, lastDirectorGuard, zeroDirectorTrigger ?? NoopTrigger, NullLogger<UserManagementService>.Instance) { }

        protected override IQueryable<User> ApplySearchFilter(IQueryable<User> query, string term)
        {
            var lowered = term.ToLowerInvariant();
            return query.Where(u =>
                u.Email.ToLower().Contains(lowered) ||
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(lowered));
        }

        protected override void EnqueueStatusEmailJob(Guid userId, string email, string name, string actionType, string? reason, CancellationToken ct)
        {
            EnqueuedJobs.Add((userId, email, name, actionType, reason));
        }

        protected override void EnqueueDeletionEmailJob(string originalEmail, string originalName, CancellationToken ct)
        {
            EnqueuedJobs.Add((Guid.Empty, originalEmail, originalName, "deleted", null));
        }

        protected override Task NotifyUserRemovedAsync(Guid organisationId, Guid userId, CancellationToken ct)
        {
            NotifiedRemovedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }

    private static LastDirectorGuard CreateGuard(AppDbContext db) => new(db);

    private static UserManagementService CreateService(AppDbContext db)
    {
        return new UserManagementService(db, new FakeAuditService(), CreateGuard(db),
            new ZeroDirectorTriggerService(db, CreateGuard(db), NullLogger<ZeroDirectorTriggerService>.Instance),
            NullLogger<UserManagementService>.Instance);
    }

    private static (TestableUserManagementService service, FakeAuditService audit) CreateTestableService(AppDbContext db)
    {
        var audit = new FakeAuditService();
        var guard = CreateGuard(db);
        return (new TestableUserManagementService(db, audit, guard), audit);
    }

    [Fact]
    public async Task ReturnsPaginatedUsersScopedToOrganisation()
    {
        using var db = CreateContext();
        var org1 = SeedOrganisation(db);
        var org2 = SeedOrganisation(db);

        SeedUser(db, org1.Id, "user1@org1.com", DateTime.UtcNow.AddDays(-2));
        SeedUser(db, org1.Id, "user2@org1.com", DateTime.UtcNow.AddDays(-1));
        SeedUser(db, org2.Id, "user@org2.com", DateTime.UtcNow);

        var service = CreateService(db);
        var result = await service.GetUserListAsync(org1.Id, page: 1, pageSize: 25);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, u => u.Email == "user@org2.com");
    }

    [Fact]
    public async Task ReturnsEmptyListWhenNoUsersMatch()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var service = CreateService(db);
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ReturnsCorrectTotalCount()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        for (int i = 0; i < 10; i++)
        {
            SeedUser(db, org.Id, $"user{i}@org.com", DateTime.UtcNow.AddDays(-i));
        }

        var service = CreateService(db);
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 5);

        Assert.Equal(10, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task OrdersByCreatedAtUtcDescending()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var older = SeedUser(db, org.Id, "older@org.com", DateTime.UtcNow.AddDays(-5));
        var newer = SeedUser(db, org.Id, "newer@org.com", DateTime.UtcNow.AddDays(-1));

        var service = CreateService(db);
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(newer.Id, result.Items[0].Id);
        Assert.Equal(older.Id, result.Items[1].Id);
    }

    [Fact]
    public async Task ReturnsPage2Correctly()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        for (int i = 0; i < 10; i++)
        {
            SeedUser(db, org.Id, $"user{i}@org.com", DateTime.UtcNow.AddDays(-i));
        }

        var service = CreateService(db);
        var page1 = await service.GetUserListAsync(org.Id, page: 1, pageSize: 4);
        var page2 = await service.GetUserListAsync(org.Id, page: 2, pageSize: 4);
        var page3 = await service.GetUserListAsync(org.Id, page: 3, pageSize: 4);

        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(4, page2.Items.Count);
        Assert.Equal(2, page3.Items.Count);
    }

    [Fact]
    public async Task SearchByEmail_ReturnsMatchingUsers()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "john@example.com", DateTime.UtcNow);
        SeedUser(db, org.Id, "jane@example.com", DateTime.UtcNow);
        SeedUser(db, org.Id, "bob@other.com", DateTime.UtcNow);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, searchTerm: "example");

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, u => u.Email == "john@example.com");
        Assert.Contains(result.Items, u => u.Email == "jane@example.com");
        Assert.DoesNotContain(result.Items, u => u.Email == "bob@other.com");
    }

    [Fact]
    public async Task FilterByRole_ReturnsMatchingUsers()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        SeedUser(db, org.Id, "coord@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);
        SeedUser(db, org.Id, "worker@org.com", DateTime.UtcNow, role: UserRoles.SocialWorker);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(
            org.Id, page: 1, pageSize: 25, roles: $"{UserRoles.Coordinator},{UserRoles.SocialWorker}");

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, u => u.Email == "director@org.com");
    }

    [Fact]
    public async Task FilterByStatusActive_ReturnsOnlyActiveUsers()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "active@org.com", DateTime.UtcNow, isActive: true, isSuspended: false);
        SeedUser(db, org.Id, "suspended@org.com", DateTime.UtcNow, isActive: true, isSuspended: true);
        SeedUser(db, org.Id, "inactive@org.com", DateTime.UtcNow, isActive: false, isSuspended: false);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, statusFilter: "active");

        Assert.Single(result.Items);
        Assert.Equal("active@org.com", result.Items[0].Email);
    }

    [Fact]
    public async Task FilterByStatusSuspended_ReturnsOnlySuspendedUsers()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "active@org.com", DateTime.UtcNow, isActive: true, isSuspended: false);
        SeedUser(db, org.Id, "suspended@org.com", DateTime.UtcNow, isActive: true, isSuspended: true);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, statusFilter: "suspended");

        Assert.Single(result.Items);
        Assert.Equal("suspended@org.com", result.Items[0].Email);
    }

    [Fact]
    public async Task CombinedFilters_ApplyAndLogic()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "alice@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, isActive: true);
        SeedUser(db, org.Id, "bob@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, isActive: true, isSuspended: true);
        SeedUser(db, org.Id, "charlie@org.com", DateTime.UtcNow, role: UserRoles.SocialWorker, isActive: true);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(
            org.Id, page: 1, pageSize: 25,
            roles: UserRoles.Coordinator, statusFilter: "active");

        Assert.Single(result.Items);
        Assert.Equal("alice@org.com", result.Items[0].Email);
    }

    [Fact]
    public async Task SortByNameAscending()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "b@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, firstName: "Aaron", lastName: "Able");
        SeedUser(db, org.Id, "a@org.com", DateTime.UtcNow.AddDays(-1), role: UserRoles.Director, firstName: "Zara", lastName: "Zebra");

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, sortBy: "name", sortDescending: false);

        Assert.Equal("Aaron", result.Items[0].FirstName);
        Assert.Equal("Zara", result.Items[1].FirstName);
    }

    [Fact]
    public async Task SortByNameDescending()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "a@org.com", DateTime.UtcNow.AddDays(-1), role: UserRoles.Director, firstName: "Zara", lastName: "Zebra");
        SeedUser(db, org.Id, "b@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, firstName: "Aaron", lastName: "Able");

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, sortBy: "name", sortDescending: true);

        Assert.Equal("Zara", result.Items[0].FirstName);
        Assert.Equal("Aaron", result.Items[1].FirstName);
    }

    [Fact]
    public async Task SortByEmail()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "z@org.com", DateTime.UtcNow);
        SeedUser(db, org.Id, "a@org.com", DateTime.UtcNow.AddDays(-1));

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, sortBy: "email", sortDescending: false);

        Assert.Equal("a@org.com", result.Items[0].Email);
        Assert.Equal("z@org.com", result.Items[1].Email);
    }

    [Fact]
    public async Task SortByRole()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "z@org.com", DateTime.UtcNow, role: UserRoles.SocialWorker);
        SeedUser(db, org.Id, "a@org.com", DateTime.UtcNow.AddDays(-1), role: UserRoles.Director);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(org.Id, page: 1, pageSize: 25, sortBy: "role", sortDescending: false);

        Assert.Equal(UserRoles.Director, result.Items[0].Role);
        Assert.Equal(UserRoles.SocialWorker, result.Items[1].Role);
    }

    [Fact]
    public async Task EmptyResultsWithFilters()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, "user@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var service = CreateTestableService(db).service;
        var result = await service.GetUserListAsync(
            org.Id, page: 1, pageSize: 25,
            searchTerm: "nonexistent");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SuspendAsync_SuspendsSuccessfully()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, audit) = CreateTestableService(db);
        var result = await service.SuspendAsync(org.Id, director.Id, target.Id, "Test reason", default);

        Assert.True(result.IsSuspended);
        Assert.Contains("suspended", result.Message);

        var saved = await db.Users.FirstAsync(u => u.Id == target.Id);
        Assert.True(saved.IsSuspended);
        Assert.NotNull(saved.SuspendedAtUtc);
        Assert.Equal(1, saved.TokenVersion);

        Assert.Single(audit.RecordedEvents);
        Assert.Equal(AuditEventTypes.UserSuspended, audit.RecordedEvents[0].eventType);

        Assert.Single(service.EnqueuedJobs);
        Assert.Equal("suspended", service.EnqueuedJobs[0].actionType);
    }

    [Fact]
    public async Task SuspendAsync_AlreadySuspended_ReturnsConflict()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, isSuspended: true);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SuspendAsync(org.Id, director.Id, target.Id, null, default));

        Assert.Contains("already suspended", ex.Message);
    }

    [Fact]
    public async Task SuspendAsync_SelfSuspend_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SuspendAsync(org.Id, director.Id, director.Id, null, default));

        Assert.Contains("cannot suspend your own account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuspendAsync_LastDirector_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var otherDirector = SeedUser(db, org.Id, "other@org.com", DateTime.UtcNow.AddDays(-1), role: UserRoles.Director, isActive: false);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SuspendAsync(org.Id, otherDirector.Id, director.Id, null, default));

        Assert.Contains("no other active Director", ex.Message);
    }

    [Fact]
    public async Task SuspendAsync_UserNotFound_Returns404()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SuspendAsync(org.Id, director.Id, Guid.NewGuid(), null, default));
    }

    [Fact]
    public async Task ReactivateAsync_ReactivatesSuccessfully()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, isSuspended: true);

        var (service, audit) = CreateTestableService(db);
        var result = await service.ReactivateAsync(org.Id, director.Id, target.Id, default);

        Assert.False(result.IsSuspended);
        Assert.Contains("reactivated", result.Message);

        var saved = await db.Users.FirstAsync(u => u.Id == target.Id);
        Assert.False(saved.IsSuspended);
        Assert.Null(saved.SuspendedAtUtc);

        Assert.Single(audit.RecordedEvents);
        Assert.Equal(AuditEventTypes.UserReactivated, audit.RecordedEvents[0].eventType);

        Assert.Single(service.EnqueuedJobs);
        Assert.Equal("reactivated", service.EnqueuedJobs[0].actionType);
    }

    [Fact]
    public async Task ReactivateAsync_NotSuspended_ReturnsConflict()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReactivateAsync(org.Id, director.Id, target.Id, default));

        Assert.Contains("not suspended", ex.Message);
    }

    [Fact]
    public async Task ReactivateAsync_SelfReactivate_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReactivateAsync(org.Id, director.Id, director.Id, default));

        Assert.Contains("cannot reactivate your own account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivateAsync_UserNotFound_Returns404()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReactivateAsync(org.Id, director.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task DeleteAsync_DeletesSuccessfully()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, audit) = CreateTestableService(db);
        var result = await service.DeleteAsync(org.Id, director.Id, target.Id, "target@org.com", default);

        Assert.Contains("deleted", result.Message);

        var saved = await db.Users.FirstAsync(u => u.Id == target.Id);
        Assert.Equal("Deleted", saved.FirstName);
        Assert.Equal("User", saved.LastName);
        Assert.StartsWith("deleted-", saved.Email);
        Assert.Equal(string.Empty, saved.PasswordHash);
        Assert.False(saved.IsSuspended);
        Assert.False(saved.IsActive);
        Assert.Null(saved.PhoneNumber);
        Assert.Null(saved.TotpSecret);
        Assert.Null(saved.TotpEnrolledAt);
        Assert.Equal(1, saved.TokenVersion);

        Assert.Single(audit.RecordedEvents);
        Assert.Equal(AuditEventTypes.UserDeleted, audit.RecordedEvents[0].eventType);

        Assert.Single(service.EnqueuedJobs);
        Assert.Equal("deleted", service.EnqueuedJobs[0].actionType);
        Assert.Equal("target@org.com", service.EnqueuedJobs[0].email);
    }

    [Fact]
    public async Task DeleteAsync_EmailMismatch_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(org.Id, director.Id, target.Id, "wrong@email.com", default));

        Assert.Contains("Email confirmation does not match", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_SelfDelete_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(org.Id, director.Id, director.Id, "director@org.com", default));

        Assert.Contains("cannot delete your own account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_LastDirector_Returns400()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var otherDirector = SeedUser(db, org.Id, "other@org.com", DateTime.UtcNow.AddDays(-1), role: UserRoles.Director, isActive: false);

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(org.Id, otherDirector.Id, director.Id, "director@org.com", default));

        Assert.Contains("no other active Director", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_UserNotFound_Returns404()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);

        var (service, _) = CreateTestableService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteAsync(org.Id, director.Id, Guid.NewGuid(), "any@email.com", default));
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_Returns409()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator, isActive: false);
        target.Email = "deleted-abc123@anonymised.local";
        await db.SaveChangesAsync();

        var (service, _) = CreateTestableService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(org.Id, director.Id, target.Id, "target@org.com", default));

        Assert.Contains("already been deleted", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_AuditEventRecorded()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, audit) = CreateTestableService(db);
        await service.DeleteAsync(org.Id, director.Id, target.Id, "target@org.com", default);

        var recorded = Assert.Single(audit.RecordedEvents);
        Assert.Equal(AuditEventTypes.UserDeleted, recorded.eventType);
        Assert.Equal(org.Id, recorded.organisationId);
        Assert.Equal(director.Id, recorded.actorUserId);
        Assert.Null(recorded.subjectUserId);

        Assert.NotNull(recorded.metadata);
        Assert.True(recorded.metadata.ContainsKey("target_user_snapshot"));
        Assert.Equal("target@org.com", recorded.metadata["target_email"]);
    }

    [Fact]
    public async Task SuspendAsync_CallsNotifyUserRemovedAsync()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, _) = CreateTestableService(db);
        await service.SuspendAsync(org.Id, director.Id, target.Id, null, default);

        Assert.Contains(target.Id, service.NotifiedRemovedUserIds);
    }

    [Fact]
    public async Task DeleteAsync_CallsNotifyUserRemovedAsync()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, "director@org.com", DateTime.UtcNow, role: UserRoles.Director);
        var target = SeedUser(db, org.Id, "target@org.com", DateTime.UtcNow, role: UserRoles.Coordinator);

        var (service, _) = CreateTestableService(db);
        await service.DeleteAsync(org.Id, director.Id, target.Id, "target@org.com", default);

        Assert.Contains(target.Id, service.NotifiedRemovedUserIds);
    }
}
