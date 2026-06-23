using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class DataRateLimitingTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _defaultClient = null!;

    public DataRateLimitingTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _defaultClient = _factory.CreateClient();
        _factory.EmailSender.Clear();
        await RbacTestData.EnsureRoleUsersAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpRequestMessage AuthorizedGet(string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static HttpRequestMessage AuthorizedPost(string path, string accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private HttpClient CreateRateLimitedClient(int readLimit = 5, int writeLimit = 3, int windowSeconds = 60)
    {
        // Set rate limit env vars to low values so we can trigger 429 without sending 100+ requests.
        // These override the factory defaults (10000 req/min) for this test's host pipeline.
        var overrideClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataRateLimiting:ReadPermitLimit", readLimit.ToString());
            builder.UseSetting("DataRateLimiting:WritePermitLimit", writeLimit.ToString());
            builder.UseSetting("DataRateLimiting:WindowSeconds", windowSeconds.ToString());
        }).CreateClient();

        return overrideClient;
    }

    [Fact]
    public async Task ReadEndpoint_ExceedsLimit_Returns429_WithProblemDetails()
    {
        var client = CreateRateLimitedClient(readLimit: 5, writeLimit: 3);
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            client, _factory.EmailSender, RbacTestData.CoordinatorEmail, AuthTestData.Password);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.SendAsync(AuthorizedGet("/api/v1/cases/search-presets", session.AccessToken));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await client.SendAsync(AuthorizedGet("/api/v1/cases/search-presets", session.AccessToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal("application/problem+json", limitedResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(limitedResponse.Headers.Contains("Retry-After"), "Response should include Retry-After header");

        var problem = await limitedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
    }

    [Fact]
    public async Task WriteEndpoint_ExceedsLimit_Returns429()
    {
        var client = CreateRateLimitedClient(readLimit: 5, writeLimit: 3);
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            client, _factory.EmailSender, RbacTestData.CoordinatorEmail, AuthTestData.Password);

        for (var i = 0; i < 3; i++)
        {
            var payload = CaseTestData.BuildValidRequest();
            var response = await client.SendAsync(
                AuthorizedPost("/api/v1/cases", session.AccessToken, payload));

            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"Expected success, got {response.StatusCode}");
        }

        var limitedPayload = CaseTestData.BuildValidRequest();
        var limitedResponse = await client.SendAsync(
            AuthorizedPost("/api/v1/cases", session.AccessToken, limitedPayload));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal("application/problem+json", limitedResponse.Content.Headers.ContentType?.MediaType);

        var problem = await limitedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
    }

    [Fact]
    public async Task DirectorBypass_No429()
    {
        var client = CreateRateLimitedClient(readLimit: 3, writeLimit: 3);

        var directorSession = await AuthTestHelpers.LoginAndVerifyAsync(
            client, _factory.EmailSender, AuthTestData.Email, AuthTestData.Password);

        for (var i = 0; i < 10; i++)
        {
            var response = await client.SendAsync(
                AuthorizedGet("/api/v1/cases/search-presets", directorSession.AccessToken));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task ExcludedEndpoints_NotRateLimited()
    {
        var client = CreateRateLimitedClient(readLimit: 3, writeLimit: 3);

        for (var i = 0; i < 10; i++)
        {
            var cspReport = new Dictionary<string, object?>
            {
                ["csp-report"] = new Dictionary<string, object?>
                {
                    ["violated-directive"] = "script-src 'self'",
                },
            };
            var response = await client.PostAsJsonAsync("/api/v1/security/csp-violation", cspReport);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/api/v1/meta");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task AfterWindowExpires_RequestSucceeds()
    {
        var client = CreateRateLimitedClient(readLimit: 3, writeLimit: 3, windowSeconds: 1);
        var session = await AuthTestHelpers.LoginAndVerifyAsync(
            client, _factory.EmailSender, RbacTestData.CoordinatorEmail, AuthTestData.Password);

        for (var i = 0; i < 3; i++)
        {
            await client.SendAsync(AuthorizedGet("/api/v1/cases/search-presets", session.AccessToken));
        }

        var blockedResponse = await client.SendAsync(
            AuthorizedGet("/api/v1/cases/search-presets", session.AccessToken));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);

        await Task.Delay(1500);

        var successResponse = await client.SendAsync(
            AuthorizedGet("/api/v1/cases/search-presets", session.AccessToken));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, successResponse.StatusCode);
    }
}
