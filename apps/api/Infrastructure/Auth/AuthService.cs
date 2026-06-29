using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Auth;
// TOTP operations delegated to TwoFactorService

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class AuthService(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    OtpChallengeStore otpChallengeStore,
    AuthVerifiedStore authVerifiedStore,
    RefreshTokenStore refreshTokenStore,
    PasswordResetTokenStore passwordResetTokenStore,
    JwtTokenService jwtTokenService,
    IEmailSender emailSender,
    IAuditService auditService,
    IUserSessionService userSessionService,
    UserDeviceService userDeviceService,
    IOptions<PasswordResetOptions> passwordResetOptions,
    TwoFactorService twoFactorService,
    ILogger<AuthService> logger)
{
    public const string DeactivatedMessage = "Contact your coordinator";
    public const string AccountNotConfirmedMessage = "Please check your email to confirm your account before logging in.";
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string InvalidOtpMessage = "Invalid or expired verification code.";
    public const string InvalidRefreshTokenMessage = "Invalid or expired refresh token.";
    public const string ForgotPasswordSuccessMessage =
        "If an account exists for that email, we sent reset instructions.";
    public const string ResetPasswordSuccessMessage =
        "Password updated. Sign in with your new password.";
    public const string InvalidResetTokenMessage = "Invalid or expired reset token.";
    public const string ExpiredOrUsedResetTokenMessage =
        "This reset link has expired or was already used.";
    public const string PasswordTooShortMessage = "Password must be at least 8 characters.";

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return null;
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            await RecordLoginFailedAsync(normalizedEmail, null, cancellationToken);
            return null;
        }

        if (!user.IsActive)
        {
            if (user.IsSuspended)
            {
                throw new AuthForbiddenException(DeactivatedMessage);
            }

            // User is not suspended but not active — pending confirmation
            throw new AuthForbiddenException(AccountNotConfirmedMessage, errorCode: "ACCOUNT_NOT_CONFIRMED");
        }

        var password = request.Password?.Trim() ?? string.Empty;
        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordLoginFailedAsync(normalizedEmail, user, cancellationToken);
            return null;
        }

        // Director with TOTP enrolled → skip email OTP, return requiresTotp=true
        if (user.Role == UserRoles.Director && user.TotpEnrolledAt is not null)
        {
            // Create a binding challenge that ties TOTP verification to this password step
            var totpChallenge = new OtpChallenge
            {
                UserId = user.Id,
                OrganisationId = user.OrganisationId,
                Email = user.Email,
                OtpHash = Guid.NewGuid().ToString("N"), // nonce — no OTP to verify, just binding
                TokenVersion = user.TokenVersion,
            };

            var totpChallengeId = await otpChallengeStore.CreateAsync(totpChallenge, cancellationToken);

            return new LoginResponse
            {
                ChallengeId = null,
                TotpChallengeId = totpChallengeId,
                ExpiresInSeconds = otpChallengeStore.ExpirySeconds,
                UserId = user.Id,
                TokenVersion = user.TokenVersion,
                RequiresTotp = true,
            };
        }

        var otpCode = GenerateOtpCode();
        var challenge = new OtpChallenge
        {
            UserId = user.Id,
            OrganisationId = user.OrganisationId,
            Email = user.Email,
            OtpHash = OtpHasher.Hash(otpCode),
            TokenVersion = user.TokenVersion,
        };

        Guid challengeId = Guid.Empty;
        try
        {
            challengeId = await otpChallengeStore.CreateAsync(challenge, cancellationToken);
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Your Kaval Online verification code",
                    $"Your verification code is: {otpCode}\n\nEnter the 6-digit code from your email."),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (challengeId != Guid.Empty)
            {
                await otpChallengeStore.RemoveAsync(challengeId, CancellationToken.None);
            }

            throw;
        }
        catch (EmailDeliveryException)
        {
            if (challengeId != Guid.Empty)
            {
                await otpChallengeStore.RemoveAsync(challengeId, CancellationToken.None);
            }

            throw;
        }

        return new LoginResponse
        {
            ChallengeId = challengeId,
            ExpiresInSeconds = otpChallengeStore.ExpirySeconds,
        };
    }

    public async Task<VerifyOtpResponse?> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await otpChallengeStore.GetAsync(request.ChallengeId, cancellationToken);
        if (challenge is null)
        {
            return null;
        }

        var code = request.Code?.Trim() ?? string.Empty;
        if (!OtpHasher.Verify(code, challenge.OtpHash))
        {
            await otpChallengeStore.RecordFailedAttemptAsync(request.ChallengeId, cancellationToken);
            await auditService.RecordAsync(
                AuditEventTypes.OtpFailed,
                challenge.OrganisationId,
                subjectUserId: challenge.UserId,
                metadata: new Dictionary<string, object?> { ["challengeId"] = request.ChallengeId },
                cancellationToken: cancellationToken);
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive || user.TokenVersion != challenge.TokenVersion)
        {
            throw new AuthForbiddenException(DeactivatedMessage);
        }

        if (!await otpChallengeStore.TryFinalizeAsync(request.ChallengeId, challenge.OtpHash, cancellationToken))
        {
            return null;
        }

        return await IssueAuthTokensAsync(user, AuditEventTypes.LoginSuccess, cancellationToken);
    }

    /// <summary>Verify TOTP code for a Director login and issue JWT + refresh token.</summary>
    public async Task<VerifyOtpResponse?> VerifyTotpLoginAsync(
        VerifyTotpLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var trimmedCode = request?.Code?.Trim() ?? string.Empty;
        if (trimmedCode.Length != 6)
        {
            return null;
        }

        // Verify the binding challenge — proves user completed the password step
        var challenge = await otpChallengeStore.GetAsync(request!.TotpChallengeId, cancellationToken);
        if (challenge is null || challenge.UserId != request.UserId)
        {
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            throw new AuthForbiddenException(
                user.IsSuspended ? DeactivatedMessage : AccountNotConfirmedMessage);
        }

        // Reject if token version doesn't match (e.g., 2FA was reset since login)
        if (user.TokenVersion != request.TokenVersion)
        {
            return null;
        }

        if (user.TotpEnrolledAt is null || string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            return null;
        }

        var verified = await twoFactorService.VerifyTotpCodeAsync(user.Id, trimmedCode, cancellationToken);
        if (!verified)
        {
            await auditService.RecordAsync(
                AuditEventTypes.OtpFailed,
                user.OrganisationId,
                subjectUserId: user.Id,
                metadata: new Dictionary<string, object?> { ["source"] = "totp-login" },
                cancellationToken: cancellationToken);
            // Record failed attempt on challenge too
            await otpChallengeStore.RecordFailedAttemptAsync(request.TotpChallengeId, cancellationToken);
            return null;
        }

        // Consume the binding challenge after successful TOTP verification
        await otpChallengeStore.RemoveAsync(request.TotpChallengeId, cancellationToken);

        return await IssueAuthTokensAsync(user, AuditEventTypes.LoginSuccess, cancellationToken);
    }

    private async Task<VerifyOtpResponse> IssueAuthTokensAsync(
        User user,
        string auditEventType,
        CancellationToken cancellationToken)
    {
        var (accessToken, expiresIn) = jwtTokenService.CreateAccessToken(user);
        var refreshToken = await refreshTokenStore.IssueAsync(user.Id, user.TokenVersion, cancellationToken);

        await auditService.RecordAsync(
            auditEventType,
            user.OrganisationId,
            actorUserId: user.Id,
            subjectUserId: user.Id,
            cancellationToken: cancellationToken);

        await authVerifiedStore.SetLoginVerifiedAsync(user.Id, cancellationToken);

        return new VerifyOtpResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            RefreshToken = refreshToken,
            User = new AuthUserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
            },
        };
    }

    public async Task<RefreshResponse?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var peek = await refreshTokenStore.TryPeekAsync(refreshToken, cancellationToken);
        if (peek.Result == RefreshTokenConsumeResult.ReuseDetected && peek.Record is not null)
        {
            await userSessionService.InvalidateUserSessionsAsync(peek.Record.UserId, cancellationToken);
            return null;
        }

        if (peek.Result != RefreshTokenConsumeResult.Success || peek.Record is null)
        {
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == peek.Record.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            throw new AuthForbiddenException(DeactivatedMessage);
        }

        if (user.TokenVersion != peek.Record.TokenVersion)
        {
            return null;
        }

        var consume = await refreshTokenStore.TryConsumeAsync(
            refreshToken,
            peek.Record.UserId,
            cancellationToken);
        if (consume.Result == RefreshTokenConsumeResult.ReuseDetected && consume.Record is not null)
        {
            await userSessionService.InvalidateUserSessionsAsync(consume.Record.UserId, cancellationToken);
            return null;
        }

        if (consume.Result != RefreshTokenConsumeResult.Success || consume.Record is null)
        {
            return null;
        }

        var (accessToken, expiresIn) = jwtTokenService.CreateAccessToken(user);
        var newRefreshToken = await refreshTokenStore.IssueAsync(user.Id, user.TokenVersion, cancellationToken);

        await auditService.RecordAsync(
            AuditEventTypes.RefreshSuccess,
            user.OrganisationId,
            actorUserId: user.Id,
            subjectUserId: user.Id,
            cancellationToken: cancellationToken);

        return new RefreshResponse
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            RefreshToken = newRefreshToken,
        };
    }

    public async Task<bool> LogoutAsync(
        string refreshToken,
        string? deviceInstallId = null,
        CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokenStore.HashToken(refreshToken);
        var record = await refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        if (record is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(deviceInstallId))
        {
            await userDeviceService.RemoveForUserAsync(record.UserId, deviceInstallId, cancellationToken);
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == record.UserId, cancellationToken);
        if (user is not null)
        {
            await authVerifiedStore.ClearVerificationAsync(user.Id, cancellationToken);
            await auditService.RecordAsync(
                AuditEventTypes.Logout,
                user.OrganisationId,
                actorUserId: user.Id,
                subjectUserId: user.Id,
                metadata: new Dictionary<string, object?> { ["tokenHash"] = hash },
                cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<SessionUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new SessionUserDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
        };
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request?.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (user is not null && user.IsActive)
            {
                try
                {
                    var token = await passwordResetTokenStore.IssueAsync(user.Id, cancellationToken);
                    var resetUrl = BuildResetUrl(token);
                    var expiryMinutes = passwordResetOptions.Value.ExpiryMinutes;
                    await emailSender.SendAsync(
                        new EmailMessage(
                            user.Email,
                            "Reset your Kaval Online password",
                            $"Use the link below to reset your password:\n\n{resetUrl}\n\n"
                                + $"This link expires in {expiryMinutes} minutes and can only be used once."),
                        cancellationToken);

                    await auditService.RecordAsync(
                        AuditEventTypes.PasswordResetRequested,
                        user.OrganisationId,
                        subjectUserId: user.Id,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process password reset for user {UserId}", user.Id);
                }
            }
        }

        return new ForgotPasswordResponse { Message = ForgotPasswordSuccessMessage };
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest? request,
        CancellationToken cancellationToken = default)
    {
        request ??= new ResetPasswordRequest();
        var newPassword = request.NewPassword?.Trim() ?? string.Empty;
        if (newPassword.Length < 8)
        {
            throw new AuthBadRequestException(PasswordTooShortMessage);
        }

        var token = request.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AuthUnauthorizedException(InvalidResetTokenMessage);
        }

        var peek = await passwordResetTokenStore.TryPeekAsync(token, cancellationToken);
        if (peek.State == PasswordResetTokenState.NotFound)
        {
            throw new AuthUnauthorizedException(InvalidResetTokenMessage);
        }

        if (peek.State == PasswordResetTokenState.ReuseDetected || peek.Record is null)
        {
            throw new AuthBadRequestException(ExpiredOrUsedResetTokenMessage);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == peek.Record.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthBadRequestException(ExpiredOrUsedResetTokenMessage);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await userSessionService.InvalidateUserSessionsAsync(user.Id, cancellationToken);

        var consume = await passwordResetTokenStore.TryConsumeAsync(token, user.Id, cancellationToken);
        if (consume.State != PasswordResetTokenState.Success)
        {
            throw new AuthBadRequestException(ExpiredOrUsedResetTokenMessage);
        }

        await auditService.RecordAsync(
            AuditEventTypes.PasswordResetCompleted,
            user.OrganisationId,
            actorUserId: user.Id,
            subjectUserId: user.Id,
            cancellationToken: cancellationToken);

        return new ResetPasswordResponse { Message = ResetPasswordSuccessMessage };
    }

    public async Task<StepUpResponse?> StepUpAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var otpCode = GenerateOtpCode();
        var challenge = new OtpChallenge
        {
            UserId = user.Id,
            OrganisationId = user.OrganisationId,
            Email = user.Email,
            OtpHash = OtpHasher.Hash(otpCode),
            TokenVersion = user.TokenVersion,
        };

        Guid challengeId = Guid.Empty;
        try
        {
            challengeId = await otpChallengeStore.CreateAsync(challenge, cancellationToken);
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Your Kaval Online step-up verification code",
                    $"Your verification code is: {otpCode}\n\nEnter the 6-digit code from your email."),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (challengeId != Guid.Empty)
            {
                await otpChallengeStore.RemoveAsync(challengeId, CancellationToken.None);
            }

            throw;
        }
        catch (EmailDeliveryException)
        {
            if (challengeId != Guid.Empty)
            {
                await otpChallengeStore.RemoveAsync(challengeId, CancellationToken.None);
            }

            throw;
        }

        return new StepUpResponse
        {
            ChallengeId = challengeId,
            ExpiresInSeconds = otpChallengeStore.ExpirySeconds,
        };
    }

    public async Task<bool> VerifyStepUpAsync(
        Guid userId,
        VerifyStepUpRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await otpChallengeStore.GetAsync(request.ChallengeId, cancellationToken);
        if (challenge is null || challenge.UserId != userId)
        {
            return false;
        }

        var code = request.Code?.Trim() ?? string.Empty;
        if (!OtpHasher.Verify(code, challenge.OtpHash))
        {
            await otpChallengeStore.RecordFailedAttemptAsync(request.ChallengeId, cancellationToken);
            await auditService.RecordAsync(
                AuditEventTypes.OtpFailed,
                challenge.OrganisationId,
                subjectUserId: challenge.UserId,
                metadata: new Dictionary<string, object?>
                {
                    ["challengeId"] = request.ChallengeId,
                    ["source"] = "step-up",
                },
                cancellationToken: cancellationToken);
            return false;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || user.TokenVersion != challenge.TokenVersion)
        {
            return false;
        }

        if (!await otpChallengeStore.TryFinalizeAsync(request.ChallengeId, challenge.OtpHash, cancellationToken))
        {
            return false;
        }

        await authVerifiedStore.SetStepUpVerifiedAsync(userId, cancellationToken);
        return true;
    }

    private string BuildResetUrl(string token)
    {
        var baseUrl = passwordResetOptions.Value.WebResetUrl.TrimEnd('/');
        return $"{baseUrl}?token={Uri.EscapeDataString(token)}";
    }

    private async Task RecordLoginFailedAsync(
        string normalizedEmail,
        User? user,
        CancellationToken cancellationToken)
    {
        var organisationId = user?.OrganisationId ?? ResolveDefaultOrganisationId();
        if (organisationId == Guid.Empty)
        {
            return;
        }

        await auditService.RecordAsync(
            AuditEventTypes.LoginFailed,
            organisationId,
            subjectUserId: user?.Id,
            metadata: new Dictionary<string, object?> { ["email"] = normalizedEmail },
            cancellationToken: cancellationToken);
    }

    private Guid ResolveDefaultOrganisationId()
    {
        var value = configuration["Seed:OrganisationId"];
        return Guid.TryParse(value, out var organisationId)
            ? organisationId
            : Guid.Empty;
    }

    private static string GenerateOtpCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

public sealed class AuthForbiddenException(string message, string? errorCode = null) : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;
}

public sealed class AuthBadRequestException(string message) : Exception(message);

public sealed class AuthUnauthorizedException(string message) : Exception(message);
