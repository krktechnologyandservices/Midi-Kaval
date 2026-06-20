using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure.Auth;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>
/// Temporary RBAC probe endpoints until Epic 2 business routes ship.
/// Hidden from OpenAPI — policies are reused by real controllers later.
/// </summary>
[ApiController]
[Route("api/v1/rbac-probe")]
[ApiExplorerSettings(IgnoreApi = true)]
public class RbacProbeController : ControllerBase
{
    [HttpPost("coordinator-mutation")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult CoordinatorMutation() => NoContent();

    [HttpGet("director-only")]
    [Authorize(Policy = Policies.DirectorOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult DirectorOnly() => Ok(new { ok = true });

    [HttpGet("field-action")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult FieldAction() => Ok(new { ok = true });
}
