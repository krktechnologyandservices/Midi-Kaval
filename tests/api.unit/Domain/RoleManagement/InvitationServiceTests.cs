using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Models.Admin;
using MidiKaval.Api.Models.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MidiKaval.Api.UnitTests.Domain.RoleManagement;

public class InvitationServiceTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-32-chars!";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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

    private static IConfiguration CreateConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INVITATION_TOKEN_TTL_HOURS"] = "24",
                ["ActivationLink:BaseUrl"] = "http://localhost:4200",
            })
            .Build();
    }

    private static Organisation SeedOrganisation(AppDbContext db)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Organisation",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        db.SaveChanges();
        return org;
    }

    private static User SeedUser(AppDbContext db, Guid orgId, string role, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            Role = role,
            TokenVersion = 0,
            IsActive = true,
            IsSuspended = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Invitation SeedInvitation(AppDbContext db, Guid orgId, string email, string status, DateTime? expiresAtUtc, Guid invitedByUserId)
    {
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            InvitedByUserId = invitedByUserId,
            TargetEmail = email,
            Role = UserRoles.Coordinator,
            TokenHash = "hash",
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddHours(24),
            Status = status,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
        };
        db.Invitations.Add(invitation);
        db.SaveChanges();
        return invitation;
    }

    /// <summary>
    /// Testable InvitationService that captures scheduled background jobs instead
    /// of running Hangfire, so tests work without a Hangfire server.
    /// </summary>
    private sealed class TestableInvitationService : InvitationService
    {
        public List<(Guid invitationId, string rawToken, string signature, string email, string role)> EnqueuedJobs { get; } = [];
        public List<(string originalInviterEmail, string originalInviterName, string targetEmail, string resentByUserName)> ResendNotifications { get; } = [];

        public TestableInvitationService(
            AppDbContext db,
            TokenService tokenService,
            IAuditService auditService,
            IConfiguration configuration)
            : base(db, tokenService, auditService, configuration, NullLogger<InvitationService>.Instance)
        {
        }

        protected override void EnqueueEmailJob(Guid invitationId, string rawToken, string signature, string email, string role)
        {
            EnqueuedJobs.Add((invitationId, rawToken, signature, email, role));
        }

        protected override void EnqueueResendNotificationJob(
            string originalInviterEmail,
            string originalInviterName,
            string targetEmail,
            string resentByUserName)
        {
            ResendNotifications.Add((originalInviterEmail, originalInviterName, targetEmail, resentByUserName));
        }
    }

    private static TestableInvitationService CreateService(AppDbContext db, IAuditService? auditService = null)
    {
        var tokenService = CreateTokenService();
        var config = CreateConfig();
        auditService ??= new FakeAuditService();
        return new TestableInvitationService(db, tokenService, auditService, config);
    }

    public class SendInvitationAsync
    {
        [Fact]
        public async Task SendsSuccessfully()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@example.org");
            var auditService = new FakeAuditService();
            var service = CreateService(db, auditService);

            var request = new SendInvitationRequest(
                Email: "newuser@example.org",
                Role: UserRoles.Coordinator);

            var result = await service.SendInvitationAsync(org.Id, director.Id, request, default);

            Assert.Equal("newuser@example.org", result.TargetEmail);
            Assert.Equal(UserRoles.Coordinator, result.Role);
            Assert.Contains("Invitation sent to", result.Message);

            var saved = await db.Invitations.FirstAsync(i => i.TargetEmail == "newuser@example.org");
            Assert.Equal(org.Id, saved.OrganisationId);
            Assert.Equal(director.Id, saved.InvitedByUserId);
            Assert.Equal(InvitationStatus.Pending, saved.Status);
            Assert.True(saved.ExpiresAtUtc > DateTime.UtcNow.AddHours(23));

            Assert.Single(auditService.RecordedEvents);
            Assert.Equal(AuditEventTypes.InvitationSent, auditService.RecordedEvents[0].eventType);

            Assert.Single(service.EnqueuedJobs);
            Assert.Equal(saved.Id, service.EnqueuedJobs[0].invitationId);
            Assert.Equal("newuser@example.org", service.EnqueuedJobs[0].email);
        }

        [Fact]
        public async Task DuplicateEmail_ReturnsConflict()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@example.org");
            SeedUser(db, org.Id, UserRoles.Coordinator, "existing@example.org");
            var service = CreateService(db);

            var request = new SendInvitationRequest(
                Email: "existing@example.org",
                Role: UserRoles.Coordinator);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendInvitationAsync(org.Id, director.Id, request, default));

            Assert.Contains("already registered", ex.Message);
        }

        [Fact]
        public async Task DuplicatePendingInvitation_ReturnsConflict()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@example.org");
            SeedInvitation(db, org.Id, "pending@example.org", InvitationStatus.Pending, expiresAtUtc: DateTime.UtcNow.AddDays(1), invitedByUserId: director.Id);
            var service = CreateService(db);

            var request = new SendInvitationRequest(
                Email: "pending@example.org",
                Role: UserRoles.Coordinator);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendInvitationAsync(org.Id, director.Id, request, default));

            Assert.Contains("already pending", ex.Message);
        }

        [Theory]
        [InlineData(UserRoles.Vendor)]
        public async Task InvalidRole_ReturnsValidationError(string invalidRole)
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@example.org");
            var service = CreateService(db);

            var request = new SendInvitationRequest(
                Email: "newuser@example.org",
                Role: invalidRole);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendInvitationAsync(org.Id, director.Id, request, default));

            Assert.Contains("Invalid role", ex.Message);
        }

        [Fact]
        public async Task UnknownRole_ReturnsValidationError()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@example.org");
            var service = CreateService(db);

            var request = new SendInvitationRequest(
                Email: "newuser@example.org",
                Role: "NonExistentRole");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendInvitationAsync(org.Id, director.Id, request, default));

            Assert.Contains("Invalid role", ex.Message);
        }
    }

    public class GetInvitationListAsync
    {
        [Fact]
        public async Task ReturnsPaginatedResults()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var otherOrg = SeedOrganisation(db);
            var helper = SeedUser(db, org.Id, UserRoles.Director, "helper@org.com");

            for (int i = 0; i < 15; i++)
            {
                SeedInvitation(db, org.Id, $"user{i}@org.com", InvitationStatus.Pending, expiresAtUtc: null, invitedByUserId: helper.Id);
            }
            SeedInvitation(db, otherOrg.Id, "other@other.com", InvitationStatus.Pending, expiresAtUtc: null, invitedByUserId: helper.Id);

            var service = CreateService(db);

            var page1 = await service.GetInvitationListAsync(org.Id, page: 1, pageSize: 10);
            var page2 = await service.GetInvitationListAsync(org.Id, page: 2, pageSize: 10);

            Assert.Equal(15, page1.TotalCount);
            Assert.Equal(10, page1.Items.Count);
            Assert.Equal(5, page2.Items.Count);
            Assert.DoesNotContain(page1.Items, i => i.TargetEmail == "other@other.com");
        }

        [Fact]
        public async Task EmptyList()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var service = CreateService(db);

            var result = await service.GetInvitationListAsync(org.Id, page: 1, pageSize: 25);

            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task OrdersByCreatedAtUtcDescending()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var helper = SeedUser(db, org.Id, UserRoles.Director, "helper@org.com");

            // Add with deliberate time gap
            var older = SeedInvitation(db, org.Id, "older@org.com", InvitationStatus.Pending, invitedByUserId: helper.Id, expiresAtUtc: DateTime.UtcNow.AddDays(2));
            await Task.Delay(50);
            var newer = SeedInvitation(db, org.Id, "newer@org.com", InvitationStatus.Pending, invitedByUserId: helper.Id, expiresAtUtc: DateTime.UtcNow.AddDays(3));

            var service = CreateService(db);
            var result = await service.GetInvitationListAsync(org.Id, page: 1, pageSize: 25);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("newer@org.com", result.Items[0].TargetEmail);
            Assert.Equal("older@org.com", result.Items[1].TargetEmail);
        }

        [Fact]
        public async Task IncludesInvitedByUserFields()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@org.com");
            SeedInvitation(db, org.Id, "invited@example.org", InvitationStatus.Pending, expiresAtUtc: DateTime.UtcNow.AddHours(24), invitedByUserId: director.Id);

            var service = CreateService(db);
            var result = await service.GetInvitationListAsync(org.Id, page: 1, pageSize: 25);

            Assert.Single(result.Items);
            var item = result.Items[0];
            Assert.Equal("director@org.com", item.InvitedByUserEmail);
            Assert.Equal("Test User", item.InvitedByUserName);
        }
    }

    public class ResendInvitationAsync
    {
        [Fact]
        public async Task SendsSuccessfully()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var helper = SeedUser(db, org.Id, UserRoles.Director, "helper@org.com");
            var invitation = SeedInvitation(db, org.Id, "user@example.org", InvitationStatus.Pending, expiresAtUtc: DateTime.UtcNow.AddHours(24), invitedByUserId: helper.Id);
            var auditService = new FakeAuditService();
            var service = CreateService(db, auditService);

            var resendByUserId = Guid.NewGuid();
            var result = await service.ResendInvitationAsync(org.Id, invitation.Id, resendByUserId, default);

            Assert.Equal(invitation.Id, result.Id);
            Assert.Equal("user@example.org", result.TargetEmail);
            Assert.True(result.NewExpiresAtUtc > DateTime.UtcNow.AddHours(23));

            var updated = await db.Invitations.FirstAsync(i => i.Id == invitation.Id);
            Assert.NotEqual("hash", updated.TokenHash); // Token should have changed
            Assert.True(updated.ExpiresAtUtc > DateTime.UtcNow.AddHours(23));

            Assert.Equal(2, auditService.RecordedEvents.Count);
            Assert.Equal(AuditEventTypes.InvitationResent, auditService.RecordedEvents[0].eventType);
            Assert.Equal(resendByUserId, auditService.RecordedEvents[0].actorUserId);
            Assert.Equal(AuditEventTypes.InvitationResentNotified, auditService.RecordedEvents[1].eventType);

            Assert.Single(service.EnqueuedJobs);
            Assert.Single(service.ResendNotifications);
            Assert.Equal("helper@org.com", service.ResendNotifications[0].originalInviterEmail);
            Assert.Equal("Unknown", service.ResendNotifications[0].resentByUserName);
        }

        [Fact]
        public async Task NotFound_Returns404()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ResendInvitationAsync(org.Id, Guid.NewGuid(), Guid.NewGuid(), default));

            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task AlreadyConfirmed_Returns409()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var director = SeedUser(db, org.Id, UserRoles.Director, "director@org.com");
            var invitation = SeedInvitation(db, org.Id, "user@example.org", InvitationStatus.Confirmed, expiresAtUtc: null, invitedByUserId: director.Id);
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ResendInvitationAsync(org.Id, invitation.Id, Guid.NewGuid(), default));

            Assert.Contains("confirmed", ex.Message);
        }

        [Fact]
        public async Task RecordsBothAuditEventsAndNotifiesOriginalInviter()
        {
            using var db = CreateContext();
            var org = SeedOrganisation(db);
            var inviterUser = SeedUser(db, org.Id, UserRoles.Director, "inviter@org.com");
            var invitation = SeedInvitation(db, org.Id, "user@example.org", InvitationStatus.Pending, expiresAtUtc: DateTime.UtcNow.AddHours(24), invitedByUserId: inviterUser.Id);
            var resenderUser = SeedUser(db, org.Id, UserRoles.Director, "resender@org.com");
            var auditService = new FakeAuditService();
            var service = CreateService(db, auditService);

            var result = await service.ResendInvitationAsync(org.Id, invitation.Id, resenderUser.Id, default);

            // Should record 2 audit events: InvitationResent + InvitationResendNotified
            Assert.Equal(2, auditService.RecordedEvents.Count);

            // First event: InvitationResent with resend metadata
            Assert.Equal(AuditEventTypes.InvitationResent, auditService.RecordedEvents[0].eventType);
            Assert.Equal(resenderUser.Id, auditService.RecordedEvents[0].actorUserId);
            Assert.Contains(auditService.RecordedEvents[0].metadata!, m => m.Key == "original_invited_by_user_id");

            // Second event: InvitationResendNotified with inviter details
            Assert.Equal(AuditEventTypes.InvitationResentNotified, auditService.RecordedEvents[1].eventType);
            Assert.Equal(resenderUser.Id, auditService.RecordedEvents[1].actorUserId);
            Assert.Equal(inviterUser.Id, auditService.RecordedEvents[1].subjectUserId);

            // Should enqueue the resend notification
            Assert.Single(service.ResendNotifications);
            Assert.Equal("inviter@org.com", service.ResendNotifications[0].originalInviterEmail);
            Assert.Equal("user@example.org", service.ResendNotifications[0].targetEmail);
            Assert.Equal("Test User", service.ResendNotifications[0].resentByUserName);

            // Email to target should still be enqueued
            Assert.Single(service.EnqueuedJobs);
            Assert.Equal(invitation.Id, service.EnqueuedJobs[0].invitationId);
        }
    }
}

/// <summary>
/// Fake audit service that records events in memory for test assertions.
/// </summary>
public sealed class FakeAuditService : IAuditService
{
    public List<(string eventType, Guid organisationId, Guid? actorUserId, Guid? subjectUserId, TargetUserSnapshotDto? targetUserSnapshot, string? actorIpAddress, IReadOnlyDictionary<string, object?>? metadata)> RecordedEvents { get; } = [];

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
        RecordedEvents.Add((eventType, organisationId, actorUserId, subjectUserId, targetUserSnapshot, actorIpAddress, metadata));
        return Task.CompletedTask;
    }
}
