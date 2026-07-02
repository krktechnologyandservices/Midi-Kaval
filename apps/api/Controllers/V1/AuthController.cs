using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Auth;
using System.Security.Claims;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Authentication endpoints for login, OTP, session refresh, and logout.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
public class AuthController(AuthService authService, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Validate credentials and send an email OTP challenge.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            if (result is null)
            {
                return UnauthorizedProblem(AuthService.InvalidCredentialsMessage);
            }

            return Ok(result);
        }
        catch (AuthForbiddenException ex)
        {
            return ForbiddenProblem(ex.Message, ex.ErrorCode);
        }
        catch (EmailDeliveryException ex)
        {
            return ServiceUnavailableProblem(ex.Message);
        }
    }

    /// <summary>Verify OTP and issue JWT access and refresh tokens.</summary>
    [HttpPost("verify-otp")]
    [EnableRateLimiting("auth-verify")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.VerifyOtpAsync(request, cancellationToken);
            if (result is null)
            {
                return UnauthorizedProblem(AuthService.InvalidOtpMessage);
            }

            AppendRefreshCookie(result.RefreshToken);
            return Ok(result);
        }
        catch (AuthForbiddenException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
    }

    /// <summary>Rotate refresh token and issue a new JWT access token.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    [ProducesResponseType(typeof(ApiResponse<RefreshResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        var refreshToken = AuthTokenHelpers.ReadRefreshToken(Request, request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return UnauthorizedProblem(AuthService.InvalidRefreshTokenMessage);
        }

        try
        {
            var result = await authService.RefreshAsync(refreshToken, cancellationToken);
            if (result is null)
            {
                return UnauthorizedProblem(AuthService.InvalidRefreshTokenMessage);
            }

            AppendRefreshCookie(result.RefreshToken);
            return Ok(result);
        }
        catch (AuthForbiddenException ex)
        {
            return ForbiddenProblem(ex.Message);
        }
    }

    /// <summary>Revoke refresh token and clear session cookie.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting("auth-logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        var refreshToken = AuthTokenHelpers.ReadRefreshToken(Request, request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return UnauthorizedProblem(AuthService.InvalidRefreshTokenMessage);
        }

        var revoked = await authService.LogoutAsync(
            refreshToken,
            request?.DeviceInstallId,
            cancellationToken);
        if (!revoked)
        {
            return UnauthorizedProblem(AuthService.InvalidRefreshTokenMessage);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Request a password reset link (always returns generic success).</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Reset password using a single-use token from email.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-reset-password")]
    [ProducesResponseType(typeof(ApiResponse<ResetPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (AuthBadRequestException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (AuthUnauthorizedException ex)
        {
            return UnauthorizedProblem(ex.Message);
        }
    }

    /// <summary>Change password for the authenticated user.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId))
        {
            return UnauthorizedProblem("Invalid access token.");
        }

        var success = await authService.ChangePasswordAsync(userId, request, cancellationToken);
        if (!success)
        {
            return BadRequestProblem("Current password is incorrect or the new password is invalid.");
        }

        return Ok(new { message = "Password updated successfully." });
    }

    /// <summary>Return the authenticated user profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SessionUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return UnauthorizedProblem("Invalid access token.");
        }

        var user = await authService.GetCurrentUserAsync(userId, cancellationToken);
        if (user is null)
        {
            if (HttpContext.Items.ContainsKey(InactiveUserAuthConstants.InactiveUserItemKey))
            {
                return ForbiddenProblem(AuthService.DeactivatedMessage);
            }

            return UnauthorizedProblem("Invalid access token.");
        }

        return Ok(user);
    }

    /// <summary>Send a step-up OTP challenge to the authenticated field worker.</summary>
    [HttpPost("step-up")]
    [Authorize(Policy = Policies.FieldWorker)]
    [EnableRateLimiting("auth-step-up")]
    [ProducesResponseType(typeof(ApiResponse<StepUpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StepUp(CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId))
        {
            return UnauthorizedProblem("Invalid access token.");
        }

        try
        {
            var result = await authService.StepUpAsync(userId, cancellationToken);
            if (result is null)
            {
                return UnauthorizedProblem("Invalid access token.");
            }

            return Ok(result);
        }
        catch (EmailDeliveryException ex)
        {
            return ServiceUnavailableProblem(ex.Message);
        }
    }

    /// <summary>Verify step-up OTP without rotating refresh token.</summary>
    [HttpPost("verify-step-up")]
    [Authorize(Policy = Policies.FieldWorker)]
    [EnableRateLimiting("auth-verify-step-up")]
    [ProducesResponseType(typeof(ApiResponse<VerifyStepUpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyStepUp(
        [FromBody] VerifyStepUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveUserId(out var userId))
        {
            return UnauthorizedProblem("Invalid access token.");
        }

        var verified = await authService.VerifyStepUpAsync(userId, request, cancellationToken);
        if (!verified)
        {
            return UnauthorizedProblem(AuthService.InvalidOtpMessage);
        }

        return Ok(new VerifyStepUpResponse { Verified = true });
    }

    private bool TryResolveUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }

    private void AppendRefreshCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Secure = !environment.IsDevelopment(),
            MaxAge = TimeSpan.FromDays(7),
        };

        Response.Cookies.Append(AuthTokenHelpers.RefreshCookieName, refreshToken, cookieOptions);
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Append(
            AuthTokenHelpers.RefreshCookieName,
            string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                Secure = !environment.IsDevelopment(),
                MaxAge = TimeSpan.Zero,
            });
    }

    private IActionResult UnauthorizedProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized");

    private IActionResult ForbiddenProblem(string detail, string? errorCode = null)
    {
        var problem = new ProblemDetails
        {
            Detail = detail,
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = errorCode is not null
                ? $"https://errors/{errorCode}"
                : "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        };
        if (errorCode is not null)
        {
            problem.Extensions["code"] = errorCode;
        }
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private IActionResult ServiceUnavailableProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service Unavailable");

    private IActionResult BadRequestProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request");
}
