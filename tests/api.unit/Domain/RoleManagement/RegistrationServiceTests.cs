using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class RegistrationServiceTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    private static TokenService CreateTokenService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACTIVATION_LINK_SIGNING_KEY"] = SigningKey,
            })
            .Build());

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Fake password hasher that returns a known hash and verifies any password.
    /// Follows the same pattern as FakeEmailSender in the codebase.
    /// </summary>
    private sealed class FakePasswordHasher : IPasswordHasher<User>
    {
        public string HashPassword(User user, string password) =>
            $"HASHED:{password}";

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) =>
            hashedPassword == $"HASHED:{providedPassword}"
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
    }

    /// <summary>
    /// Testable RegistrationService that overrides the transactional activation
    /// to work with the EF Core InMemory provider (which does not support
    /// transactions or raw SQL).
    /// </summary>
    private sealed class TestableRegistrationService : RegistrationService
    {
        private readonly AppDbContext _db;

        public TestableRegistrationService(
            AppDbContext db,
            TokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            ILogger<RegistrationService> logger)
            : base(db, tokenService, passwordHasher, logger)
        {
            _db = db;
        }

        protected override async Task<ActivationResult> ExecuteActivationAsync(
            ActivationToken token, string fullName, string password, CancellationToken cancellationToken)
        {
            // Simulate atomic consumption via EF Core (InMemory-compatible)
            var dbToken = await _db.ActivationTokens.FindAsync(new object[] { token.Id }, cancellationToken);
            if (dbToken is null || dbToken.ConsumedAtUtc is not null)
            {
                throw new ValidationException("This link has expired or already been used. Please contact the Vendor to request a new activation link.");
            }
            dbToken.ConsumedAtUtc = DateTime.UtcNow;

            // Split full name
            var (firstName, lastName) = SplitFullName(fullName);

            // Create Director user
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = token.OrganisationId,
                Email = token.TargetEmail,
                FirstName = firstName,
                LastName = lastName,
                Role = UserRoles.Director,
                TokenVersion = 0,
                IsActive = true,
                IsSuspended = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            user.PasswordHash = PasswordHasherHelper.HashPassword(user, password);
            _db.Users.Add(user);

            // Activate organisation
            var org = await _db.Organisations.FindAsync(new object[] { token.OrganisationId }, cancellationToken);
            if (org is not null)
            {
                org.IsActive = true;
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new ActivationResult(user.Id, token.OrganisationId, token.Organisation.Name);
        }

        private static class PasswordHasherHelper
        {
            public static string HashPassword(User user, string password) => $"HASHED:{password}";
        }
    }

    private static RegistrationService CreateService(AppDbContext db)
    {
        var tokenService = CreateTokenService();
        var passwordHasher = new FakePasswordHasher();
        var logger = NullLogger<RegistrationService>.Instance;
        return new TestableRegistrationService(db, tokenService, passwordHasher, logger);
    }

    private static (string rawToken, string tokenHash, string signature) GenerateTestToken(TokenService service)
    {
        return service.GenerateActivationToken();
    }

    private static Organisation SeedOrganisation(AppDbContext db, bool isActive = false)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Organisation",
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        db.SaveChanges();
        return org;
    }

    private static ActivationToken SeedActivationToken(
        AppDbContext db,
        Guid organisationId,
        string tokenHash,
        string targetEmail,
        DateTime? expiresAtUtc = null,
        DateTime? consumedAtUtc = null)
    {
        var token = new ActivationToken
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            TokenHash = tokenHash,
            TargetEmail = targetEmail,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(7),
            ConsumedAtUtc = consumedAtUtc,
            DeliveryAttempts = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.ActivationTokens.Add(token);
        db.SaveChanges();
        return token;
    }

    public class ValidateLinkAsync
    {
        [Fact]
        public async Task ValidLink_ReturnsEmailAndOrgName()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);
            var result = await service.ValidateLinkAsync(rawToken, signature);

            Assert.True(result.IsValid);
            Assert.Equal("director@example.org", result.Email);
            Assert.Equal("Test Organisation", result.OrganisationName);
        }

        [Fact]
        public async Task InvalidSignature_ReturnsInvalid()
        {
            var tokenService = CreateTokenService();
            var (_, tokenHash, _) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);
            var result = await service.ValidateLinkAsync(rawToken: "tampered-token", signature: "tampered-sig");

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }

        [Fact]
        public async Task NonexistentHash_ReturnsInvalid()
        {
            var tokenService = CreateTokenService();
            var (rawToken, _, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();

            var service = CreateService(db);
            var result = await service.ValidateLinkAsync(rawToken, signature);

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }

        [Fact]
        public async Task ExpiredToken_ReturnsInvalid()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org",
                expiresAtUtc: DateTime.UtcNow.AddDays(-1));

            var service = CreateService(db);
            var result = await service.ValidateLinkAsync(rawToken, signature);

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }

        [Fact]
        public async Task ConsumedToken_ReturnsInvalid()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org",
                consumedAtUtc: DateTime.UtcNow);

            var service = CreateService(db);
            var result = await service.ValidateLinkAsync(rawToken, signature);

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }
    }

    public class ActivateOrganisationAsync
    {
        [Fact]
        public async Task ValidRequest_CreatesUserAndActivatesOrg()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var seededToken = SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);
            var result = await service.ActivateOrganisationAsync(
                rawToken, signature, "Jane Smith", "Password1");

            Assert.NotEqual(Guid.Empty, result.UserId);
            Assert.Equal(org.Id, result.OrganisationId);
            Assert.Equal("Test Organisation", result.OrganisationName);

            // Verify org is active
            var updatedOrg = await db.Organisations.FindAsync(org.Id);
            Assert.NotNull(updatedOrg);
            Assert.True(updatedOrg.IsActive);

            // Verify user created
            var user = await db.Users.FindAsync(result.UserId);
            Assert.NotNull(user);
            Assert.Equal("director@example.org", user.Email);
            Assert.Equal("Jane", user.FirstName);
            Assert.Equal("Smith", user.LastName);
            Assert.Equal(UserRoles.Director, user.Role);
            Assert.True(user.IsActive);
            Assert.False(user.IsSuspended);

            // Verify token consumed
            var consumedToken = await db.ActivationTokens.FindAsync(seededToken.Id);
            Assert.NotNull(consumedToken);
            Assert.NotNull(consumedToken.ConsumedAtUtc);
        }

        [Fact]
        public async Task InvalidSignature_ThrowsValidationException()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync("tampered-token", "tampered-sig", "Jane Smith", "Password1"));

            Assert.Contains("Invalid activation link", ex.Message);
        }

        [Fact]
        public async Task ExpiredToken_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org",
                expiresAtUtc: DateTime.UtcNow.AddDays(-1));

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "Password1"));

            Assert.Contains("has expired", ex.Message);
        }

        [Fact]
        public async Task AlreadyConsumedToken_ThrowsConflictException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org",
                consumedAtUtc: DateTime.UtcNow);

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ActivationConflictException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "Password1"));

            Assert.Contains("already been used", ex.Message);
        }

        [Fact]
        public async Task ConcurrentConsumption_SecondRequestFails()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);

            // First request succeeds
            var result1 = await service.ActivateOrganisationAsync(
                rawToken, signature, "Jane Smith", "Password1");

            Assert.NotEqual(Guid.Empty, result1.UserId);

            // Second request should fail (token consumed)
            var ex = await Assert.ThrowsAsync<ActivationConflictException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "John Doe", "Password2"));

            Assert.Contains("already been used", ex.Message);
        }

        [Fact]
        public async Task ShortPassword_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "Ab1"));

            Assert.Contains("at least 8 characters", ex.Message);
        }

        [Fact]
        public async Task PasswordMissingUppercase_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "password1"));

            Assert.Contains("uppercase", ex.Message);
        }

        [Fact]
        public async Task PasswordMissingLowercase_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "PASSWORD1"));

            Assert.Contains("lowercase", ex.Message);
        }

        [Fact]
        public async Task PasswordMissingDigit_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ActivateOrganisationAsync(rawToken, signature, "Jane Smith", "PasswordA"));

            Assert.Contains("digit", ex.Message);
        }

        [Fact]
        public async Task SingleWordName_SetsFirstNameAndEmptyLastName()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);
            var result = await service.ActivateOrganisationAsync(
                rawToken, signature, "Prince", "Password1");

            var user = await db.Users.FindAsync(result.UserId);
            Assert.NotNull(user);
            Assert.Equal("Prince", user.FirstName);
            Assert.Equal(string.Empty, user.LastName);
        }

        [Fact]
        public async Task SetsOrgIsActiveOnSuccess()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: false);
            SeedActivationToken(db, org.Id, tokenHash, "director@example.org");

            var service = CreateService(db);
            var result = await service.ActivateOrganisationAsync(
                rawToken, signature, "Jane Smith", "Password1");

            var updatedOrg = await db.Organisations.FindAsync(org.Id);
            Assert.NotNull(updatedOrg);
            Assert.True(updatedOrg.IsActive);
        }
    }
}
