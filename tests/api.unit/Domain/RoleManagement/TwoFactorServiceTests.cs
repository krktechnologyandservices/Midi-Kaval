using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Audit;
using OtpNet;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class TwoFactorServiceTests
{
    private static readonly TotpOptions DefaultOptions = new()
    {
        Issuer = "Midi-Kaval",
        StepSeconds = 30,
        CodeLength = 6,
    };

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
        string? totpSecret = null,
        DateTime? totpEnrolledAt = null,
        string email = "")
    {
        if (string.IsNullOrEmpty(email))
        {
            email = totpEnrolledAt is not null ? "enrolled@test.com" : "unenrolled@test.com";
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            IsSuspended = false,
            TotpSecret = totpSecret,
            TotpEnrolledAt = totpEnrolledAt,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    /// <summary>
    /// Testable subclass that overrides the audit service dependency so we can
    /// verify audit events without setting up the real audit infrastructure.
    /// The core TOTP logic is left untouched (we test it directly).
    /// </summary>
    private sealed class TestableTwoFactorService : TwoFactorService
    {
        public List<(string EventType, Guid OrgId, Guid? ActorId, Guid? SubjectId, TargetUserSnapshotDto? Snapshot, string? IpAddress)> AuditEvents { get; } = [];

        public TestableTwoFactorService(
            AppDbContext db,
            IOptions<TotpOptions> options,
            ILogger<TwoFactorService> logger)
            : base(db, options, new CollectingAuditService(), logger)
        {
        }

        /// <summary>Expose the protected EnrollCoreAsync for testing.</summary>
        public Task<TotpEnrollmentResult> CallEnrollCoreAsync(User user, string code, string? actorIpAddress = null, CancellationToken ct = default)
            => EnrollCoreAsync(user, code, actorIpAddress, ct);
    }

    /// <summary>Stub audit service that collects events in-memory.</summary>
    private sealed class CollectingAuditService : IAuditService
    {
        public List<(string EventType, Guid OrgId, Guid? ActorId, Guid? SubjectId, TargetUserSnapshotDto? Snapshot, string? IpAddress)> Events { get; } = [];

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

    [Fact]
    public async Task GenerateProvisioningAsync_ReturnsValidProvisioningUri()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var user = SeedUser(db, org.Id);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.GenerateProvisioningAsync(user.Id, ct: CancellationToken.None);

        // Assert — URI matches expected otpauth:// format
        Assert.StartsWith("otpauth://totp/", result.ProvisioningUri);
        Assert.Contains("Midi-Kaval", result.ProvisioningUri);
        // Email in URI is URL-encoded (%40 instead of @)
        Assert.Contains(Uri.EscapeDataString(user.Email), result.ProvisioningUri);

        // Secret is valid base32
        Assert.NotNull(result.SecretBase32);
        var bytes = Base32Encoding.ToBytes(result.SecretBase32);
        Assert.Equal(20, bytes.Length);

        // Secret was persisted
        var savedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(result.SecretBase32, savedUser.TotpSecret);
    }

    [Fact]
    public async Task GenerateProvisioningAsync_ThrowsForUnknownUser()
    {
        // Arrange
        using var db = CreateContext();
        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GenerateProvisioningAsync(Guid.NewGuid(), ct: CancellationToken.None));
    }

    [Fact]
    public async Task EnrollAsync_ValidCode_SetsEnrolledAt()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        // Generate a known secret
        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret);

        // Compute a valid TOTP code using the same secret + options
        var totp = new Totp(secret, step: DefaultOptions.StepSeconds, totpSize: DefaultOptions.CodeLength);
        var validCode = totp.ComputeTotp(DateTime.UtcNow);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.EnrollAsync(user.Id, validCode, ct: CancellationToken.None);
    
        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        var savedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.NotNull(savedUser.TotpEnrolledAt);
        Assert.True((DateTime.UtcNow - savedUser.TotpEnrolledAt!.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task EnrollAsync_InvalidCode_ReturnsError()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.EnrollAsync(user.Id, "000000", ct: CancellationToken.None);
    
        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Invalid", result.ErrorMessage);

        var savedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(savedUser.TotpEnrolledAt);
    }

    [Fact]
    public async Task EnrollAsync_NoSecret_ReturnsError()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var user = SeedUser(db, org.Id, totpSecret: null); // No secret generated

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.EnrollAsync(user.Id, "123456", ct: CancellationToken.None);
    
        // Assert
        Assert.False(result.Success);
        Assert.Contains("No TOTP secret found", result.ErrorMessage);
    }

    [Fact]
    public async Task EnrollAsync_AlreadyEnrolled_WithValidCode_Succeeds()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow.AddDays(-1));

        var totp = new Totp(secret, step: DefaultOptions.StepSeconds, totpSize: DefaultOptions.CodeLength);
        var validCode = totp.ComputeTotp(DateTime.UtcNow);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act — re-enrollment with valid code should still succeed
        var result = await service.EnrollAsync(user.Id, validCode, ct: CancellationToken.None);
    
        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_ValidCode_ReturnsTrue()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow);

        var totp = new Totp(secret, step: DefaultOptions.StepSeconds, totpSize: DefaultOptions.CodeLength);
        var validCode = totp.ComputeTotp(DateTime.UtcNow);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.VerifyTotpCodeAsync(user.Id, validCode, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_InvalidCode_ReturnsFalse()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.VerifyTotpCodeAsync(user.Id, "000000", CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_NoSecret_ReturnsFalse()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var user = SeedUser(db, org.Id, totpSecret: null);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.VerifyTotpCodeAsync(user.Id, "123456", CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        using var db = CreateContext();
        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.VerifyTotpCodeAsync(Guid.NewGuid(), "123456", CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetTwoFactorAsync_ClearsSecretAndEnrolledAt()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow);
        var actorUser = SeedUser(db, org.Id, email: "actor@test.com");

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        await service.ResetTwoFactorAsync(actorUser.Id, user.Id, ct: CancellationToken.None);

        // Assert
        var savedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(savedUser.TotpSecret);
        Assert.Null(savedUser.TotpEnrolledAt);
    }

    [Fact]
    public async Task ResetTwoFactorAsync_IncrementsTokenVersion()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow);
        var actorUser = SeedUser(db, org.Id, email: "actor@test.com");
        var originalVersion = user.TokenVersion;

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        await service.ResetTwoFactorAsync(actorUser.Id, user.Id, ct: CancellationToken.None);

        // Assert
        var savedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(originalVersion + 1, savedUser.TokenVersion);
    }

    [Fact]
    public async Task ResetTwoFactorAsync_RecordsAuditEvent()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret, totpEnrolledAt: DateTime.UtcNow);
        var actorUser = SeedUser(db, org.Id, email: "actor@test.com");

        var auditService = new CollectingAuditService();
        var service = new TwoFactorService(
            db,
            Options.Create(DefaultOptions),
            auditService,
            NullLogger<TwoFactorService>.Instance);

        // Act
        await service.ResetTwoFactorAsync(actorUser.Id, user.Id, ct: CancellationToken.None);

        // Assert
        var auditEvent = auditService.Events.SingleOrDefault(e => e.EventType == AuditEventTypes.TwoFactorReset);
        Assert.NotNull(auditEvent);
        Assert.Equal(org.Id, auditEvent.OrgId);
        Assert.Equal(actorUser.Id, auditEvent.ActorId);
        Assert.Equal(user.Id, auditEvent.SubjectId);
    }

    [Fact]
    public async Task IsEnrolledAsync_ReturnsTrue_WhenEnrolled()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var user = SeedUser(db, org.Id,
            totpSecret: Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20)),
            totpEnrolledAt: DateTime.UtcNow);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.IsEnrolledAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsEnrolledAsync_ReturnsFalse_WhenNotEnrolled()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);
        var user = SeedUser(db, org.Id, totpSecret: null);

        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act
        var result = await service.IsEnrolledAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnrollAsync_RecordsAuditEvent_OnSuccess()
    {
        // Arrange
        using var db = CreateContext();
        var org = SeedOrganisation(db);

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);
        var user = SeedUser(db, org.Id, totpSecret: base32Secret);

        var totp = new Totp(secret, step: DefaultOptions.StepSeconds, totpSize: DefaultOptions.CodeLength);
        var validCode = totp.ComputeTotp(DateTime.UtcNow);

        var auditService = new CollectingAuditService();
        var service = new TwoFactorService(
            db,
            Options.Create(DefaultOptions),
            auditService,
            NullLogger<TwoFactorService>.Instance);

        // Act
        await service.EnrollAsync(user.Id, validCode, ct: CancellationToken.None);

        // Assert
        var auditEvent = auditService.Events.SingleOrDefault(e => e.EventType == AuditEventTypes.TwoFactorEnrolled);
        Assert.NotNull(auditEvent);
        Assert.Equal(org.Id, auditEvent.OrgId);
        Assert.Equal(user.Id, auditEvent.ActorId);
        Assert.Equal(user.Id, auditEvent.SubjectId);
    }

    [Fact]
    public async Task ResetTwoFactorAsync_ThrowsForUnknownUser()
    {
        // Arrange
        using var db = CreateContext();
        var service = new TestableTwoFactorService(
            db,
            Options.Create(DefaultOptions),
            NullLogger<TwoFactorService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ResetTwoFactorAsync(Guid.NewGuid(), Guid.NewGuid(), ct: CancellationToken.None));
    }
}
