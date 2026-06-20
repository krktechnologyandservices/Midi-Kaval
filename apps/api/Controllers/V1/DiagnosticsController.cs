using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Development-only diagnostics (not for production).</summary>
[ApiController]
[Route("api/v1/diagnostics")]
[ApiExplorerSettings(IgnoreApi = true)]
public class DiagnosticsController(IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Throws an unhandled exception for Problem Details testing.</summary>
    [HttpGet("throw")]
    public IActionResult Throw()
    {
        if (!environment.IsDevelopment() && !environment.IsTesting())
        {
            return NotFound();
        }

        throw new InvalidOperationException("Diagnostics test exception.");
    }
}
