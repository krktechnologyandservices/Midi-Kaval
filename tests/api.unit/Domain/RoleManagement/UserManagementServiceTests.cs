using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Admin;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class UserManagementServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
    /// </summary>
    private sealed class TestableUserManagementService : UserManagementService
    {
        public TestableUserManagementService(AppDbContext db) : base(db) { }

        protected override IQueryable<User> ApplySearchFilter(IQueryable<User> query, string term)
        {
            var lowered = term.ToLowerInvariant();
            return query.Where(u =>
                u.Email.ToLower().Contains(lowered) ||
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(lowered));
        }
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

        var service = new UserManagementService(db);
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

        var service = new UserManagementService(db);
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

        var service = new UserManagementService(db);
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

        var service = new UserManagementService(db);
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

        var service = new UserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
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

        var service = new TestableUserManagementService(db);
        var result = await service.GetUserListAsync(
            org.Id, page: 1, pageSize: 25,
            searchTerm: "nonexistent");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }
}
