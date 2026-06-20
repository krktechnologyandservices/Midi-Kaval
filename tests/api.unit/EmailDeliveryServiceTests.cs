using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Email.Templates;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.UnitTests;

public class EmailNotificationChannelMapperTests
{
    [Theory]
    [InlineData(NotificationEventTypes.TravelClaimSubmitted, true)]
    [InlineData(NotificationEventTypes.CaseTransferred, true)]
    [InlineData(NotificationEventTypes.ReportExportReady, true)]
    [InlineData("visit.overdue", false)]
    public void IsKnownEventType_ReturnsExpected(string eventType, bool expected) =>
        Assert.Equal(expected, EmailNotificationChannelMapper.IsKnownEventType(eventType));

    [Fact]
    public void IsChannelEnabled_MapsAssignmentsChannel() =>
        Assert.True(EmailNotificationChannelMapper.IsChannelEnabled(
            new NotificationPreferenceChannelsDto { Assignments = true },
            NotificationEventTypes.CaseTransferred));
}

public class EmailTemplateTests
{
    [Fact]
    public void CourtReminderTemplate_OmitsPurposeForPocsoCase()
    {
        var context = new CourtReminderEmailContext(
            "District Court",
            DateTime.Parse("2026-06-21T10:00:00Z").ToUniversalTime(),
            "Sensitive beneficiary discussion",
            SensitivityLevel.POCSO,
            "CR-100",
            "ST-200");

        var body = CourtReminderEmailTemplate.RenderBody(context);

        Assert.Contains(CourtSittingEmailBodyHelper.PocsoPurposeLine, body);
        Assert.DoesNotContain("Sensitive beneficiary discussion", body);
        Assert.Contains(EmailTemplateFooter.Line, body);
    }

    [Fact]
    public void CourtReminderTemplate_IncludesPurposeForStandardCase()
    {
        var context = new CourtReminderEmailContext(
            "District Court",
            DateTime.Parse("2026-06-21T10:00:00Z").ToUniversalTime(),
            "Bail hearing",
            SensitivityLevel.Standard,
            "CR-100",
            "ST-200");

        var body = CourtReminderEmailTemplate.RenderBody(context);

        Assert.Contains("Purpose: Bail hearing", body);
    }

    [Fact]
    public void TravelClaimDecisionTemplate_UsesSupervisorFacingCopy()
    {
        var context = new TravelClaimDecisionEmailContext(
            "worker@example.com",
            "Chennai",
            1500m,
            IsApproved: true,
            DirectorComment: "Approved for June visit",
            ClaimId: Guid.NewGuid());

        var body = TravelClaimDecisionEmailTemplate.RenderBody(context);

        Assert.Contains("Claim from worker@example.com for Chennai (₹1500) was approved.", body);
        Assert.Contains("Director note: Approved for June visit", body);
        Assert.Contains(EmailTemplateFooter.Line, body);
    }

    [Fact]
    public void ReportExportReadyTemplate_IncludesReportAndExpiry()
    {
        var expires = DateTime.Parse("2026-06-22T12:00:00Z").ToUniversalTime();
        var body = ReportExportReadyEmailTemplate.RenderBody(
            new ReportExportReadyEmailContext("Monthly visits", expires));

        Assert.Contains("Monthly visits", body);
        Assert.Contains(expires.ToString("O"), body);
        Assert.Contains(EmailTemplateFooter.Line, body);
    }
}

public class EmailDeliveryServiceTests
{
    [Fact]
    public void ShouldSendToUser_SkipsFieldWorkerWithoutOperationalOverride()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "worker@example.com",
            Role = UserRoles.CaseWorker,
            IsActive = true,
        };

        Assert.False(EmailDeliveryService.ShouldSendToUser(
            user,
            NotificationEventTypes.CaseTransferred,
            operationalOverride: false));
    }

    [Fact]
    public void ShouldSendToUser_AllowsAssigneeOperationalOverride()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "worker@example.com",
            Role = UserRoles.CaseWorker,
            IsActive = true,
        };

        Assert.True(EmailDeliveryService.ShouldSendToUser(
            user,
            NotificationEventTypes.CaseTransferred,
            operationalOverride: true));
    }

    [Fact]
    public async Task SendCourtReminderEmailsAsync_PropagatesEmailDeliveryException()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var assignee = SeedFieldWorker(context, organisationId, Guid.NewGuid(), "worker@example.com");
        var caseEntity = SeedCase(context, organisationId, assignee.Id);
        var sitting = SeedSitting(context, organisationId, caseEntity.Id);
        await context.SaveChangesAsync();

        var fakeSender = new FakeEmailSender { FailNextSend = true };
        var service = new EmailDeliveryService(
            context,
            fakeSender,
            NullLogger<EmailDeliveryService>.Instance);

        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            service.SendCourtReminderEmailsAsync(sitting, caseEntity, assignee));
    }

    [Fact]
    public async Task TrySendTravelClaimSubmittedAsync_SwallowsEmailDeliveryException()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        SeedCoordinator(context, organisationId, Guid.NewGuid(), "coordinator@example.com");
        await context.SaveChangesAsync();

        var claim = new TravelClaim
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ClaimantUserId = Guid.NewGuid(),
            Destination = "Chennai",
            Amount = 500m,
            ClaimDate = DateTime.UtcNow,
            Status = TravelClaimStatus.Submitted,
            TransportMode = TransportMode.Bus,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var fakeSender = new FakeEmailSender { FailNextSend = true };
        var service = new EmailDeliveryService(
            context,
            fakeSender,
            NullLogger<EmailDeliveryService>.Instance);

        await service.TrySendTravelClaimSubmittedAsync(claim, "worker@example.com");
    }

    [Fact]
    public async Task TrySendReportExportReadyAsync_SendsWhenReportsChannelEnabled()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedCoordinator(context, organisationId, userId, "coordinator@example.com");
        await context.SaveChangesAsync();

        var fakeSender = new FakeEmailSender();
        var service = new EmailDeliveryService(
            context,
            fakeSender,
            NullLogger<EmailDeliveryService>.Instance);

        var expires = DateTime.UtcNow.AddHours(24);
        await service.TrySendReportExportReadyAsync(
            userId,
            organisationId,
            "Monthly visits",
            expires);

        var message = Assert.Single(fakeSender.Messages);
        Assert.Equal("coordinator@example.com", message.To);
        Assert.Contains("Monthly visits", message.Subject);
        Assert.Contains(expires.ToString("O"), message.Body);
    }

    [Fact]
    public void ShouldSendToUser_SkipsUserWithEmptyEmail()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "",
            Role = UserRoles.Coordinator,
            IsActive = true,
        };

        Assert.False(EmailDeliveryService.ShouldSendToUser(
            user,
            NotificationEventTypes.CaseTransferred,
            operationalOverride: false));
    }

    [Fact]
    public async Task SendToDeskStaffAsync_FansOutToMultipleCoordinators()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        SeedCoordinator(context, organisationId, Guid.NewGuid(), "coord1@example.com");
        SeedCoordinator(context, organisationId, Guid.NewGuid(), "coord2@example.com");
        await context.SaveChangesAsync();

        var fakeSender = new FakeEmailSender();
        var service = new EmailDeliveryService(
            context,
            fakeSender,
            NullLogger<EmailDeliveryService>.Instance);

        // Trigger via TrySendTravelClaimSubmittedAsync which fans out to desk staff
        var claim = new TravelClaim
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ClaimantUserId = Guid.NewGuid(),
            Destination = "Chennai",
            Amount = 500m,
            ClaimDate = DateTime.UtcNow,
            Status = TravelClaimStatus.Submitted,
            TransportMode = TransportMode.Bus,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await service.TrySendTravelClaimSubmittedAsync(claim, "worker@example.com");

        Assert.Equal(2, fakeSender.Messages.Count);
        Assert.Contains(fakeSender.Messages, m => m.To == "coord1@example.com");
        Assert.Contains(fakeSender.Messages, m => m.To == "coord2@example.com");
    }

    [Fact]
    public async Task SendCourtReminderEmailsAsync_DeduplicatesSharedEmail()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();

        // Coordinator and assignee share the same email
        var sharedEmail = "shared@example.com";
        var userId = Guid.NewGuid();
        SeedCoordinator(context, organisationId, userId, sharedEmail);
        await context.SaveChangesAsync();

        var assignee = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = sharedEmail,
            Role = UserRoles.CaseWorker,
            IsActive = true,
            TokenVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var caseEntity = new Case
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            CrimeNumber = "CR-1",
            StNumber = "ST-1",
            BeneficiaryName = "Hidden",
            TypeOfOffence = "Theft",
            OffenceClassification = OffenceClassification.Petty,
            Domicile = Domicile.Urban,
            CreatedByUserId = assignee.Id,
            AssignedWorkerId = assignee.Id,
            CurrentStage = CaseStage.ProcessInitiation,
            SensitivityLevel = SensitivityLevel.Standard,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var sitting = new CourtSitting
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            CaseId = caseEntity.Id,
            CourtName = "District Court",
            ScheduledAtUtc = DateTime.UtcNow.AddDays(1),
            Purpose = "Hearing",
            Status = CourtSittingStatus.Upcoming,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var fakeSender = new FakeEmailSender();
        var service = new EmailDeliveryService(
            context,
            fakeSender,
            NullLogger<EmailDeliveryService>.Instance);

        await service.SendCourtReminderEmailsAsync(sitting, caseEntity, assignee);

        // Coordinator and assignee share email → only 1 email sent
        Assert.Single(fakeSender.Messages);
        Assert.Equal(sharedEmail, fakeSender.Messages[0].To);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User SeedFieldWorker(
        AppDbContext context,
        Guid organisationId,
        Guid userId,
        string email)
    {
        var user = new User
        {
            Id = userId,
            OrganisationId = organisationId,
            Email = email,
            Role = UserRoles.CaseWorker,
            IsActive = true,
            TokenVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        context.Users.Add(user);
        return user;
    }

    private static User SeedCoordinator(
        AppDbContext context,
        Guid organisationId,
        Guid userId,
        string email)
    {
        var user = new User
        {
            Id = userId,
            OrganisationId = organisationId,
            Email = email,
            Role = UserRoles.Coordinator,
            IsActive = true,
            TokenVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        context.Users.Add(user);
        return user;
    }

    private static Case SeedCase(AppDbContext context, Guid organisationId, Guid assigneeId)
    {
        var caseEntity = new Case
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            CrimeNumber = "CR-1",
            StNumber = "ST-1",
            BeneficiaryName = "Hidden",
            TypeOfOffence = "Theft",
            OffenceClassification = OffenceClassification.Petty,
            Domicile = Domicile.Urban,
            CreatedByUserId = assigneeId,
            AssignedWorkerId = assigneeId,
            CurrentStage = CaseStage.ProcessInitiation,
            SensitivityLevel = SensitivityLevel.Standard,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        context.Cases.Add(caseEntity);
        return caseEntity;
    }

    private static CourtSitting SeedSitting(AppDbContext context, Guid organisationId, Guid caseId)
    {
        var sitting = new CourtSitting
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            CaseId = caseId,
            CourtName = "District Court",
            ScheduledAtUtc = DateTime.UtcNow.AddDays(1),
            Purpose = "Hearing",
            Status = CourtSittingStatus.Upcoming,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        context.CourtSittings.Add(sitting);
        return sitting;
    }
}
