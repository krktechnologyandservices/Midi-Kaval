using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AuthSessionTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthSessionTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return ResetAuthTestUserAsync(_factory);
    }

    public Task DisposeAsync() => ResetAuthTestUserAsync(_factory);

    private static async Task ResetAuthTestUserAsync(AuthWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessionService = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.IsActive = true;
        await db.SaveChangesAsync();
        await sessionService.InvalidateUserSessionsAsync(user.Id);
        user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.TokenVersion = 0;
        user.IsActive = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Refresh_WithBody_ReturnsNewTokens()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var envelope = await refreshResponse.Content.ReadFromJsonAsync<ApiEnvelope<RefreshResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.RefreshToken));
        Assert.Equal(900, envelope.Data.ExpiresIn);
        Assert.NotEqual(session.RefreshToken, envelope.Data.RefreshToken);

        var oldTokenRefresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithCookie_ReturnsNewTokens()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        var cookieClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        cookieClient.DefaultRequestHeaders.Add("Cookie", session.RefreshCookieHeader);

        var refreshResponse = await cookieClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest());

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var envelope = await refreshResponse.Content.ReadFromJsonAsync<ApiEnvelope<RefreshResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.RefreshToken));
        Assert.Contains(
            refreshResponse.Headers,
            h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                && h.Value.Any(v => v.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase)
                    && v.Contains("httponly", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = "not-a-valid-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndClearsCookie()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        var logoutResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new RefreshRequest { RefreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Contains(
            logoutResponse.Headers,
            h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                && h.Value.Any(v => v.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase)
                    && v.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)));

        var refreshAfterLogout = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidAccessToken_ReturnsUserProfile()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        var meClient = _factory.CreateClient();
        meClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await meClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var envelope = await meResponse.Content.ReadFromJsonAsync<ApiEnvelope<SessionUserDto>>();
        Assert.NotNull(envelope?.Data);
        Assert.Equal(AuthTestData.Email, envelope.Data.Email);
        Assert.Equal(UserRoles.Director, envelope.Data.Role);
    }

    [Fact]
    public async Task Me_AfterTokenVersionBump_Returns401()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
            await sessionService.InvalidateUserSessionsAsync(session.UserId);
        }

        var meClient = _factory.CreateClient();
        meClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await meClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Me_DeactivatedUser_Returns403_WithCoordinatorMessage()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var meClient = _factory.CreateClient();
        meClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await meClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, meResponse.StatusCode);
        var body = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("Contact your coordinator", body, StringComparison.OrdinalIgnoreCase);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Refresh_AfterTokenVersionBump_Returns401()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
            await sessionService.InvalidateUserSessionsAsync(session.UserId);
        }

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_DeactivatedUser_Returns403_WithoutBurningToken()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, refreshResponse.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }

        var retryAfterReactivate = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, retryAfterReactivate.StatusCode);
    }

    [Fact]
    public async Task LoginAndLogout_WritesAuditEvents()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var initialCount = await db.AuditEvents.CountAsync();

        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);

        var afterLoginCount = await db.AuditEvents.CountAsync();
        Assert.True(afterLoginCount > initialCount);
        Assert.Contains(
            await db.AuditEvents.Select(e => e.EventType).ToListAsync(),
            t => t == AuditEventTypes.LoginSuccess);

        await _client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new RefreshRequest { RefreshToken = session.RefreshToken });

        var afterLogoutCount = await db.AuditEvents.CountAsync();
        Assert.True(afterLogoutCount > afterLoginCount);
        Assert.Contains(
            await db.AuditEvents.Select(e => e.EventType).ToListAsync(),
            t => t == AuditEventTypes.Logout);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_RevokesAllSessions()
    {
        var noCookieClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var session = await AuthTestHelpers.LoginAndVerifyAsync(noCookieClient, _factory.EmailSender);
        var oldRefresh = session.RefreshToken;

        var firstRefresh = await noCookieClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var reuseAttempt = await noCookieClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        var envelope = await firstRefresh.Content.ReadFromJsonAsync<ApiEnvelope<RefreshResponse>>();
        var secondRefresh = await noCookieClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = envelope!.Data!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}

internal static class AuthTestHelpers
{
    public static Task<AuthSession> LoginAndVerifyAsync(
        HttpClient client,
        FakeEmailSender emailSender) =>
        LoginAndVerifyAsync(client, emailSender, AuthTestData.Email, AuthTestData.Password);

    public static async Task<AuthSession> LoginAndVerifyAsync(
        HttpClient client,
        FakeEmailSender emailSender,
        string email,
        string password)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();

        var otp = ExtractOtpFromEmail(emailSender.LastMessage);
        Assert.NotNull(otp);

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest { ChallengeId = loginEnvelope!.Data!.ChallengeId.Value, Code = otp });
        verifyResponse.EnsureSuccessStatusCode();

        var verifyEnvelope = await verifyResponse.Content.ReadFromJsonAsync<ApiEnvelope<VerifyOtpResponse>>();
        Assert.NotNull(verifyEnvelope?.Data);

        var cookieHeader = verifyResponse.Headers
            .Single(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .Value.Single(v => v.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase))
            .Split(';')[0];

        return new AuthSession(
            verifyEnvelope.Data.AccessToken,
            verifyEnvelope.Data.RefreshToken,
            verifyEnvelope.Data.User.Id,
            cookieHeader);
    }

    private static string? ExtractOtpFromEmail(EmailMessage? message)
    {
        if (message is null)
        {
            return null;
        }

        var match = Regex.Match(message.Body, @"Your verification code is:\s*(\d{6})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}

internal sealed record AuthSession(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string RefreshCookieHeader);
