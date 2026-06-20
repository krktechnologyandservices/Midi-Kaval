using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class RbacAuthorizationTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public RbacAuthorizationTests(AuthWebApplicationFactory factory)
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
    public async Task Unauthenticated_Probe_Returns401()
    {
        var getResponse = await _client.GetAsync("/api/v1/rbac-probe/director-only");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);

        var postResponse = await _client.PostAsync(
            "/api/v1/rbac-probe/coordinator-mutation",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_RbacProbe_Returns403_DeactivatedMessage()
    {
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            RbacTestData.SocialWorkerEmail,
            AuthTestData.Password);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var probeClient = _factory.CreateClient();
        probeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await probeClient.GetAsync("/api/v1/rbac-probe/field-action");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.DeactivatedMessage, problem?.Detail);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData(RbacTestData.CoordinatorEmail, "/api/v1/rbac-probe/director-only", "GET")]
    [InlineData(RbacTestData.SocialWorkerEmail, "/api/v1/rbac-probe/coordinator-mutation", "POST")]
    [InlineData(RbacTestData.CaseWorkerEmail, "/api/v1/rbac-probe/coordinator-mutation", "POST")]
    [InlineData(AuthTestData.Email, "/api/v1/rbac-probe/field-action", "GET")]
    public async Task ForbiddenRole_Returns403_ProblemDetails(
        string email,
        string path,
        string method)
    {
        var token = await LoginAndGetAccessTokenAsync(email);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Forbidden", problem?.Title);
        Assert.Equal(Policies.ForbiddenByRoleMessage, problem?.Detail);
    }

    [Fact]
    public async Task Director_CanAccess_CoordinatorMutation_And_DirectorOnly()
    {
        var token = await LoginAndGetAccessTokenAsync(AuthTestData.Email);

        var mutation = await SendAuthorizedAsync(HttpMethod.Post, "/api/v1/rbac-probe/coordinator-mutation", token);
        Assert.Equal(HttpStatusCode.NoContent, mutation.StatusCode);

        var directorOnly = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/rbac-probe/director-only", token);
        Assert.Equal(HttpStatusCode.OK, directorOnly.StatusCode);
    }

    [Fact]
    public async Task Coordinator_CanAccess_CoordinatorMutation()
    {
        var token = await LoginAndGetAccessTokenAsync(RbacTestData.CoordinatorEmail);
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/v1/rbac-probe/coordinator-mutation",
            token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(RbacTestData.SocialWorkerEmail)]
    [InlineData(RbacTestData.CaseWorkerEmail)]
    public async Task FieldWorker_CanAccess_FieldAction(string email)
    {
        var token = await LoginAndGetAccessTokenAsync(email);
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/rbac-probe/field-action", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> LoginAndGetAccessTokenAsync(string email)
    {
        _factory.EmailSender.Clear();
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            _client,
            _factory.EmailSender,
            email,
            AuthTestData.Password);
        return session.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}

internal static class RbacTestData
{
    public const string CoordinatorEmail = "coordinator@rbac.test";
    public const string SocialWorkerEmail = "social@rbac.test";
    public const string CaseWorkerEmail = "case@rbac.test";

    public static async Task EnsureRoleUsersAsync(AuthWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await EnsureUserAsync(db, passwordHasher, CoordinatorEmail, UserRoles.Coordinator);
        await EnsureUserAsync(db, passwordHasher, SocialWorkerEmail, UserRoles.SocialWorker);
        await EnsureUserAsync(db, passwordHasher, CaseWorkerEmail, UserRoles.CaseWorker);
    }

    private static async Task EnsureUserAsync(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        string email,
        string role)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(
            u => u.OrganisationId == AuthTestData.OrganisationId && u.Email == normalizedEmail);

        if (existing is not null)
        {
            existing.Role = role;
            existing.IsActive = true;
            existing.TokenVersion = 0;
            existing.PasswordHash = passwordHasher.HashPassword(existing, AuthTestData.Password);
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = AuthTestData.OrganisationId,
            Email = normalizedEmail,
            Role = role,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, AuthTestData.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
