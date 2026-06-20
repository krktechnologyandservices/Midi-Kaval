using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Models;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>API metadata and version information.</summary>
[ApiController]
[Route("api/v1/meta")]
[Produces("application/json")]
public class MetaController : ControllerBase
{
    /// <summary>Returns API version metadata wrapped in the standard envelope.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<MetaDto>), StatusCodes.Status200OK)]
    public ActionResult<MetaDto> Get() => Ok(new MetaDto("0.1.0", "v1"));
}
