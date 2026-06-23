using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MidiKaval.Api.IntegrationTests;

[Collection("AuthIntegration")]
public class SecurityControllerTests : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public SecurityControllerTests(AuthWebApplicationFactory factory)
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
    public async Task AllEndpoints_IncludeContentSecurityPolicyHeader()
    {
        var response = await _client.GetAsync("/api/v1/meta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Content-Security-Policy"),
            "Response is missing Content-Security-Policy header");

        var cspHeader = response.Headers
            .Single(h => h.Key.Equals("Content-Security-Policy", StringComparison.OrdinalIgnoreCase))
            .Value.Single();

        Assert.Contains("default-src 'self'", cspHeader);
        Assert.Contains("script-src 'self' 'unsafe-inline'", cspHeader);
        Assert.Contains("style-src 'self' 'unsafe-inline'", cspHeader);
        Assert.Contains("img-src 'self' data: blob:", cspHeader);
        Assert.Contains("object-src 'none'", cspHeader);
        Assert.Contains("base-uri 'self'", cspHeader);
        Assert.Contains("form-action 'self'", cspHeader);
        Assert.Contains("frame-ancestors 'self'", cspHeader);
        Assert.Contains("report-uri /api/v1/security/csp-violation", cspHeader);
    }

    [Fact]
    public async Task ReportCspViolation_ValidBody_Returns204()
    {
        var cspReport = new Dictionary<string, object?>
        {
            ["csp-report"] = new Dictionary<string, object?>
            {
                ["document-uri"] = "https://kaval.local/",
                ["violated-directive"] = "script-src 'self'",
                ["blocked-uri"] = "https://evil.example.com/hack.js",
            },
        };

        var response = await _client.PostAsJsonAsync("/api/v1/security/csp-violation", cspReport);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ReportCspViolation_EmptyBody_Returns400()
    {
        var content = new StringContent("", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/security/csp-violation", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportCspViolation_InvalidBody_Returns400()
    {
        var content = new StringContent("not valid json", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/security/csp-violation", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportCspViolation_NoAuthRequired_Returns204()
    {
        var cspReport = new Dictionary<string, object?>
        {
            ["csp-report"] = new Dictionary<string, object?>
            {
                ["document-uri"] = "https://kaval.local/",
                ["violated-directive"] = "img-src 'self'",
            },
        };

        // No auth header set — should still succeed (CSP reports come from browsers)
        var response = await _client.PostAsJsonAsync("/api/v1/security/csp-violation", cspReport);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ReportCspViolation_ApplicationCspReportContentType_Returns204()
    {
        var cspReport = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["csp-report"] = new Dictionary<string, object?>
            {
                ["violated-directive"] = "script-src 'self'",
            },
        });

        var content = new StringContent(cspReport, Encoding.UTF8, "application/csp-report");
        var response = await _client.PostAsync("/api/v1/security/csp-violation", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
