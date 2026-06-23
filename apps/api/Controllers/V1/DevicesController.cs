using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Device registration for push notification delivery.</summary>
[ApiController]
[Authorize]
[Route("api/v1/devices")]
public sealed class DevicesController(UserDeviceService userDeviceService) : ControllerBase
{
    /// <summary>Register or update the current user's device push token.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> RegisterMe(
        [FromBody] RegisterUserDeviceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await userDeviceService.RegisterAsync(request, cancellationToken);
            return Ok(dto);
        }
        catch (UserDeviceValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    private IActionResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
