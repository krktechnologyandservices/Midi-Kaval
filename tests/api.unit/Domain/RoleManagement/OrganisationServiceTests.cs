using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using StackExchange.Redis;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class OrganisationServiceTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static TokenService CreateTokenService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACTIVATION_LINK_SIGNING_KEY"] = SigningKey,
            })
            .Build());

    private static TestableOrganisationService CreateService(AppDbContext db)
    {
        var tokenService = CreateTokenService();
        var emailSender = new FakeEmailSender();
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<OrganisationService>.Instance;
        var lastDirectorGuard = new LastDirectorGuard(db);
        return new TestableOrganisationService(db, tokenService, emailSender, config, lastDirectorGuard, logger);
    }

    /// <summary>
    /// Testable OrganisationService that overrides the Redis-backed email rate limit check
    /// to work without a Redis connection in unit tests.
    /// </summary>
    private sealed class TestableOrganisationService : OrganisationService
    {
        public TestableOrganisationService(
            AppDbContext db,
            TokenService tokenService,
            IEmailSender emailSender,
            IConfiguration configuration,
            LastDirectorGuard lastDirectorGuard,
            ILogger<OrganisationService> logger)
            : base(db, tokenService, emailSender, null!, configuration, lastDirectorGuard, logger)
        {
        }

        protected override Task<bool> IsEmailRateLimitedAsync(string email) =>
            Task.FromResult(false);
    }

    private static Organisation SeedOrganisation(AppDbContext db, bool isActive = false, bool hasPendingRecovery = false)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            IsActive = isActive,
            HasPendingRecovery = hasPendingRecovery,
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

    public class ReissueActivationAsync
    {
        [Fact]
        public async Task Succeeds_ForOrgInZeroDirectorState()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true, hasPendingRecovery: true);
            var service = CreateService(db);

            var result = await service.ReissueActivationAsync(org.Id, "director@example.org", default);

            Assert.Equal(org.Id, result.OrganisationId);
            Assert.Equal("Test Org", result.Name);
            Assert.Equal("sent", result.Status);

            var token = await db.ActivationTokens.FirstAsync(t => t.OrganisationId == org.Id);
            Assert.NotNull(token);
            Assert.Equal("director@example.org", token.TargetEmail);
        }

        [Fact]
        public async Task Throws_WhenOrgHasActiveDirectors()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedUser(db, org.Id, UserRoles.Director);
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ReissueActivationAsync(org.Id, "director@example.org", default));

            Assert.Contains("already has active Directors", ex.Message);
        }

        [Fact]
        public async Task Throws_WhenOrgNotFound()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ReissueActivationAsync(Guid.NewGuid(), "director@example.org", default));

            Assert.Contains("Organisation not found", ex.Message);
        }

        [Fact]
        public async Task Throws_WhenEmailIsInvalid()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true, hasPendingRecovery: true);
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ReissueActivationAsync(org.Id, "not-an-email", default));

            Assert.Contains("valid email", ex.Message);
        }

        [Fact]
        public async Task StoresToken_WithCorrectExpiry()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true, hasPendingRecovery: true);
            var service = CreateService(db);

            var result = await service.ReissueActivationAsync(org.Id, "director@example.org", default);

            var token = await db.ActivationTokens.FirstAsync(t => t.OrganisationId == org.Id);
            Assert.True(token.ExpiresAtUtc > DateTime.UtcNow.AddDays(6));
            Assert.True(token.ExpiresAtUtc < DateTime.UtcNow.AddDays(8));
        }
    }

    public class GetOrganisationListAsync
    {
        [Fact]
        public async Task ReturnsAllOrgsOrderedByCreatedDesc()
        {
            using var db = CreateContext();
            var org1 = SeedOrganisation(db, isActive: true);
            await Task.Delay(10); // Ensure different timestamps
            var org2 = SeedOrganisation(db, isActive: false);
            var service = CreateService(db);

            var orgs = await service.GetOrganisationListAsync(default);

            Assert.Equal(2, orgs.Count);
            Assert.Equal(org2.Id, orgs[0].Id); // Most recent first
            Assert.Equal(org1.Id, orgs[1].Id);
        }

        [Fact]
        public async Task IncludesDirectorCount()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedUser(db, org.Id, UserRoles.Director);
            SeedUser(db, org.Id, UserRoles.Director);
            SeedUser(db, org.Id, UserRoles.Coordinator); // Should not count
            var service = CreateService(db);

            var orgs = await service.GetOrganisationListAsync(default);

            var result = Assert.Single(orgs);
            Assert.Equal(2, result.DirectorCount);
        }
    }

    public class GetOrganisationDetailAsync
    {
        [Fact]
        public async Task ReturnsNull_WhenOrgNotFound()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var result = await service.GetOrganisationDetailAsync(Guid.NewGuid(), default);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsDetail_WhenOrgExists()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedUser(db, org.Id, UserRoles.Director);
            var service = CreateService(db);

            var result = await service.GetOrganisationDetailAsync(org.Id, default);

            Assert.NotNull(result);
            Assert.Equal(org.Id, result.Id);
            Assert.Equal("Test Org", result.Name);
            Assert.True(result.IsActive);
            Assert.Equal(1, result.DirectorCount);
        }
    }
}
