using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Users;
using MidiKaval.Api.Models.Cases;
using Microsoft.AspNetCore.RateLimiting;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[EnableRateLimiting("data-read")]
[Route("api/v1/users")]
public sealed class UsersController(UserQueryService userQueryService) : ControllerBase
{
    [HttpGet("field-workers")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<FieldWorkerUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListFieldWorkers(CancellationToken cancellationToken)
    {
        var workers = await userQueryService.ListFieldWorkersAsync(cancellationToken);
        return Ok(workers);
    }
}
