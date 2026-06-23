using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure.Budgets;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Models.Budgets;
using MidiKaval.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/budgets")]
[Authorize]
[Produces("application/json")]
public class BudgetsController : ControllerBase
{
    private readonly BudgetService budgetService;
    private readonly BudgetUtilizationService budgetUtilizationService;
    private readonly IBudgetReportExportService budgetReportExcelService;

    public BudgetsController(
        BudgetService budgetService,
        BudgetUtilizationService budgetUtilizationService,
        IBudgetReportExportService budgetReportExcelService)
    {
        this.budgetService = budgetService;
        this.budgetUtilizationService = budgetUtilizationService;
        this.budgetReportExcelService = budgetReportExcelService;
    }

    /// <summary>
    /// List budgets (paginated).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(PaginatedResult<BudgetListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await budgetService.ListAsync(page, pageSize, ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Get budget by ID with line items.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(BudgetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await budgetService.GetByIdAsync(id, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Create a new budget (Draft status).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(typeof(BudgetDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await budgetService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "A budget with the same source and financial year already exists." });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Update a budget (Draft or Returned only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(typeof(BudgetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await budgetService.UpdateAsync(id, request, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "The update resulted in a conflict. Check for duplicate budget heads or source/financial year conflicts." });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Propose a budget (Draft → Proposed).
    /// </summary>
    [HttpPost("{id:guid}/propose")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Propose(Guid id, CancellationToken ct = default)
    {
        try
        {
            await budgetService.ProposeAsync(id, ct);
            return Ok();
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Approve a budget (Proposed → Approved). Director only.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.Director)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveBudgetRequest request, CancellationToken ct = default)
    {
        try
        {
            await budgetService.ApproveAsync(id, request, ct);
            return Ok();
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Return a budget (Proposed → Returned). Director only.
    /// </summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = Policies.Director)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnBudgetRequest request, CancellationToken ct = default)
    {
        try
        {
            await budgetService.ReturnAsync(id, request, ct);
            return Ok();
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Execute a budget (Approved → Executed).
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Execute(Guid id, CancellationToken ct = default)
    {
        try
        {
            await budgetService.ExecuteAsync(id, ct);
            return Ok();
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// List utilization entries for a budget (paginated, date-filtered).
    /// </summary>
    [HttpGet("{budgetId:guid}/utilizations")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(PaginatedResult<BudgetUtilizationListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ListUtilizations(
        Guid budgetId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await budgetUtilizationService.ListAsync(budgetId, fromDate, toDate, page, pageSize, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Get utilization summary per budget head.
    /// </summary>
    [HttpGet("{budgetId:guid}/utilizations/summary")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(BudgetUtilizationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> GetUtilizationSummary(Guid budgetId, CancellationToken ct = default)
    {
        try
        {
            var result = await budgetUtilizationService.GetSummaryAsync(budgetId, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Create a utilization entry for a budget.
    /// </summary>
    [HttpPost("{budgetId:guid}/utilizations")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(typeof(BudgetUtilizationListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> CreateUtilization(
        Guid budgetId,
        [FromBody] CreateBudgetUtilizationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await budgetUtilizationService.CreateAsync(budgetId, request, ct);
            return CreatedAtAction(nameof(ListUtilizations), new { budgetId }, result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "A database conflict occurred while creating the utilization entry." });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Update a utilization entry.
    /// </summary>
    [HttpPut("{budgetId:guid}/utilizations/{id:guid}")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(typeof(BudgetUtilizationListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> UpdateUtilization(
        Guid budgetId,
        Guid id,
        [FromBody] UpdateBudgetUtilizationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await budgetUtilizationService.UpdateAsync(budgetId, id, request, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Delete a utilization entry (hard or soft delete).
    /// </summary>
    [HttpDelete("{budgetId:guid}/utilizations/{id:guid}")]
    [Authorize(Policy = Policies.AccountantOrAbove)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> DeleteUtilization(
        Guid budgetId,
        Guid id,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            await budgetUtilizationService.DeleteAsync(budgetId, id, force, ct);
            return Ok();
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "A database conflict occurred while deleting the utilization entry." });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Get budget report by frequency.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = Policies.Director)]
    [ProducesResponseType(typeof(BudgetReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> Report(
        [FromQuery] string frequency = "Quarterly",
        [FromQuery] int year = 2026,
        CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<BudgetReportFrequency>(frequency, ignoreCase: true, out var parsedFrequency)
                || !Enum.IsDefined(parsedFrequency)
                || !Enum.GetName(parsedFrequency)!.Equals(frequency, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = $"'{frequency}' is not a valid frequency. Must be one of: Monthly, Quarterly, HalfYearly, Annually." });
            }

            var result = await budgetService.GetReportAsync(parsedFrequency, year, ct);
            return Ok(result);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Export budget report as Excel.
    /// </summary>
    [HttpGet("report/export")]
    [Authorize(Policy = Policies.Director)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ExportReport(
        [FromQuery] string frequency = "Quarterly",
        [FromQuery] int year = 2026,
        CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<BudgetReportFrequency>(frequency, ignoreCase: true, out var parsedFrequency)
                || !Enum.IsDefined(parsedFrequency)
                || !Enum.GetName(parsedFrequency)!.Equals(frequency, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = $"'{frequency}' is not a valid frequency. Must be one of: Monthly, Quarterly, HalfYearly, Annually." });
            }

            if (year < 2000 || year > 2100)
            {
                return BadRequest(new { error = "Year must be between 2000 and 2100." });
            }

            var report = await budgetService.GetReportAsync(parsedFrequency, year, ct);
            var bytes = budgetReportExcelService.Generate(report);
            var fileName = $"Budget-Report-{frequency}-{year}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (BudgetNotFoundException)
        {
            return NotFound();
        }
        catch (BudgetBusinessRuleException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}
