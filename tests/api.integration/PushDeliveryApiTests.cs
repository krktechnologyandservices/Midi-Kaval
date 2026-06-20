using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Models.Notifications;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class PushDeliveryApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public PushDeliveryApiTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        _factory.PushSender.Clear();
        await RbacTestData.EnsureRoleUsersAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ApproveTravelClaim_SendsPushToRegisteredDevice()
    {
        var submitted = await CaseTestData.CreateSubmittedTravelClaimWithReceiptAsync(
            _client,
            _factory,
            RbacTestData.SocialWorkerEmail);

        var fieldSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var pushToken = $"integration-fcm-token-{Guid.NewGuid():N}";
        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            fieldSession.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "push-delivery-device",
                Platform = "android",
                PushToken = pushToken,
            });

        var director = await VisitTestData.BuildDirectorSessionAsync(_client, _factory.EmailSender);
        await CaseTestData.ApproveTravelClaimAsync(_client, director.AccessToken, submitted.Id);

        var message = Assert.Single(_factory.PushSender.Messages);
        Assert.Equal("travel.claim.approved", message.Data["eventType"]);
        Assert.Equal(submitted.Id.ToString("D"), message.Data["resourceId"]);
        Assert.Equal(pushToken, message.Device.PushToken);
        Assert.Equal("Travel claim approved", message.Title);
    }
}
