using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Models.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
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

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfirmationLink:BaseUrl"] = "http://localhost:4200",
                ["ConfirmationLink:TokenTtlHours"] = "24",
            })
            .Build();
    }

    /// <summary>
    /// Fake password hasher that returns a known hash and verifies any password.
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
    /// Fake audit service that records events in memory for assertions.
    /// </summary>
    private sealed class FakeAuditService : IAuditService
    {
        public ConcurrentBag<(string EventType, Guid OrgId, Guid? ActorId, Guid? SubjectId, TargetUserSnapshotDto? Snapshot, string? IpAddress)> Events { get; } = [];

        public Task RecordAsync(
            string eventType,
            Guid organisationId,
            Guid? actorUserId = null,
            Guid? subjectUserId = null,
            TargetUserSnapshotDto? targetUserSnapshot = null,
            string? actorIpAddress = null,
            IReadOnlyDictionary<string, object?>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            Events.Add((eventType, organisationId, actorUserId, subjectUserId, targetUserSnapshot, actorIpAddress));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Testable RegistrationService that overrides the transactional activation/accept/confirm
    /// to work with the EF Core InMemory provider (which does not support transactions or raw SQL).
    /// </summary>
    private sealed class TestableRegistrationService : RegistrationService
    {
        private readonly AppDbContext _db;
        private readonly FakeAuditService _auditService;

        public TestableRegistrationService(
            AppDbContext db,
            TokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            FakeAuditService auditService,
            ILogger<RegistrationService> logger)
            : base(db, tokenService, passwordHasher, auditService, configuration, logger)
        {
            _db = db;
            _auditService = auditService;
        }

        public IReadOnlyCollection<(string EventType, Guid OrgId, Guid? ActorId, Guid? SubjectId, TargetUserSnapshotDto? Snapshot, string? IpAddress)> AuditEvents =>
            _auditService.Events.ToList().AsReadOnly();

        protected override async Task<ActivationResult> ExecuteActivationAsync(
            ActivationToken token, string fullName, string password, string? actorIpAddress = null, CancellationToken cancellationToken = default)
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

            // Write audit event
            var snapshot = new TargetUserSnapshotDto(user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.Role);
            await _auditService.RecordAsync(
                AuditEventTypes.OrganisationActivated,
                user.OrganisationId,
                actorUserId: user.Id,
                subjectUserId: user.Id,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["organisation_name"] = token.Organisation.Name,
                    ["activation_token_id"] = token.Id.ToString(),
                },
                cancellationToken: cancellationToken);

            return new ActivationResult(user.Id, token.OrganisationId, token.Organisation.Name);
        }

        /// <summary>Bypass Hangfire enqueue in tests.</summary>
        protected override Task SendConfirmationEmailAsync(
            Guid confirmationTokenId, string rawToken, string signature,
            string targetEmail, string userName, Guid userId, Guid orgId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>Bypass atomic SQL — use EF Core FindAsync for InMemory compatibility.</summary>
        protected override async Task ConsumeInvitationTokenAtomicallyAsync(Guid invitationId, CancellationToken cancellationToken)
        {
            var invitation = await _db.Invitations.FindAsync(new object[] { invitationId }, cancellationToken);
            if (invitation is null || invitation.Status != InvitationStatus.Pending)
            {
                throw new RegistrationConflictException("This invitation has already been used.");
            }
            invitation.Status = InvitationStatus.Confirmed;
            invitation.ConfirmedAtUtc = DateTime.UtcNow;
        }

        /// <summary>Bypass atomic SQL — use EF Core FindAsync for InMemory compatibility.</summary>
        protected override async Task ConsumeConfirmationTokenAtomicallyAsync(Guid tokenId, CancellationToken cancellationToken)
        {
            var ct = await _db.ConfirmationTokens.FindAsync(new object[] { tokenId }, cancellationToken);
            if (ct is null || ct.ConsumedAtUtc is not null)
            {
                throw new RegistrationConflictException("This confirmation link has already been used.");
            }
            ct.ConsumedAtUtc = DateTime.UtcNow;
        }

        /// <summary>Bypass transactions — InMemory provider does not support them.</summary>
        protected override async Task<AcceptInvitationResult> ExecuteAcceptInvitationAsync(
            Invitation invitation, string fullName, string password, string? actorIpAddress = null, CancellationToken cancellationToken = default)
        {
            // Simulate atomic token consumption
            await ConsumeInvitationTokenAtomicallyAsync(invitation.Id, cancellationToken);

            var (firstName, lastName) = SplitFullName(fullName);

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = invitation.OrganisationId,
                Email = invitation.TargetEmail,
                FirstName = firstName,
                LastName = lastName,
                Role = invitation.Role,
                TokenVersion = 1,
                IsActive = false,
                IsSuspended = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            user.PasswordHash = new FakePasswordHasher().HashPassword(user, password);
            _db.Users.Add(user);

            invitation.Status = InvitationStatus.Confirmed;
            invitation.ConfirmedAtUtc = now;

            var rawBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = Convert.ToHexString(rawBytes).ToLowerInvariant();
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
            var ttlHours = 24;

            var ct = new ConfirmationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                InvitationId = invitation.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = now.AddHours(ttlHours),
                CreatedAtUtc = now,
            };
            _db.ConfirmationTokens.Add(ct);

            await _db.SaveChangesAsync(cancellationToken);

            await _auditService.RecordAsync(
                AuditEventTypes.AccountCreated,
                user.OrganisationId,
                subjectUserId: user.Id,
                metadata: new Dictionary<string, object?>
                {
                    ["email"] = user.Email,
                    ["role"] = user.Role,
                    ["invitationId"] = invitation.Id,
                },
                cancellationToken: cancellationToken);

            return new AcceptInvitationResult(user.Email, invitation.Organisation.Name);
        }

        /// <summary>Bypass transactions — InMemory provider does not support them.</summary>
        protected override async Task<ConfirmEmailResult> ExecuteConfirmEmailAsync(
            ConfirmationToken confirmationToken, string? actorIpAddress = null, CancellationToken cancellationToken = default)
        {
            var user = confirmationToken.User;
            user.IsActive = true;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await ConsumeConfirmationTokenAtomicallyAsync(confirmationToken.Id, cancellationToken);

            if (confirmationToken.Invitation is not null)
            {
                confirmationToken.Invitation.ConfirmedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _auditService.RecordAsync(
                AuditEventTypes.EmailConfirmed,
                user.OrganisationId,
                subjectUserId: user.Id,
                metadata: new Dictionary<string, object?>
                {
                    ["confirmationTokenId"] = confirmationToken.Id,
                    ["email"] = user.Email,
                },
                cancellationToken: cancellationToken);

            return new ConfirmEmailResult(user.Email);
        }

        private static class PasswordHasherHelper
        {
            public static string HashPassword(User user, string password) => $"HASHED:{password}";
        }
    }

    private static TestableRegistrationService CreateService(AppDbContext db)
    {
        var tokenService = CreateTokenService();
        var passwordHasher = new FakePasswordHasher();
        var logger = NullLogger<RegistrationService>.Instance;
        var auditService = new FakeAuditService();
        var configuration = CreateConfiguration();
        return new TestableRegistrationService(db, tokenService, passwordHasher, configuration, auditService, logger);
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

    private static Invitation SeedInvitation(
        AppDbContext db,
        Guid organisationId,
        string tokenHash,
        string targetEmail,
        string role,
        string status = InvitationStatus.Pending,
        DateTime? expiresAtUtc = null,
        Guid? invitedByUserId = null)
    {
        var user = new User
        {
            Id = invitedByUserId ?? Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = "director@example.org",
            FirstName = "Test",
            LastName = "Director",
            Role = UserRoles.Director,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        if (invitedByUserId is null)
        {
            db.Users.Add(user);
        }
        else
        {
            // Ensure the referenced user exists in the DB context
            var existing = db.Users.Local.FirstOrDefault(u => u.Id == invitedByUserId.Value);
            if (existing is null)
            {
                db.Users.Add(user);
            }
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            InvitedByUserId = user.Id,
            TargetEmail = targetEmail,
            Role = role,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(7),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Invitations.Add(invitation);
        db.SaveChanges();
        return invitation;
    }

    // ============= Existing Tests (ValidateLinkAsync, ActivateOrganisationAsync) =============

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

    // ============= New Tests: AcceptInvitationAsync =============

    public class AcceptInvitationAsync
    {
        [Fact]
        public async Task ValidRequest_CreatesUserInPendingConfirmation()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker");

            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            var result = await service.AcceptInvitationAsync(rawToken, signature, "Jane Worker", "Password1");

            Assert.Equal("worker@example.org", result.Email);
            Assert.Equal("Test Organisation", result.OrganisationName);

            // Verify user created in pending state
            var user = db.Users.FirstOrDefault(u => u.Email == "worker@example.org");
            Assert.NotNull(user);
            Assert.Equal("Jane", user.FirstName);
            Assert.Equal("Worker", user.LastName);
            Assert.Equal("SocialWorker", user.Role);
            Assert.False(user.IsActive); // Pending confirmation
            Assert.False(user.IsSuspended);
            Assert.Equal(1, user.TokenVersion);

            // Verify confirmation token created
            var ct = db.ConfirmationTokens.FirstOrDefault(t => t.UserId == user.Id);
            Assert.NotNull(ct);
            Assert.Equal(user.Id, ct.UserId);
            Assert.NotNull(ct.ExpiresAtUtc);
            Assert.Null(ct.ConsumedAtUtc);
            Assert.Equal(0, ct.DeliveryAttempts);

            // Verify invitation consumed
            var inv = db.Invitations.First();
            Assert.Equal(InvitationStatus.Confirmed, inv.Status);
            Assert.NotNull(inv.ConfirmedAtUtc);
        }

        [Fact]
        public async Task InvalidSignature_ThrowsValidationException()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.AcceptInvitationAsync("tampered-token", "tampered-sig", "Jane Worker", "Password1"));

            Assert.Contains("Invalid invitation link", ex.Message);
        }

        [Fact]
        public async Task ExpiredToken_ThrowsConflictException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker",
                expiresAtUtc: DateTime.UtcNow.AddDays(-1));

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<RegistrationConflictException>(() =>
                service.AcceptInvitationAsync(rawToken, signature, "Jane Worker", "Password1"));

            Assert.Contains("expired", ex.Message);
        }

        [Fact]
        public async Task AlreadyConfirmedInvitation_ThrowsConflictException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker",
                status: InvitationStatus.Confirmed);

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<RegistrationConflictException>(() =>
                service.AcceptInvitationAsync(rawToken, signature, "Jane Worker", "Password1"));

            Assert.Contains("already been used", ex.Message);
        }

        [Fact]
        public async Task WeakPassword_ThrowsValidationException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker");

            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.AcceptInvitationAsync(rawToken, signature, "Jane Worker", "weak"));

            Assert.Contains("at least 8 characters", ex.Message);
        }

        [Fact]
        public async Task ValidRequest_WritesAuditEvent()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker");

            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            await service.AcceptInvitationAsync(rawToken, signature, "Jane Worker", "Password1");

            var user = db.Users.First(u => u.Email == "worker@example.org");
            var accountCreatedEvents = service.AuditEvents
                .Where(e => e.EventType == AuditEventTypes.AccountCreated)
                .ToList();

            Assert.Single(accountCreatedEvents);
            Assert.Equal(user.OrganisationId, accountCreatedEvents[0].OrgId);
            Assert.Equal(user.Id, accountCreatedEvents[0].SubjectId);
        }
    }

    // ============= New Tests: ConfirmEmailAsync =============

    public class ConfirmEmailAsync
    {
        private static async Task<(string rawToken, string signature, TestableRegistrationService service, AppDbContext db, User user, ConfirmationToken ct)>
            SetupValidConfirmation(CancellationToken cancellationToken = default)
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);

            // Create user in pending state
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = org.Id,
                Email = "worker@example.org",
                FirstName = "Jane",
                LastName = "Worker",
                Role = "SocialWorker",
                TokenVersion = 1,
                IsActive = false,
                IsSuspended = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            user.PasswordHash = new FakePasswordHasher().HashPassword(user, "Password1");
            db.Users.Add(user);

            SeedInvitation(db, org.Id, "some-hash", "worker@example.org", "SocialWorker");

            var ct = new ConfirmationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                InvitationId = db.Invitations.First().Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = now.AddDays(1),
                DeliveryAttempts = 0,
                CreatedAtUtc = now,
            };
            db.ConfirmationTokens.Add(ct);
            await db.SaveChangesAsync(cancellationToken);

            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            return (rawToken, signature, service, db, user, ct);
        }

        [Fact]
        public async Task ValidToken_ActivatesUser()
        {
            var (rawToken, signature, service, db, user, ct) = await SetupValidConfirmation();

            var result = await service.ConfirmEmailAsync(rawToken, signature);

            Assert.Equal("worker@example.org", result.Email);

            // Verify user activated
            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.True(updatedUser.IsActive);

            // Verify token consumed
            var updatedCt = await db.ConfirmationTokens.FindAsync(ct.Id);
            Assert.NotNull(updatedCt);
            Assert.NotNull(updatedCt.ConsumedAtUtc);

            // Verify audit event
            var emailConfirmedEvents = service.AuditEvents
                .Where(e => e.EventType == AuditEventTypes.EmailConfirmed)
                .ToList();
            Assert.Single(emailConfirmedEvents);
        }

        [Fact]
        public async Task InvalidSignature_ThrowsValidationException()
        {
            using var db = CreateContext();
            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                service.ConfirmEmailAsync("tampered-token", "tampered-sig"));

            Assert.Contains("Invalid confirmation link", ex.Message);
        }

        [Fact]
        public async Task ExpiredToken_ThrowsConflictException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = org.Id,
                Email = "worker@example.org",
                FirstName = "Jane",
                LastName = "Worker",
                Role = "SocialWorker",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            db.Users.Add(user);

            var ct = new ConfirmationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.ConfirmationTokens.Add(ct);
            await db.SaveChangesAsync();

            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            var ex = await Assert.ThrowsAsync<RegistrationConflictException>(() =>
                service.ConfirmEmailAsync(rawToken, signature));

            Assert.Contains("expired", ex.Message);
        }

        [Fact]
        public async Task AlreadyConsumedToken_ThrowsConflictException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = org.Id,
                Email = "worker@example.org",
                FirstName = "Jane",
                LastName = "Worker",
                Role = "SocialWorker",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            db.Users.Add(user);

            var ct = new ConfirmationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                ConsumedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.ConfirmationTokens.Add(ct);
            await db.SaveChangesAsync();

            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            var ex = await Assert.ThrowsAsync<RegistrationConflictException>(() =>
                service.ConfirmEmailAsync(rawToken, signature));

            Assert.Contains("already been used", ex.Message);
        }

        [Fact]
        public async Task NonexistentToken_ThrowsKeyNotFoundException()
        {
            var tokenService = CreateTokenService();
            var (rawToken, _, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var wrapper = CreateService(db);
            var service = (TestableRegistrationService)wrapper;

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ConfirmEmailAsync(rawToken, signature));
        }
    }

    // ============= New Tests: ValidateInvitationLinkAsync =============

    public class ValidateInvitationLinkAsync
    {
        [Fact]
        public async Task ValidLink_ReturnsEmailOrgRole()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker");

            var service = CreateService(db);
            var result = await service.ValidateInvitationLinkAsync(rawToken, signature);

            Assert.True(result.IsValid);
            Assert.Equal("worker@example.org", result.Email);
            Assert.Equal("Test Organisation", result.OrganisationName);
            Assert.Equal("SocialWorker", result.Role);
        }

        [Fact]
        public async Task InvalidSignature_ReturnsInvalid()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            var result = await service.ValidateInvitationLinkAsync("tampered", "tampered");

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }

        [Fact]
        public async Task ExpiredLink_ReturnsInvalid()
        {
            var tokenService = CreateTokenService();
            var (rawToken, tokenHash, signature) = GenerateTestToken(tokenService);

            using var db = CreateContext();
            var org = SeedOrganisation(db, isActive: true);
            SeedInvitation(db, org.Id, tokenHash, "worker@example.org", "SocialWorker",
                expiresAtUtc: DateTime.UtcNow.AddDays(-1));

            var service = CreateService(db);
            var result = await service.ValidateInvitationLinkAsync(rawToken, signature);

            Assert.False(result.IsValid);
            Assert.Null(result.Email);
        }
    }

    // ============= Tests: ValidateFullName =============

    public class ValidateFullName
    {
        [Fact]
        public void ValidName_ReturnsValid()
        {
            var (valid, error) = RegistrationService.ValidateFullName("Jane Smith");
            Assert.True(valid);
            Assert.Empty(error);
        }

        [Fact]
        public void EmptyName_ReturnsInvalid()
        {
            var (valid, error) = RegistrationService.ValidateFullName("");
            Assert.False(valid);
            Assert.Contains("required", error);
        }

        [Fact]
        public void SingleCharName_ReturnsInvalid()
        {
            var (valid, error) = RegistrationService.ValidateFullName("A");
            Assert.False(valid);
            Assert.Contains("at least 2", error);
        }
    }
}
