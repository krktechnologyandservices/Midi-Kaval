using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class ZeroDirectorTriggerServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Organisation SeedOrganisation(AppDbContext db, bool hasPendingRecovery = false)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            IsActive = true,
            HasPendingRecovery = hasPendingRecovery,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        db.SaveChanges();
        return org;
    }

    private static User SeedUser(
        AppDbContext db,
        Guid orgId,
        string role,
        bool isActive = true,
        bool isSuspended = false,
        string emailPrefix = "")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            Email = string.IsNullOrEmpty(emailPrefix)
                ? $"{role.ToLowerInvariant()}@example.org"
                : $"{emailPrefix}@example.org",
            FirstName = role,
            LastName = "User",
            Role = role,
            TokenVersion = 0,
            IsActive = isActive,
            IsSuspended = isSuspended,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static ZeroDirectorTriggerService CreateService(AppDbContext db)
    {
        var guard = new LastDirectorGuard(db);
        return new TestableZeroDirectorTriggerService(db, guard);
    }

    private sealed class TestableZeroDirectorTriggerService : ZeroDirectorTriggerService
    {
        public TestableZeroDirectorTriggerService(AppDbContext db, LastDirectorGuard guard)
            : base(db, guard, NullLogger<ZeroDirectorTriggerService>.Instance) { }

        protected override void EnqueueZeroDirectorAlert(Guid organisationId)
        {
            // No-op in test context — avoids Hangfire initialization
        }
    }

    [Fact]
    public async Task NotifyUserRemovedAsync_TriggersRecovery_WhenDirectorRemovedAndNoOthersRemain()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, UserRoles.Director);

        // Simulate the Director being deactivated (as happens before the trigger call in the real flow)
        var user = await db.Users.FirstAsync(u => u.Id == director.Id);
        user.IsSuspended = true;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.NotifyUserRemovedAsync(org.Id, director.Id, default);

        var savedOrg = await db.Organisations.FirstAsync(o => o.Id == org.Id);
        Assert.True(savedOrg.HasPendingRecovery);
    }

    [Fact]
    public async Task NotifyUserRemovedAsync_DoesNotTrigger_WhenOtherActiveDirectorsRemain()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var director = SeedUser(db, org.Id, UserRoles.Director, emailPrefix: "director1");
        SeedUser(db, org.Id, UserRoles.Director, emailPrefix: "director2");

        var service = CreateService(db);
        await service.NotifyUserRemovedAsync(org.Id, director.Id, default);

        var savedOrg = await db.Organisations.FirstAsync(o => o.Id == org.Id);
        Assert.False(savedOrg.HasPendingRecovery);
    }

    [Fact]
    public async Task NotifyUserRemovedAsync_DoesNotTrigger_ForNonDirectorUser()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, UserRoles.Director);
        var coordinator = SeedUser(db, org.Id, UserRoles.Coordinator);

        var service = CreateService(db);
        await service.NotifyUserRemovedAsync(org.Id, coordinator.Id, default);

        var savedOrg = await db.Organisations.FirstAsync(o => o.Id == org.Id);
        Assert.False(savedOrg.HasPendingRecovery);
    }

    [Fact]
    public async Task NotifyUserRemovedAsync_DoesNotThrow_WhenUserNotFound()
    {
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id, UserRoles.Director);

        var service = CreateService(db);
        var exception = await Record.ExceptionAsync(() =>
            service.NotifyUserRemovedAsync(org.Id, Guid.NewGuid(), default));

        Assert.Null(exception);
    }
}
