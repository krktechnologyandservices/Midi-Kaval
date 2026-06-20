using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(NotificationListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var dto = await notificationService.ListForCurrentUserAsync(cancellationToken);
        return Ok(dto);
    }

    /// <summary>Return role-based notification preference defaults (v1 stub).</summary>
    [HttpGet("unread-count")]
    [Authorize]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var count = await notificationService.GetUnreadCountAsync(cancellationToken);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpGet("preferences")]
    [Authorize]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Preferences()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        return Ok(NotificationPreferencesDefaults.ForRole(role));
    }

    [HttpPatch("{id:guid}/read")]
    [Authorize]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await notificationService.MarkReadAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (NotificationNotFoundException)
        {
            return NotFoundProblem("Notification not found.");
        }
    }

    private IActionResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);
}
