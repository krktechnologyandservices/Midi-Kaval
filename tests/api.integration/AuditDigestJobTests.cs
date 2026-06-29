using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AuditDigestJobTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;

    public AuditDigestJobTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _factory.EmailSender.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SingleEventDigest_SendsEmailWithEventDetails()
    {
        var (orgId, directorEmail) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = AuditEventTypes.UserSuspended,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                TargetUserSnapshot = """{"email":"target@test.com","name":"Target User","role":"SocialWorker"}""",
            });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        var emails = _factory.EmailSender.Messages;
        var email = Assert.Single(emails);
        Assert.Equal(directorEmail, email.To);
        Assert.Contains("User activity digest", email.Subject);
        Assert.Contains("User suspended", email.Body);
        Assert.Contains("Target User", email.Body);
        Assert.Contains("target@test.com", email.Body);
    }

    [Fact]
    public async Task BatchedMultiEventDigest_CombinesAllEventsInOneEmail()
    {
        var (orgId, directorEmail) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgId,
                    EventType = AuditEventTypes.UserSuspended,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    TargetUserSnapshot = """{"email":"u1@test.com","name":"User One"}""",
                },
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgId,
                    EventType = AuditEventTypes.UserReactivated,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-8),
                    TargetUserSnapshot = """{"email":"u2@test.com","name":"User Two"}""",
                },
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgId,
                    EventType = AuditEventTypes.UserDeleted,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-6),
                    TargetUserSnapshot = """{"email":"u3@test.com","name":"User Three"}""",
                });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Contains("User suspended", email.Body);
        Assert.Contains("User reactivated", email.Body);
        Assert.Contains("User permanently deleted", email.Body);
    }

    [Fact]
    public async Task Dedup_SecondRun_SendsNoNewEmails()
    {
        var (orgId, _) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = AuditEventTypes.UserSuspended,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();
        var firstCount = _factory.EmailSender.Messages.Count;

        _factory.EmailSender.Clear();
        await RunAuditDigestJobAsync();

        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task MultiOrganisationIsolation_EachOrgGetsOwnEvents()
    {
        var (orgAId, directorAEmail) = await SeedOrganisationWithDirectorAsync();
        var (orgBId, directorBEmail) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgAId,
                    EventType = AuditEventTypes.UserSuspended,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    TargetUserSnapshot = """{"email":"a@test.com","name":"OrgA User"}""",
                },
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgBId,
                    EventType = AuditEventTypes.UserReactivated,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-8),
                    TargetUserSnapshot = """{"email":"b@test.com","name":"OrgB User"}""",
                });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        var dirAEmail = _factory.EmailSender.Messages.Single(m => m.To == directorAEmail);
        Assert.Contains("OrgA User", dirAEmail.Body);
        Assert.DoesNotContain("OrgB User", dirAEmail.Body);

        var dirBEmail = _factory.EmailSender.Messages.Single(m => m.To == directorBEmail);
        Assert.Contains("OrgB User", dirBEmail.Body);
        Assert.DoesNotContain("OrgA User", dirBEmail.Body);
    }

    [Fact]
    public async Task NoActiveDirectors_NoEmailSentButEventsMarkedProcessed()
    {
        var (orgId, _) = await SeedOrganisationWithDirectorAsync(active: false);

        Guid eventId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditEvent = new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = AuditEventTypes.UserSuspended,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            };
            eventId = auditEvent.Id;
            db.AuditEvents.Add(auditEvent);
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        Assert.Empty(_factory.EmailSender.Messages);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.AuditDigestEntries
                .SingleOrDefaultAsync(e => e.AuditEventId == eventId);
            Assert.NotNull(entry);
        }
    }

    [Fact]
    public async Task EmailFailureForOneDirector_OtherDirectorStillReceivesDigest()
    {
        var (orgId, directorBEmail) = await SeedOrganisationWithDirectorAsync();
        var (_, directorBEmail2) = await SeedOrganisationWithDirectorAsync(orgId: orgId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = AuditEventTypes.UserDeleted,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            });
            await db.SaveChangesAsync();
        }

        _factory.EmailSender.FailNextSend = true;
        await RunAuditDigestJobAsync();

        Assert.Single(_factory.EmailSender.Messages);

        var sentEmail = _factory.EmailSender.Messages[0];
        Assert.Equal(directorBEmail2, sentEmail.To);

        // Verify events are marked processed despite the email failure (AC 6)
        await using (var verifyScope = _factory.Services.CreateAsyncScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entries = await verifyDb.AuditDigestEntries
                .Where(e => e.OrganisationId == orgId)
                .ToListAsync();
            Assert.NotEmpty(entries);
        }
    }

    [Fact]
    public async Task OnlyUserManagementEventTypes_AreIncludedInDigest()
    {
        var (orgId, directorEmail) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgId,
                    EventType = AuditEventTypes.UserSuspended,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    TargetUserSnapshot = """{"email":"u@test.com","name":"User"}""",
                },
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgId,
                    EventType = AuditEventTypes.CourtSittingCreated,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-9),
                });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Contains("User suspended", email.Body);
        Assert.DoesNotContain("Court sitting", email.Body);
    }

    [Fact]
    public async Task MultiTenancyDataIsolation_DirectorsSeeOnlyOwnOrgEvents()
    {
        var (orgAId, directorAEmail) = await SeedOrganisationWithDirectorAsync();
        var (orgBId, directorBEmail) = await SeedOrganisationWithDirectorAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgAId,
                    EventType = AuditEventTypes.UserSuspended,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    TargetUserSnapshot = """{"email":"only@orgA.com","name":"OrgA Only"}""",
                },
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    OrganisationId = orgBId,
                    EventType = AuditEventTypes.AccountCreated,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-8),
                    TargetUserSnapshot = """{"email":"only@orgB.com","name":"OrgB Only"}""",
                });
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        var dirAEmail = _factory.EmailSender.Messages.Single(m => m.To == directorAEmail);
        Assert.Contains("OrgA Only", dirAEmail.Body);
        Assert.DoesNotContain("OrgB Only", dirAEmail.Body);

        var dirBEmail = _factory.EmailSender.Messages.Single(m => m.To == directorBEmail);
        Assert.DoesNotContain("OrgA Only", dirBEmail.Body);
        Assert.Contains("OrgB Only", dirBEmail.Body);
    }

    [Fact]
    public async Task SuspendedDirector_DoesNotReceiveDigest_EventsMarkedProcessed()
    {
        var (orgId, _) = await SeedOrganisationWithDirectorAsync(active: true, suspended: true);

        Guid eventId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditEvent = new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                EventType = AuditEventTypes.UserSuspended,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            };
            eventId = auditEvent.Id;
            db.AuditEvents.Add(auditEvent);
            await db.SaveChangesAsync();
        }

        await RunAuditDigestJobAsync();

        Assert.Empty(_factory.EmailSender.Messages);

        // Events still marked processed even though no email was sent (AC 4)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.AuditDigestEntries
                .SingleOrDefaultAsync(e => e.AuditEventId == eventId);
            Assert.NotNull(entry);
        }
    }

    private async Task RunAuditDigestJobAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<AuditDigestJobRunner>();
        await runner.RunAsync();
    }

    private async Task<(Guid OrgId, string DirectorEmail)> SeedOrganisationWithDirectorAsync(
        bool active = true,
        bool suspended = false,
        Guid? orgId = null)
    {
        var organisationId = orgId ?? Guid.NewGuid();
        var directorEmail = $"director-{Guid.NewGuid():N}@digest.test";

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var org = new Organisation
        {
            Id = organisationId,
            Name = $"Digest Org {organisationId:N}"[..30],
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
        };
        db.Organisations.Add(org);

        var director = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = directorEmail,
            FirstName = "Digest",
            LastName = "Director",
            Role = UserRoles.Director,
            IsActive = active,
            IsSuspended = suspended,
            PasswordHash = passwordHasher.HashPassword(new User(), "TestPassword123!"),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            UpdatedAtUtc = DateTime.UtcNow,
            TokenVersion = 0,
        };
        db.Users.Add(director);

        await db.SaveChangesAsync();

        return (organisationId, directorEmail);
    }
}
