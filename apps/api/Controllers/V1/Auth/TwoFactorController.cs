using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Models.Auth;
using MidiKaval.Api.Models;

namespace MidiKaval.Api.Controllers.V1.Auth;

[ApiController]
[Route("api/v1/auth")]
public class TwoFactorController(
    TwoFactorService twoFactorService,
    AuthService authService,
    BackupCodeService backupCodeService,
    ILogger<TwoFactorController> logger) : ControllerBase
{
    private const string UserNotFoundMessage = "User not found.";

    private Guid? TryGetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (value is null || !Guid.TryParse(value, out var userId))
            return null;
        return userId;
    }

    /// <summary>Initiate TOTP enrollment — generates secret and returns provisioning URI.</summary>
    [Authorize]
    [HttpPost("enroll-2fa")]
    [EnableRateLimiting("auth-enroll-totp")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InitiateEnrollment(CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await twoFactorService.GenerateProvisioningAsync(userId.Value, ipAddress, ct);
            return Ok(new { provisioningUri = result.ProvisioningUri, secretBase32 = result.SecretBase32 });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User Not Found",
                Detail = UserNotFoundMessage,
            });
        }
        catch (InvalidOperationException)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Already Enrolled",
                Detail = "Two-factor authentication is already enrolled.",
            });
        }
    }

    /// <summary>Complete TOTP enrollment by verifying a code from the authenticator app.</summary>
    [Authorize]
    [HttpPost("verify-enroll-2fa")]
    [EnableRateLimiting("auth-verify-totp")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteEnrollment([FromBody] VerifyTotpRequest request, CancellationToken ct)
    {
        var trimmedCode = request?.Code?.Trim() ?? string.Empty;
        if (trimmedCode.Length != 6)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Code",
                Detail = "A 6-digit code is required.",
            });
        }

        var userId = TryGetUserId();
        if (userId is null)
            return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await twoFactorService.EnrollAsync(userId.Value, trimmedCode, ipAddress, ct);

        if (!result.Success)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Code",
                Detail = result.ErrorMessage,
            });
        }

        return Ok(new { success = true });
    }

    /// <summary>Check whether the current user has enrolled in 2FA.</summary>
    [Authorize]
    [HttpGet("totp-status")]
    [EnableRateLimiting("data-read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTotpStatus(CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (userId is null)
            return Unauthorized();

        var enrolled = await twoFactorService.IsEnrolledAsync(userId.Value, ct);
        return Ok(new { enrolled });
    }

    /// <summary>Get 2FA enrollment status with timestamp.</summary>
    [Authorize]
    [HttpGet("2fa-status")]
    [EnableRateLimiting("data-read")]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get2faStatus(CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (userId is null)
            return Unauthorized();

        var (enrolled, enrolledAt) = await twoFactorService.GetEnrollmentStatusAsync(userId.Value, ct);
        return Ok(new TwoFactorStatusResponse { Enrolled = enrolled, EnrolledAt = enrolledAt });
    }

    /// <summary>Verify a backup code during login (unauthenticated) and issue JWT.</summary>
    [HttpPost("verify-backup-code")]
    [EnableRateLimiting("auth-verify-backup-code")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyBackupCode([FromBody] VerifyBackupCodeRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Request",
                Detail = "Request body is required.",
            });
        }

        var trimmedCode = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedCode))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Code",
                Detail = "A verification code is required.",
            });
        }

        var verified = await backupCodeService.VerifyAsync(request.UserId, trimmedCode, ct);
        if (!verified)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Code",
                Detail = "The backup code is invalid or has already been used.",
            });
        }

        var result = await authService.CompleteBackupCodeLoginAsync(request.UserId, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login Failed",
                Detail = "Could not complete login. The account may be deactivated.",
            });
        }

        return Ok(new ApiResponse<VerifyOtpResponse>(result, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Verify TOTP code during login (unauthenticated) and issue JWT.</summary>
    [AllowAnonymous]
    [HttpPost("verify-totp-login")]
    [EnableRateLimiting("auth-verify-totp")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyTotpLogin([FromBody] VerifyTotpLoginRequest request, CancellationToken ct)
    {
        var trimmedCode = request?.Code?.Trim() ?? string.Empty;
        if (trimmedCode.Length != 6)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Code",
                Detail = "A 6-digit code is required.",
            });
        }

        try
        {
            var result = await authService.VerifyTotpLoginAsync(request!, ct);
            if (result is null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid Code",
                    Detail = "Invalid or expired verification code.",
                });
            }

            return Ok(new ApiResponse<VerifyOtpResponse>(result, new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (AuthForbiddenException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Account Deactivated",
                Detail = ex.Message,
            });
        }
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
