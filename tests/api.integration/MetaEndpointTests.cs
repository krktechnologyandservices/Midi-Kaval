using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MidiKaval.Api.IntegrationTests;

public class MetaEndpointTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MetaEndpointTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Meta_Returns200_WithEnvelope()
    {
        var response = await _client.GetAsync("/api/v1/meta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<MetaData>>();
        Assert.NotNull(body);
        Assert.NotNull(body.Data);
        Assert.Equal("0.1.0", body.Data.Version);
        Assert.Equal("v1", body.Data.Api);
        Assert.False(string.IsNullOrWhiteSpace(body.Meta.RequestId));
        Assert.True(Guid.TryParse(body.Meta.RequestId, out _));
    }

    private sealed record MetaData(string Version, string Api);

    private sealed record ApiMeta(string RequestId);

    private sealed record ApiEnvelope<T>(T Data, ApiMeta Meta);
}
