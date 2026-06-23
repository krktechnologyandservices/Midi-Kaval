using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Migration;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Migration;
using Microsoft.AspNetCore.RateLimiting;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Legacy Excel migration endpoints — validate and import.</summary>
[ApiController]
[EnableRateLimiting("data-write")]
[Authorize(Policy = Policies.DirectorOnly)]
[Route("api/v1/migration")]
[Produces("application/json")]
public sealed class MigrationController(
    MappingSpecLoader specLoader,
    MigrationImportService importService,
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<MigrationController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Validate a legacy Excel file against the mapping specification.
    /// This is a read-only operation — no Cases are created in the database.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(MigrationValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Validate(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequestProblem("A non-empty Excel file (.xlsx) is required.");
        }

        if (!file.ContentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequestProblem("File must be an .xlsx Excel file.");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequestProblem("File size must not exceed 10 MB.");
        }

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            MigrationImportService.ValidateFileFormat(stream);

            using var workbook = new XLWorkbook(stream);
            var spec = specLoader.Load();
            var validator = new ColumnMappingValidator(spec);
            var result = validator.Validate(workbook);
            var (organisationId, actorUserId) = ResolveActorContext();
            var now = DateTime.UtcNow;
            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = null,
                EventType = AuditEventTypes.MigrationValidationRun,
                MetadataJson = JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["totalColumns"] = result.TotalColumns,
                        ["matchedColumns"] = result.MatchedColumns,
                        ["unmatchedColumns"] = result.UnmatchedColumns,
                        ["missingRequiredFields"] = result.MissingRequiredFields,
                        ["dataTypeWarnings"] = result.DataTypeWarnings,
                        ["isValid"] = result.IsValid,
                        ["fileName"] = file.FileName,
                    },
                    JsonOptions),
                CreatedAtUtc = now,
            });
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Migration validation run: {TotalCols} cols, {Matched} matched, {Unmatched} unmatched, {Missing} missing, {Warnings} warnings. File: {FileName}",
                result.TotalColumns, result.MatchedColumns, result.UnmatchedColumns, result.MissingRequiredFields, result.DataTypeWarnings, file.FileName);

            return Ok(result);
        }
        catch (CaseValidationException ex)
        {
            logger.LogWarning(ex, "Migration validation failed: {Message}", ex.Message);
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate migration Excel file: {FileName}", file.FileName);
            return Problem(
                detail: "Failed to read the Excel file. Ensure it is a valid .xlsx format.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Unprocessable Entity");
        }
    }

    /// <summary>Import a legacy Excel file, creating Cases from each row.
    /// Supports dry-run mode for previewing results before committing.</summary>
    /// <param name="file">The Excel file (.xlsx) containing legacy case data.</param>
    /// <param name="dryRun">If true, validate rows without writing to the database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(MigrationImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequestProblem("A non-empty Excel file (.xlsx) is required.");
        }

        if (!file.ContentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequestProblem("File must be an .xlsx Excel file.");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequestProblem("File size must not exceed 10 MB.");
        }

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            MigrationImportService.ValidateFileFormat(stream);

            using var workbook = new XLWorkbook(stream);
            var (organisationId, actorUserId) = ResolveActorContext();
            var result = await importService.ImportAsync(
                workbook, dryRun, organisationId, actorUserId, cancellationToken);

            logger.LogInformation(
                "Migration import {Mode}: {Created} created, {Skipped} skipped, {Errors} errors. File: {FileName}",
                dryRun ? "dry-run" : "live", result.Created, result.Skipped.Count, result.Errors.Count, file.FileName);

            return Ok(result);
        }
        catch (CaseValidationException ex)
        {
            logger.LogWarning(ex, "Migration import failed: {Message}", ex.Message);
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Migration import failed: {Message}", ex.Message);
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to import migration Excel file: {FileName}", file.FileName);
            return Problem(
                detail: "An unexpected error occurred during import. Ensure the file is a valid .xlsx format.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Unprocessable Entity");
        }
    }

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new CaseValidationException("No authenticated user context.");

        var orgClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value
            ?? throw new CaseValidationException("Missing organisation claim.");
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new CaseValidationException("Missing user identifier claim.");

        return (Guid.Parse(orgClaim), Guid.Parse(userIdClaim));
    }

    private IActionResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
}
