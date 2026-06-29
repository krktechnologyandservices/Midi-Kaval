using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Jobs;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class UserStatusEmailJobTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;

    public UserStatusEmailJobTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _factory.EmailSender.Clear();
        _factory.NotificationRateLimiter.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrganisationId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private const string UserEmail = "user@test.com";
    private const string UserName = "Test User";

    [Fact]
    public async Task SuspensionEmail_SendsWithReasonAndAppealInstructions()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", "Violated policy");

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Equal(UserEmail, email.To);
        Assert.Contains("suspended", email.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Violated policy", email.Body);
        Assert.Contains("contact another Director", email.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletionEmail_SendsWithIrreversibleNotice()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "deleted", null);

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Equal(UserEmail, email.To);
        Assert.Contains("permanently deleted", email.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be undone", email.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permanently removed", email.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimit_BlocksFourthEmail_OfSameType()
    {
        _factory.NotificationRateLimiter.MaxPerDay = 3;

        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        // Send 3 emails — all should succeed
        for (var i = 0; i < 3; i++)
        {
            await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null);
        }

        Assert.Equal(3, _factory.EmailSender.Messages.Count);

        // 4th email should be silently skipped
        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null);

        // Still only 3 — 4th was rate-limited
        Assert.Equal(3, _factory.EmailSender.Messages.Count);
    }

    [Fact]
    public async Task DeliveryFailure_Throws_ForHangfireRetry()
    {
        _factory.EmailSender.FailNextSend = true;

        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null));

        Assert.Contains("Simulated SMTP failure", ex.Message);

        // No email was sent (and no audit should be recorded since send failed before audit)
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task AuditEvent_Recorded_AfterSuccessfulSend()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get a fresh userId that exists in the DB so the audit FK works (subjectUserId)
        // Use the seeded admin user
        var adminUser = await db.Users.FirstAsync(u => u.Email == AuthTestData.Email);

        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        await job.ExecuteAsync(adminUser.Id, AuthTestData.OrganisationId, adminUser.Email, "Admin User", "suspended", "Test");

        // Verify email was sent
        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Contains("suspended", email.Subject);

        // Verify audit event was recorded
        var auditEvent = await db.AuditEvents
            .Where(e => e.EventType == AuditEventTypes.UserNotificationSent
                && e.SubjectUserId == adminUser.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(auditEvent);
        Assert.Contains("\"notification_type\": \"suspended\"", auditEvent.MetadataJson);
    }

    [Fact]
    public async Task DifferentNotificationTypes_HaveSeparateRateLimitCounters()
    {
        _factory.NotificationRateLimiter.MaxPerDay = 3;

        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        // Send 3 suspension emails
        for (var i = 0; i < 3; i++)
        {
            await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null);
        }

        Assert.Equal(3, _factory.EmailSender.Messages.Count);

        // Deletion is a different type — should not be rate-limited
        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "deleted", null);

        Assert.Equal(4, _factory.EmailSender.Messages.Count);
    }

    [Fact]
    public async Task SuspensionEmail_NullReason_StillSends()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null);

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Equal(UserEmail, email.To);
        Assert.Contains("suspended", email.Subject);
        // Should not contain "Reason:" when no reason provided
        Assert.DoesNotContain("Reason:", email.Body);
        // Should not have nested <p> (HTML should be valid)
        Assert.DoesNotContain("<p><", email.Body);
    }

    [Fact]
    public async Task SuspensionEmail_EmptyReason_StillSends()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        await job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", "");

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Equal(UserEmail, email.To);
        Assert.Contains("suspended", email.Subject);
        Assert.DoesNotContain("Reason:", email.Body);
    }

    [Fact]
    public async Task ConcurrentRateLimit_BlocksExcessCalls()
    {
        _factory.NotificationRateLimiter.MaxPerDay = 3;

        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        // Fire 5 concurrent calls — only 3 should result in emails
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            job.ExecuteAsync(UserId, OrganisationId, UserEmail, UserName, "suspended", null));

        await Task.WhenAll(tasks);

        Assert.Equal(3, _factory.EmailSender.Messages.Count);
    }

    [Fact]
    public async Task HtmlInjection_EscapedInBody()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        var maliciousName = "<script>alert('xss')</script>";
        var maliciousReason = "<img src=x onerror=alert(1)>";

        await job.ExecuteAsync(
            UserId, OrganisationId, UserEmail, maliciousName, "suspended", maliciousReason);

        var email = Assert.Single(_factory.EmailSender.Messages);
        // Raw HTML tags should NOT appear in the body — they must be HTML-encoded
        Assert.DoesNotContain("<script>", email.Body);
        Assert.DoesNotContain("<img", email.Body);
        Assert.DoesNotContain("onerror", email.Body);
        // Encoded versions SHOULD be present
        Assert.Contains("&lt;script&gt;", email.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", email.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VeryLongNameAndReason_DoesNotBreakEmail()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<UserStatusEmailJob>();

        var longName = new string('A', 500) + " " + new string('B', 500);
        var longReason = new string('X', 2000);

        await job.ExecuteAsync(
            UserId, OrganisationId, UserEmail, longName, "suspended", longReason);

        var email = Assert.Single(_factory.EmailSender.Messages);
        Assert.Contains("suspended", email.Subject);
        Assert.Contains(new string('A', 500), email.Body);
        Assert.Contains(new string('X', 2000), email.Body);
    }
}
