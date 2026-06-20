using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
public class PasswordResetTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private const string NewPassword = "NewSecurePassword99!";

    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public PasswordResetTests(AuthWebApplicationFactory factory)
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
        user.PasswordHash = factory.Services
            .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>()
            .HashPassword(user, AuthTestData.Password);
        await db.SaveChangesAsync();
        await sessionService.InvalidateUserSessionsAsync(user.Id);
        user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.TokenVersion = 0;
        user.IsActive = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ForgotPassword_ThenReset_ThenLoginWithNewPassword_Succeeds()
    {
        var forgotResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = AuthTestData.Email });

        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);
        var forgotEnvelope = await forgotResponse.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
        Assert.Equal(AuthService.ForgotPasswordSuccessMessage, forgotEnvelope?.Data?.Message);

        var resetToken = ExtractResetTokenFromEmail(_factory.EmailSender.LastMessage);
        Assert.NotNull(resetToken);

        var resetResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = resetToken, NewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var resetEnvelope = await resetResponse.Content.ReadFromJsonAsync<ApiEnvelope<ResetPasswordResponse>>();
        Assert.Equal(AuthService.ResetPasswordSuccessMessage, resetEnvelope?.Data?.Message);

        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            AuthTestData.Email,
            NewPassword);
        Assert.NotEqual(Guid.Empty, session.UserId);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsGeneric200_NoEmailSent()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = "nobody@pilot.example" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
        Assert.Equal(AuthService.ForgotPasswordSuccessMessage, envelope?.Data?.Message);
        Assert.Null(_factory.EmailSender.LastMessage);
    }

    [Fact]
    public async Task ForgotPassword_EmptyEmail_ReturnsGeneric200()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
        Assert.Equal(AuthService.ForgotPasswordSuccessMessage, envelope?.Data?.Message);
    }

    [Fact]
    public async Task ForgotPassword_DeactivatedUser_ReturnsGeneric200_NoEmailSent()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);
        user.IsActive = false;
        await db.SaveChangesAsync();

        try
        {
            _factory.EmailSender.Clear();
            var response = await _client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password",
                new ForgotPasswordRequest { Email = AuthTestData.Email });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
            Assert.Equal(AuthService.ForgotPasswordSuccessMessage, envelope?.Data?.Message);
            Assert.Null(_factory.EmailSender.LastMessage);
        }
        finally
        {
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ResetPassword_ShortPassword_Returns400_WithoutConsumingToken()
    {
        var token = await IssueResetTokenAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var retry = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_UnknownToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = "not-a-real-token", NewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_UsedToken_Returns400()
    {
        var token = await IssueResetTokenAsync();

        var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await ResetAuthTestUserAsync(_factory);

        var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = "AnotherPassword99!" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidatesExistingRefreshTokens()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(_client, _factory.EmailSender);
        var token = await IssueResetTokenAsync();

        var resetResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WritesAuditEvent_ForActiveUser()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);

        var before = await db.AuditEvents.CountAsync(e =>
            e.EventType == AuditEventTypes.PasswordResetRequested && e.SubjectUserId == user.Id);

        await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = AuthTestData.Email });

        var after = await db.AuditEvents.CountAsync(e =>
            e.EventType == AuditEventTypes.PasswordResetRequested && e.SubjectUserId == user.Id);
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task ResetPassword_WritesCompletedAndSessionInvalidatedAuditEvents()
    {
        var token = await IssueResetTokenAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == AuthTestData.Email);

        await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = NewPassword });

        Assert.Contains(
            await db.AuditEvents.Select(e => e.EventType).ToListAsync(),
            t => t == AuditEventTypes.PasswordResetCompleted);
        Assert.Contains(
            await db.AuditEvents.Select(e => e.EventType).ToListAsync(),
            t => t == AuditEventTypes.SessionInvalidated);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(updated.TokenVersion > 0);
    }

    [Fact]
    public async Task ForgotPassword_EmailFailure_ReturnsGeneric200()
    {
        _factory.EmailSender.FailNextSend = true;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = AuthTestData.Email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
        Assert.Equal(AuthService.ForgotPasswordSuccessMessage, envelope?.Data?.Message);
        Assert.Null(_factory.EmailSender.LastMessage);
    }

    [Fact]
    public async Task ForgotPassword_NullBody_ReturnsGeneric200()
    {
        var response = await _client.PostAsync(
            "/api/v1/auth/forgot-password",
            JsonContent.Create<ForgotPasswordRequest?>(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ForgotPasswordResponse>>();
        Assert.Equal(AuthService.ForgotPasswordSuccessMessage, envelope?.Data?.Message);
    }

    [Fact]
    public async Task ResetPassword_NullBody_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/v1/auth/reset-password",
            JsonContent.Create<ResetPasswordRequest?>(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> IssueResetTokenAsync()
    {
        _factory.EmailSender.Clear();
        await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = AuthTestData.Email });

        var token = ExtractResetTokenFromEmail(_factory.EmailSender.LastMessage);
        Assert.NotNull(token);
        return token;
    }

    private static string? ExtractResetTokenFromEmail(EmailMessage? message)
    {
        if (message is null)
        {
            return null;
        }

        var match = Regex.Match(message.Body, @"token=([^&\s]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
