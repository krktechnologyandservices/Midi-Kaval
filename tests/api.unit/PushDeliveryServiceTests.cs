using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.UnitTests;

public class PushNotificationChannelMapperTests
{
    [Theory]
    [InlineData(NotificationEventTypes.InterventionOverdue, true)]
    [InlineData(NotificationEventTypes.CourtReminder24h, true)]
    [InlineData(NotificationEventTypes.TravelClaimApproved, true)]
    [InlineData("visit.overdue", false)]
    public void IsKnownEventType_ReturnsExpected(string eventType, bool expected) =>
        Assert.Equal(expected, PushNotificationChannelMapper.IsKnownEventType(eventType));

    [Fact]
    public void IsChannelEnabled_MapsClaimsChannel() =>
        Assert.True(PushNotificationChannelMapper.IsChannelEnabled(
            new NotificationPreferenceChannelsDto { Claims = true },
            NotificationEventTypes.TravelClaimApproved));
}

public class PushDeliveryServiceTests
{
    [Fact]
    public void BuildDataPayload_OmitsEmptyOptionalFields()
    {
        var notification = new InAppNotification
        {
            Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            EventType = NotificationEventTypes.TravelClaimApproved,
            Title = "Travel claim approved",
            Body = "Approved",
            CaseId = Guid.Empty,
            ResourceType = "",
            ResourceId = Guid.Parse("22222222-2222-4222-8222-222222222222"),
        };

        var data = PushDeliveryService.BuildDataPayload(notification);

        Assert.Equal("travel.claim.approved", data["eventType"]);
        Assert.Equal("22222222-2222-4222-8222-222222222222", data["resourceId"]);
        Assert.False(data.ContainsKey("caseId"));
        Assert.False(data.ContainsKey("resourceType"));
    }

    [Fact]
    public async Task TrySendAsync_SkipsDevStubToken()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedFieldWorker(context, organisationId, userId);
        context.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            DeviceInstallId = "device-1",
            Platform = "android",
            PushToken = "dev-stub-token-android",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            LastRegisteredAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var fakeSender = new FakePushNotificationSender(NullLogger<FakePushNotificationSender>.Instance);
        var service = new PushDeliveryService(
            context,
            fakeSender,
            NullLogger<PushDeliveryService>.Instance);

        await service.TrySendAsync(new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            EventType = NotificationEventTypes.TravelClaimApproved,
            Title = "Approved",
            Body = "Body",
            CaseId = Guid.NewGuid(),
            ResourceType = "TravelClaim",
            ResourceId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        });

        Assert.Empty(fakeSender.Messages);
    }

    [Fact]
    public async Task TrySendAsync_SkipsWhenPushDisabledForRole()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            OrganisationId = organisationId,
            Email = "coord@pilot.example",
            Role = UserRoles.Coordinator,
            IsActive = true,
            TokenVersion = 1,
            PasswordHash = "hash",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        context.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            DeviceInstallId = "device-1",
            Platform = "android",
            PushToken = "integration-fcm-token-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            LastRegisteredAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var fakeSender = new FakePushNotificationSender(NullLogger<FakePushNotificationSender>.Instance);
        var service = new PushDeliveryService(
            context,
            fakeSender,
            NullLogger<PushDeliveryService>.Instance);

        await service.TrySendAsync(new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            EventType = NotificationEventTypes.CourtMissEscalated,
            Title = "Missed",
            Body = "Body",
            CaseId = Guid.NewGuid(),
            ResourceType = "CourtSitting",
            ResourceId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        });

        Assert.Empty(fakeSender.Messages);
    }

    [Fact]
    public async Task TrySendAsync_FansOutToMultipleDevices()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedFieldWorker(context, organisationId, userId);
        context.UserDevices.AddRange(
            CreateDevice(organisationId, userId, "device-1", "integration-fcm-token-1"),
            CreateDevice(organisationId, userId, "device-2", "integration-fcm-token-2"));
        await context.SaveChangesAsync();

        var fakeSender = new FakePushNotificationSender(NullLogger<FakePushNotificationSender>.Instance);
        var service = new PushDeliveryService(
            context,
            fakeSender,
            NullLogger<PushDeliveryService>.Instance);

        await service.TrySendAsync(new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            EventType = NotificationEventTypes.TravelClaimApproved,
            Title = "Approved",
            Body = "Body",
            CaseId = Guid.NewGuid(),
            ResourceType = "TravelClaim",
            ResourceId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        });

        Assert.Equal(2, fakeSender.Messages.Count);
    }

    [Fact]
    public async Task TrySendAsync_RemovesStaleDeviceToken()
    {
        await using var context = CreateContext();
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedFieldWorker(context, organisationId, userId);
        var staleDevice = CreateDevice(organisationId, userId, "device-stale", "integration-fcm-token-stale");
        context.UserDevices.Add(staleDevice);
        await context.SaveChangesAsync();

        var fakeSender = new FakePushNotificationSender(NullLogger<FakePushNotificationSender>.Instance)
        {
            FailNextSendAsStaleToken = true,
        };
        var service = new PushDeliveryService(
            context,
            fakeSender,
            NullLogger<PushDeliveryService>.Instance);

        await service.TrySendAsync(new InAppNotification
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            EventType = NotificationEventTypes.TravelClaimApproved,
            Title = "Approved",
            Body = "Body",
            CaseId = Guid.NewGuid(),
            ResourceType = "TravelClaim",
            ResourceId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        });

        Assert.False(await context.UserDevices.AnyAsync(d => d.Id == staleDevice.Id));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedFieldWorker(AppDbContext context, Guid organisationId, Guid userId)
    {
        var now = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            OrganisationId = organisationId,
            Email = "worker@pilot.example",
            Role = UserRoles.SocialWorker,
            IsActive = true,
            TokenVersion = 1,
            PasswordHash = "hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
    }

    private static UserDevice CreateDevice(
        Guid organisationId,
        Guid userId,
        string installId,
        string pushToken) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            DeviceInstallId = installId,
            Platform = "android",
            PushToken = pushToken,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            LastRegisteredAtUtc = DateTime.UtcNow,
        };
}
