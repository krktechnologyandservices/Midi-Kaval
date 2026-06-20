using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MidiKaval.Api.IntegrationTests;

public class ProblemDetailsTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_UnknownV1Route_Returns404_ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Fact]
    public async Task Get_DiagnosticsThrow_Returns500_ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/diagnostics/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        Assert.True(
            contentType.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("500", body);
    }

    private sealed record ProblemDetailsDto(int? Status, string? Title, string? Type, string? Detail);
}
