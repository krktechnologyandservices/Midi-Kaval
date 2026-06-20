using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Auth;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class DeviceRegistrationApiTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public DeviceRegistrationApiTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await RbacTestData.EnsureRoleUsersAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterDevice_UpsertsForSameDeviceInstallId()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var first = await CaseTestData.RegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "device-install-1",
                Platform = "android",
                PushToken = "token-v1",
            });

        var second = await CaseTestData.RegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "device-install-1",
                Platform = "android",
                PushToken = "token-v2",
            });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("token-v2", await GetPushTokenAsync(session.UserId, "device-install-1"));
    }

    [Fact]
    public async Task RegisterDevice_EmptyFields_Returns400()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendRegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "",
                Platform = "android",
                PushToken = "token",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_OversizedFields_Returns400()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var oversizeInstallId = new string('a', 65);
        var installIdResponse = await CaseTestData.SendRegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = oversizeInstallId,
                Platform = "android",
                PushToken = "token",
            });
        Assert.Equal(HttpStatusCode.BadRequest, installIdResponse.StatusCode);

        var oversizeToken = new string('b', 513);
        var tokenResponse = await CaseTestData.SendRegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "device-install-oversize",
                Platform = "android",
                PushToken = oversizeToken,
            });
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_InvalidPlatform_Returns400()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var response = await CaseTestData.SendRegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "device-install-2",
                Platform = "web",
                PushToken = "token",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_TokenHandoff_DeletesPriorUserRow()
    {
        var firstSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            firstSession.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "shared-device",
                Platform = "android",
                PushToken = "shared-token",
            });

        var secondSession = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CaseWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            secondSession.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "shared-device-2",
                Platform = "android",
                PushToken = "shared-token",
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.UserDevices
            .Where(d => d.PushToken == "shared-token")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(secondSession.UserId, rows[0].UserId);
    }

    [Fact]
    public async Task Logout_WithDeviceInstallId_RemovesDeviceRow()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "logout-device",
                Platform = "ios",
                PushToken = "logout-token",
            });

        var logoutResponse = await CaseTestData.LogoutWithDeviceAsync(
            _client,
            session.RefreshToken,
            "logout-device");
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.UserDevices.CountAsync(
            d => d.UserId == session.UserId && d.DeviceInstallId == "logout-device");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Preferences_FieldWorker_ReturnsPushDefaults()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        var prefs = await CaseTestData.GetNotificationPreferencesAsync(_client, session.AccessToken);

        Assert.True(prefs.PushEnabled);
        Assert.False(prefs.EmailEnabled);
        Assert.True(prefs.Channels.Visits);
        Assert.True(prefs.Channels.Court);
        Assert.True(prefs.Channels.Interventions);
        Assert.True(prefs.Channels.Claims);
    }

    [Fact]
    public async Task Preferences_Coordinator_ReturnsEmailDefaults()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.CoordinatorEmail,
            AuthTestData.Password);

        var prefs = await CaseTestData.GetNotificationPreferencesAsync(_client, session.AccessToken);

        Assert.False(prefs.PushEnabled);
        Assert.True(prefs.EmailEnabled);
        Assert.False(prefs.Channels.Visits);
        Assert.True(prefs.Channels.Reports);
        Assert.True(prefs.Channels.Assignments);
    }

    [Fact]
    public async Task InvalidateUserSessions_RemovesAllDeviceRows()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "invalidate-device-1",
                Platform = "android",
                PushToken = "invalidate-token-1",
            });

        await CaseTestData.RegisterUserDeviceAsync(
            _client,
            session.AccessToken,
            new RegisterUserDeviceRequest
            {
                DeviceInstallId = "invalidate-device-2",
                Platform = "ios",
                PushToken = "invalidate-token-2",
            });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
            await sessionService.InvalidateUserSessionsAsync(session.UserId);
        }

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.UserDevices.CountAsync(d => d.UserId == session.UserId);
        Assert.Equal(0, count);
    }

    private async Task<string> GetPushTokenAsync(Guid userId, string deviceInstallId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.UserDevices.SingleAsync(
            d => d.UserId == userId && d.DeviceInstallId == deviceInstallId);
        return row.PushToken;
    }
}
