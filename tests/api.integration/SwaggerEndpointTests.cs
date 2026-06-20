using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MidiKaval.Api.IntegrationTests;

public class SwaggerEndpointTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SwaggerEndpointTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_Swagger_WhenRequested()
    {
        var exportPath = Environment.GetEnvironmentVariable("EXPORT_OPENAPI_PATH");
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return;
        }

        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var fullPath = Path.IsPathRooted(exportPath)
            ? exportPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", exportPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, json);
    }

    [Fact]
    public async Task Get_SwaggerJson_Returns200_ContainingMetaRoute()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/meta", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/login", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/verify-otp", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/refresh", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/logout", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/me", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/forgot-password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/auth/reset-password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/stage", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("check-duplicate", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("merge", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/search", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/search/export", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search-presets", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/assigned", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/users/field-workers", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assignedWorkerUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transfer", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/visits/today", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/sync/push", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/visits/weekly", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/visits/overdue", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/{id}/visits", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/{id}/notes", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/cases/{id}/court-sittings", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/court-sittings/upcoming", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/travel-claims", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/travel-claims/{id}/approve", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/travel-claims/{id}/return", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/devices/me", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/notifications/preferences", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/director/travel-claims/pending", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/supervisor/travel-claims/{id}", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/supervisor/travel-claims/monthly-totals", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/attachments/presign", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/attachments/confirm", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/attachments/{id}/download-url", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/v1/rbac-probe", json, StringComparison.OrdinalIgnoreCase);
    }
}
