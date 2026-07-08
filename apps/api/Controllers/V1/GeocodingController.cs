using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Geocoding;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Geocoding;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Proxies free-text address search to OpenStreetMap's Nominatim service, used
/// when a coordinator picks a place to visit while scheduling a visit.</summary>
[ApiController]
[Route("api/v1/geocoding")]
[Authorize(Policy = Policies.CoordinatorOrAbove)]
public sealed class GeocodingController(IGeocodingService geocodingService) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<GeocodingSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequestProblem("q is required.");
        }

        var items = await geocodingService.SearchAsync(q, cancellationToken);
        var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

        return Ok(new ApiResponse<GeocodingSearchResultDto>(
            new GeocodingSearchResultDto { Items = items },
            new ApiMeta { RequestId = requestId }));
    }

    private ObjectResult BadRequestProblem(string detail) =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid request",
            detail: detail);
}
