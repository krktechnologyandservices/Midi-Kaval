using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Reports;
using MidiKaval.Api.Infrastructure.Storage;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Reports;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Report export endpoints — start async exports and check status.</summary>
[ApiController]
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[Route("api/v1/reports")]
[Produces("application/json")]
public sealed class ReportsController(
    AppDbContext db,
    IBlobStorageService blobService,
    IOptions<ReportExportOptions> options,
    ILogger<ReportsController> logger) : ControllerBase
{
    /// <summary>Start an async report export job. Returns 202 Accepted.</summary>
    [HttpPost("{type}/export")]
    [ProducesResponseType(typeof(ApiResponse<ReportExportJobDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> StartExport(
        string type,
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var reportType = ReportTypeExtensions.FromApiString(type);
        if (reportType is null)
        {
            return BadRequest(new ProblemDetails
            {
                Detail = $"Invalid report type '{type}'. Valid types: {string.Join(", ", ReportTypeExtensions.All)}",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var format = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
        if (format != "excel" && format != "pdf")
        {
            return BadRequest(new ProblemDetails
            {
                Detail = "Format must be 'excel' or 'pdf'",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();

        var job = new ReportExportJob
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            CreatedByUserId = userId,
            ReportType = reportType.Value.ToApiString(),
            Format = format,
            Status = ReportExportJobStatus.Pending,
            FromDate = request.From,
            ToDate = request.To,
            Year = request.Year,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Set<ReportExportJob>().Add(job);
        await db.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(job, null);
        var requestId = ResolveRequestId();

        return Accepted(new ApiResponse<ReportExportJobDto>(dto, new ApiMeta { RequestId = requestId }));
    }

    /// <summary>Get the status of a report export job.</summary>
    [HttpGet("exports/{jobId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ReportExportStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> GetExportStatus(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();

        var job = await db.Set<ReportExportJob>()
            .Where(j => j.Id == jobId && j.OrganisationId == organisationId && j.CreatedByUserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        string? downloadUrl = null;
        if (job.Status == ReportExportJobStatus.Completed && job.BlobPath is not null)
        {
            try
            {
                var (url, expiresAt) = blobService.GenerateReadSasUri(job.BlobPath);
                if (expiresAt < DateTime.UtcNow)
                {
                    return Problem(
                        detail: "The download URL has expired.",
                        statusCode: StatusCodes.Status410Gone,
                        title: "Gone");
                }
                downloadUrl = url.ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate SAS URL for blob {BlobPath}", job.BlobPath);
                downloadUrl = null;
            }
        }

        var statusDto = new ReportExportStatusDto
        {
            Status = job.Status,
            DownloadUrl = downloadUrl,
            ErrorMessage = job.Status == ReportExportJobStatus.Failed ? job.ErrorMessage : null,
        };

        var requestId = ResolveRequestId();

        return Ok(new ApiResponse<ReportExportStatusDto>(statusDto, new ApiMeta { RequestId = requestId }));
    }

    /// <summary>List the requesting user's recent export jobs.</summary>
    [HttpGet("exports")]
    [ProducesResponseType(typeof(ApiResponse<ReportExportListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ListExports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Set<ReportExportJob>()
            .Where(j => j.OrganisationId == organisationId && j.CreatedByUserId == userId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ThenByDescending(j => j.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new
            {
                j.Id,
                j.Status,
                j.ReportType,
                j.Format,
                j.CreatedAtUtc,
                j.CompletedAtUtc,
                j.ErrorMessage,
            })
            .ToListAsync(cancellationToken);

        var result = new ReportExportListResultDto
        {
            Items = items.Select(j => new ReportExportJobDto
            {
                JobId = j.Id,
                Status = j.Status,
                ReportType = j.ReportType,
                Format = j.Format,
                CreatedAtUtc = j.CreatedAtUtc,
                CompletedAtUtc = j.CompletedAtUtc,
                DownloadUrl = null,
                ErrorMessage = j.Status == ReportExportJobStatus.Failed ? j.ErrorMessage : null,
            }).ToList()
        };

        var requestId = ResolveRequestId();

        return Ok(new ApiResponse<ReportExportListResultDto>(
            result,
            new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

    private static ReportExportJobDto MapToDto(ReportExportJob job, string? downloadUrl) => new()
    {
        JobId = job.Id,
        Status = job.Status,
        ReportType = job.ReportType,
        Format = job.Format,
        CreatedAtUtc = job.CreatedAtUtc,
        CompletedAtUtc = job.CompletedAtUtc,
        DownloadUrl = downloadUrl,
        ErrorMessage = job.Status == ReportExportJobStatus.Failed ? job.ErrorMessage : null,
    };

    private Guid ResolveOrganisationId()
    {
        var organisationClaim = HttpContext.User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (!Guid.TryParse(organisationClaim, out var organisationId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return organisationId;
    }

    private Guid ResolveUserId()
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return userId;
    }
}
