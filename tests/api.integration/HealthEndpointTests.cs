using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MidiKaval.Api.IntegrationTests;

public class HealthEndpointTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Health_Returns200_WithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
    }

    private sealed record HealthResponse(string Status);
}
