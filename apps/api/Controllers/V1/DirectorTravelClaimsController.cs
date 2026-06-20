using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using MidiKaval.Api.Infrastructure;

using MidiKaval.Api.Infrastructure.Auth;

using MidiKaval.Api.Infrastructure.Cases;

using MidiKaval.Api.Infrastructure.TravelClaims;

using MidiKaval.Api.Models;

using MidiKaval.Api.Models.TravelClaims;



namespace MidiKaval.Api.Controllers.V1;



/// <summary>Director travel claim review and pending list.</summary>

[ApiController]

[Route("api/v1/director/travel-claims")]

[Authorize(Policy = Policies.DirectorOnly)]

public sealed class DirectorTravelClaimsController(TravelClaimService travelClaimService) : ControllerBase

{

    /// <summary>Lists submitted travel claims awaiting director approval.</summary>

    [HttpGet("pending")]

    [ProducesResponseType(typeof(ApiResponse<TravelClaimListResultDto>), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]

    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]

    public async Task<IActionResult> ListPending(CancellationToken cancellationToken)

    {

        var (result, totalCount) = await travelClaimService.ListPendingForDirectorAsync(cancellationToken);

        var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string

            ?? HttpContext.TraceIdentifier;



        return Ok(new ApiResponse<TravelClaimListResultDto>(

            result,

            new ApiMeta { RequestId = requestId, TotalCount = totalCount }));

    }



    /// <summary>Returns a travel claim for director review.</summary>

    [HttpGet("{id:guid}")]

    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]

    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]

    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]

    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]

    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)

    {

        try

        {

            var dto = await travelClaimService.GetForDirectorAsync(id, cancellationToken);

            return Ok(dto);

        }

        catch (CaseNotFoundException)

        {

            return NotFoundProblem("Travel claim not found.");

        }

    }



    private IActionResult NotFoundProblem(string detail) =>

        Problem(

            detail: detail,

            statusCode: StatusCodes.Status404NotFound,

            title: "Not Found");

}


