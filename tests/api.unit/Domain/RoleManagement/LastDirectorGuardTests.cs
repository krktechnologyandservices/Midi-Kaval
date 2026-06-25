using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class LastDirectorGuardTests
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

    private static User SeedUser(AppDbContext db, Guid orgId, string role, bool isActive = true, bool isSuspended = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            Email = $"{role.ToLowerInvariant()}@example.org",
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

    public class IsLastActiveDirectorAsync
    {
        [Fact]
        public async Task ReturnsTrue_WhenUserIsOnlyActiveDirector()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director);

            var guard = new LastDirectorGuard(db);
            var result = await guard.IsLastActiveDirectorAsync(org.Id, director.Id, default);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsFalse_WhenOtherActiveDirectorsExist()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director1 = SeedUser(db, org.Id, UserRoles.Director);
            SeedUser(db, org.Id, UserRoles.Director);

            var guard = new LastDirectorGuard(db);
            var result = await guard.IsLastActiveDirectorAsync(org.Id, director1.Id, default);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalse_WhenTargetUserNotInOrg()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedUser(db, org.Id, UserRoles.Director);
            var otherOrg = SeedOrganisation(db);
            var otherDirector = SeedUser(db, otherOrg.Id, UserRoles.Director);

            var guard = new LastDirectorGuard(db);
            var result = await guard.IsLastActiveDirectorAsync(org.Id, otherDirector.Id, default);

            Assert.False(result);
        }
    }

    public class HasAnyActiveDirectorAsync
    {
        [Fact]
        public async Task ReturnsTrue_WhenActiveDirectorExists()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedUser(db, org.Id, UserRoles.Director);

            var guard = new LastDirectorGuard(db);
            var result = await guard.HasAnyActiveDirectorAsync(org.Id, default);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsFalse_WhenZeroActiveDirectors()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);

            var guard = new LastDirectorGuard(db);
            var result = await guard.HasAnyActiveDirectorAsync(org.Id, default);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalse_WhenOnlySuspendedDirectors()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedUser(db, org.Id, UserRoles.Director, isActive: true, isSuspended: true);

            var guard = new LastDirectorGuard(db);
            var result = await guard.HasAnyActiveDirectorAsync(org.Id, default);

            Assert.False(result);
        }

        [Fact]
        public async Task ReturnsFalse_WhenOnlyInactiveDirectors()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedUser(db, org.Id, UserRoles.Director, isActive: false);

            var guard = new LastDirectorGuard(db);
            var result = await guard.HasAnyActiveDirectorAsync(org.Id, default);

            Assert.False(result);
        }
    }

    public class GetLastKnownDirectorInfoAsync
    {
        [Fact]
        public async Task ReturnsNull_WhenOrgNeverHadDirector()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);

            var guard = new LastDirectorGuard(db);
            var result = await guard.GetLastKnownDirectorInfoAsync(org.Id, default);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsInfo_FromMostRecentDirectorAuditEvent()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);

            // Seed an audit event for a Director user being created
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = org.Id,
                EventType = "user_created",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                ActorUserId = null,
            });
            db.SaveChanges();

            var guard = new LastDirectorGuard(db);
            var result = await guard.GetLastKnownDirectorInfoAsync(org.Id, default);

            // Without a subject/actor user, info may be null but non-null result means we found an event
            Assert.NotNull(result);
            Assert.NotNull(result.LastActiveAt);
        }
    }
}
