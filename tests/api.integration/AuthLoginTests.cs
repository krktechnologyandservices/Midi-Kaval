using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class AuthLoginTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthLoginTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EmailSender.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_ThenVerifyOtp_ReturnsJwt_WithExpectedClaims()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = AuthTestData.Email, Password = AuthTestData.Password });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();
        Assert.NotNull(loginEnvelope?.Data);
        Assert.NotEqual(Guid.Empty, loginEnvelope.Data.ChallengeId);

        var otp = ExtractOtpFromEmail(_factory.EmailSender.LastMessage);
        Assert.NotNull(otp);

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest { ChallengeId = loginEnvelope.Data.ChallengeId!.Value, Code = otp });

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyEnvelope = await verifyResponse.Content.ReadFromJsonAsync<ApiEnvelope<VerifyOtpResponse>>();
        Assert.NotNull(verifyEnvelope?.Data);
        Assert.False(string.IsNullOrWhiteSpace(verifyEnvelope.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(verifyEnvelope.Data.RefreshToken));
        Assert.Equal(900, verifyEnvelope.Data.ExpiresIn);
        Assert.Equal(AuthTestData.Email, verifyEnvelope.Data.User.Email);
        Assert.Equal(UserRoles.Director, verifyEnvelope.Data.User.Role);

        Assert.Contains(
            verifyResponse.Headers,
            h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                && h.Value.Any(v => v.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase)
                    && v.Contains("httponly", StringComparison.OrdinalIgnoreCase)));

        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('k', 32)));
        handler.ValidateToken(
            verifyEnvelope.Data.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "test-issuer",
                ValidAudience = "test-audience",
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1),
            },
            out _);

        var jwt = handler.ReadJwtToken(verifyEnvelope.Data.AccessToken);
        Assert.Equal(verifyEnvelope.Data.User.Id.ToString("D"), jwt.Subject);
        Assert.Equal(AuthTestData.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(UserRoles.Director, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(AuthTestData.OrganisationId.ToString("D"), jwt.Claims.First(c => c.Type == AuthClaimTypes.OrganisationId).Value);
        Assert.Equal("0", jwt.Claims.First(c => c.Type == AuthClaimTypes.TokenVersion).Value);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_ProblemDetails_WithGenericMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = AuthTestData.Email, Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthService.InvalidCredentialsMessage, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401_ProblemDetails_WithGenericMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "nobody@pilot.example", Password = "any-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthService.InvalidCredentialsMessage, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyOtp_WithWrongCode_Returns401_ProblemDetails()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = AuthTestData.Email, Password = AuthTestData.Password });
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest { ChallengeId = loginEnvelope!.Data!.ChallengeId.Value, Code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
        Assert.Equal("application/problem+json", verifyResponse.Content.Headers.ContentType?.MediaType);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(
            await db.AuditEvents.Select(e => e.EventType).ToListAsync(),
            t => t == AuditEventTypes.OtpFailed);
    }

    [Fact]
    public async Task VerifyOtp_WithInvalidChallengeId_Returns401_ProblemDetails()
    {
        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest { ChallengeId = Guid.NewGuid(), Code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
        Assert.Equal("application/problem+json", verifyResponse.Content.Headers.ContentType?.MediaType);

        var body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains(AuthService.InvalidOtpMessage, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_DeactivatedUser_Returns403_WithCoordinatorMessage()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.IsActive = false;
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = AuthTestData.Email, Password = AuthTestData.Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Contact your coordinator", body, StringComparison.OrdinalIgnoreCase);

        user.IsActive = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task VerifyOtp_DeactivatedUser_Returns403_WithCoordinatorMessage()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = AuthTestData.Email, Password = AuthTestData.Password });
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginResponse>>();
        var otp = ExtractOtpFromEmail(_factory.EmailSender.LastMessage);
        Assert.NotNull(otp);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.IsActive = false;
        await db.SaveChangesAsync();

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest { ChallengeId = loginEnvelope!.Data!.ChallengeId.Value, Code = otp });

        Assert.Equal(HttpStatusCode.Forbidden, verifyResponse.StatusCode);
        var body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Contact your coordinator", body, StringComparison.OrdinalIgnoreCase);

        user.IsActive = true;
        await db.SaveChangesAsync();
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

public sealed class AuthRateLimitWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ApplyTestConfiguration()
    {
        base.ApplyTestConfiguration();
        Environment.SetEnvironmentVariable("Auth__RateLimitPermitLimit", "2");
        Environment.SetEnvironmentVariable("Auth__RateLimitWindowSeconds", "60");
    }
}

[Collection("AuthIntegration")]
public class AuthRateLimitTests : IClassFixture<AuthRateLimitWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthRateLimitWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AuthRateLimitTests(AuthRateLimitWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_WhenRateLimitExceeded_Returns429_ProblemDetails()
    {
        for (var i = 0; i < 2; i++)
        {
            var ok = await _client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest { Email = "a@example.com", Password = "x" });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, ok.StatusCode);
        }

        var limited = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = "b@example.com", Password = "y" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }
}
