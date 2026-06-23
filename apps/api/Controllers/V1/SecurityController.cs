using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>
/// Security-related endpoints.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/security")]
[Produces("application/json")]
public class SecurityController : ControllerBase
{
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(ILogger<SecurityController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Accepts CSP violation reports from browsers (CSP Level 2+).
    /// No auth required — CSP reports come from browsers, not authenticated clients (per AD-04).
    /// Reads body as raw string to support both application/json and application/csp-report
    /// Content-Type headers sent by different browser versions.
    /// Logs only known-safe fields at Warning level — avoids logging PII in CSP report fields.
    /// </summary>
    /// <returns>204 No Content per CSP reporting spec.</returns>
    [HttpPost("csp-violation")]
    [RequestSizeLimit(16_384)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportCspViolation()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest("Request body is empty");
        }

        try
        {
            using var report = JsonDocument.Parse(body);

            // Extract only known-safe fields to avoid logging PII (AD-07)
            var cspReport = report.RootElement.TryGetProperty("csp-report", out var csp)
                ? csp
                : report.RootElement;

            var violatedDirective = TryGetString(cspReport, "violated-directive");
            var effectiveDirective = TryGetString(cspReport, "effective-directive");
            var blockedUri = TryGetString(cspReport, "blocked-uri");
            var documentUri = Truncate(TryGetString(cspReport, "document-uri"), 500);

            _logger.LogWarning(
                "CSP violation: violated-directive={ViolatedDirective}, effective-directive={EffectiveDirective}, " +
                "blocked-uri={BlockedUri}, document-uri={DocumentUri}",
                violatedDirective, effectiveDirective, blockedUri, documentUri);

            return NoContent();
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON in request body");
        }
    }

    private static string TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? "(null)"
            : "(none)";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
