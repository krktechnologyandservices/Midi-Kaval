using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Reports;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
[Produces("application/json")]
public class SocioDemographicReportsController : ControllerBase
{
    private readonly SocioDemographicProfileService socioDemographicProfileService;
    private readonly SocioDemographicProfileExcelService socioDemographicProfileExcelService;

    public SocioDemographicReportsController(
        SocioDemographicProfileService socioDemographicProfileService,
        SocioDemographicProfileExcelService socioDemographicProfileExcelService)
    {
        this.socioDemographicProfileService = socioDemographicProfileService;
        this.socioDemographicProfileExcelService = socioDemographicProfileExcelService;
    }

    /// <summary>
    /// Generate socio-demographic profile report as Excel.
    /// </summary>
    [HttpGet("socio-demographic-profile")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSocioDemographicProfile(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken ct = default)
    {
        if (month < 1 || month > 12)
        {
            return BadRequest(new { error = "Month must be between 1 and 12." });
        }

        if (year < 2000 || year > 2100)
        {
            return BadRequest(new { error = "Year must be between 2000 and 2100." });
        }

        var report = await socioDemographicProfileService.BuildReportAsync(month, year, ct);

        var bytes = socioDemographicProfileExcelService.Generate(report, DateTime.UtcNow);
        var fileName = $"Socio-Demographic-Profile-{month}-{year}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
