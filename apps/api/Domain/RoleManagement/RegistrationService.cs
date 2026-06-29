using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models.Audit;

namespace MidiKaval.Api.Domain.RoleManagement;

public sealed class ActivationConflictException(string message) : Exception(message);

public sealed class RegistrationConflictException(string message, string? errorCode = null) : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;
}

public class RegistrationService(
    AppDbContext db,
    TokenService tokenService,
    IPasswordHasher<User> passwordHasher,
    IAuditService auditService,
    IConfiguration configuration,
    ILogger<RegistrationService> logger)
{
    /// <summary>Validates an activation link without consuming the token.</summary>
    public async Task<ValidateLinkResult> ValidateLinkAsync(
        string rawToken, string signature, CancellationToken cancellationToken = default)
    {
        // Always perform DB lookup regardless of signature validity (timing side-channel mitigation).
        var tokenHash = ComputeSha256Hex(rawToken);
        var token = await db.ActivationTokens
            .AsNoTracking()
            .Include(t => t.Organisation)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (!tokenService.ValidateSignature(rawToken, signature))
        {
            return new ValidateLinkResult(false, null, null);
        }

        if (token is null || token.ExpiresAtUtc <= DateTime.UtcNow || token.ConsumedAtUtc is not null)
        {
            return new ValidateLinkResult(false, null, null);
        }

        return new ValidateLinkResult(true, token.TargetEmail, token.Organisation.Name);
    }

    /// <summary>Consumes the activation token and creates the first Director account.</summary>
    public async Task<ActivationResult> ActivateOrganisationAsync(
        string rawToken,
        string signature,
        string fullName,
        string password,
        string? actorIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. HMAC signature verification
        if (!tokenService.ValidateSignature(rawToken, signature))
        {
            throw new ValidationException("Invalid activation link.");
        }

        // 2. Compute SHA-256 hash and look up token
        var tokenHash = ComputeSha256Hex(rawToken);
        var token = await db.ActivationTokens
            .AsNoTracking()
            .Include(t => t.Organisation)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            throw new ValidationException("This activation link was not found. Please contact the Vendor to request a new activation link.");
        }

        // 3. Check expiry
        if (token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ValidationException("This activation link has expired. Please contact the Vendor to request a new activation link.");
        }

        // 4. Check already consumed
        if (token.ConsumedAtUtc is not null)
        {
            throw new ActivationConflictException("This activation link has already been used. Please contact the Vendor to request a new activation link.");
        }

        // 5. Check if org already has a Director
        var hasDirector = await db.Users.AnyAsync(
            u => u.OrganisationId == token.OrganisationId && u.Role == UserRoles.Director, cancellationToken);
        if (hasDirector)
        {
            throw new ActivationConflictException("This organisation already has a Director. Another activation link cannot be used.");
        }

        // 6. Validate password
        var (passwordValid, passwordErrors) = ValidatePassword(password);
        if (!passwordValid)
        {
            throw new ValidationException(string.Join(" ", passwordErrors));
        }

        // 7-12. Execute the transactional activation (atomic consume, user creation, org activation)
        return await ExecuteActivationAsync(token, fullName, password, actorIpAddress, cancellationToken);
    }

    /// <summary>
    /// Executes the transactional part of activation: atomic token consumption,
    /// user creation, and organisation activation. Override in tests to bypass
    /// relational-provider requirements (InMemory does not support transactions
    /// or raw SQL).
    /// </summary>
    protected virtual async Task<ActivationResult> ExecuteActivationAsync(
        ActivationToken token, string fullName, string password, string? actorIpAddress = null, CancellationToken cancellationToken = default)
    {
        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await ConsumeTokenAtomicallyAsync(token.Id, cancellationToken);

            // 8. Split full name into first/last
            var (firstName, lastName) = SplitFullName(fullName);

            // Validate name segments against DB column limits (128 chars each)
            if (firstName.Length > 128)
                throw new ValidationException($"First name must be 128 characters or fewer (currently {firstName.Length}).");
            if (lastName.Length > 128)
                throw new ValidationException($"Last name must be 128 characters or fewer (currently {lastName.Length}).");

            // 9. Create Director user
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = token.OrganisationId,
                Email = token.TargetEmail,
                FirstName = firstName,
                LastName = lastName,
                Role = UserRoles.Director,
                TokenVersion = 0,
                IsActive = true,
                IsSuspended = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Users.Add(user);

            // 10. Activate organisation
            var org = await db.Organisations.FindAsync(new object[] { token.OrganisationId }, cancellationToken);
            if (org is not null)
            {
                org.IsActive = true;

                // Clear any pending recovery flag if a re-issued link is being consumed
                if (org.HasPendingRecovery)
                {
                    org.HasPendingRecovery = false;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // Record audit event for organisation activation and Director creation
            var snapshot = new TargetUserSnapshotDto(user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.Role);
            await auditService.RecordAsync(
                AuditEventTypes.OrganisationActivated,
                token.OrganisationId,
                actorUserId: user.Id,
                subjectUserId: user.Id,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["organisation_name"] = token.Organisation.Name,
                    ["activation_token_id"] = token.Id.ToString(),
                },
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Organisation {OrganisationId} activated. Director user {UserId} created ({Email}).",
                token.OrganisationId, user.Id, user.Email);

            return new ActivationResult(user.Id, token.OrganisationId, token.Organisation.Name);
        }
        catch
        {
            // Best-effort rollback: do not propagate cancellation during rollback
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx,
                    "Rollback failed for organisation activation {OrganisationId}.",
                    token.OrganisationId);
            }
            throw;
        }
    }

    /// <summary>Validates an invitation link without consuming the token.</summary>
    public async Task<ValidateInvitationLinkResult> ValidateInvitationLinkAsync(
        string rawToken, string signature, CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeSha256Hex(rawToken);
        var invitation = await db.Invitations
            .AsNoTracking()
            .Include(i => i.Organisation)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (!tokenService.ValidateSignature(rawToken, signature))
        {
            return new ValidateInvitationLinkResult(false, null, null, null);
        }

        if (invitation is null || invitation.ExpiresAtUtc <= DateTime.UtcNow || invitation.Status != InvitationStatus.Pending)
        {
            return new ValidateInvitationLinkResult(false, null, null, null);
        }

        return new ValidateInvitationLinkResult(true, invitation.TargetEmail, invitation.Organisation.Name, invitation.Role);
    }

    /// <summary>Accepts an invitation, creates a user in pending confirmation state, and sends a confirmation email.</summary>
    public async Task<AcceptInvitationResult> AcceptInvitationAsync(
        string rawToken,
        string signature,
        string fullName,
        string password,
        string? actorIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. HMAC signature verification
        if (!tokenService.ValidateSignature(rawToken, signature))
        {
            throw new ValidationException("Invalid invitation link.");
        }

        // 2. Validate full name
        var (fullNameValid, fullNameError) = ValidateFullName(fullName);
        if (!fullNameValid)
        {
            throw new ValidationException(fullNameError);
        }

        // 3. Validate password
        var (passwordValid, passwordErrors) = ValidatePassword(password);
        if (!passwordValid)
        {
            throw new ValidationException(string.Join(" ", passwordErrors));
        }

        // 4. SHA-256 hash and look up invitation
        var tokenHash = ComputeSha256Hex(rawToken);
        var invitation = await db.Invitations
            .Include(i => i.Organisation)
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            throw new KeyNotFoundException("Invitation not found. Please ask your Director to send a new invitation.");
        }

        // 5. Check expiry
        if (invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new RegistrationConflictException("This invitation has expired. Please ask your Director to send a new invitation.", errorCode: "INVITATION_EXPIRED");
        }

        // 6. Check status
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new RegistrationConflictException("This invitation has already been used. Please ask your Director to send a new invitation.", errorCode: "INVITATION_ALREADY_USED");
        }

        return await ExecuteAcceptInvitationAsync(invitation, fullName, password, actorIpAddress, cancellationToken);
    }

    /// <summary>
    /// Executes the transactional part of invitation acceptance: atomic token consumption,
    /// user creation (pending confirmation), and confirmation email enqueue.
    /// Override in tests to bypass relational-provider requirements.
    /// </summary>
    protected virtual async Task<AcceptInvitationResult> ExecuteAcceptInvitationAsync(
        Invitation invitation, string fullName, string password, string? actorIpAddress = null, CancellationToken cancellationToken = default)
    {
        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Atomic token consumption
            await ConsumeInvitationTokenAtomicallyAsync(invitation.Id, cancellationToken);

            var (firstName, lastName) = SplitFullName(fullName);

            if (string.IsNullOrWhiteSpace(lastName))
                throw new ValidationException("Full name must include both first and last name.");

            if (firstName.Length > 128)
                throw new ValidationException($"First name must be 128 characters or fewer (currently {firstName.Length}).");
            if (lastName.Length > 128)
                throw new ValidationException($"Last name must be 128 characters or fewer (currently {lastName.Length}).");

            var now = DateTime.UtcNow;

            // Create user in "pending confirmation" state
            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganisationId = invitation.OrganisationId,
                Email = invitation.TargetEmail,
                FirstName = firstName,
                LastName = lastName,
                Role = invitation.Role,
                TokenVersion = 1,
                IsActive = false,
                IsSuspended = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Users.Add(user);

            // Update invitation
            invitation.Status = InvitationStatus.Confirmed;
            invitation.ConfirmedAtUtc = now;

            // Generate confirmation token
            (string rawConfirmationToken, string confirmationTokenHash, string confirmationSignature) confirmation;
            try
            {
                confirmation = GenerateConfirmationToken();
            }
            catch (CryptographicException)
            {
                throw new ValidationException("Could not generate security token. Please try again.");
            }
            var (rawConfirmationToken, confirmationTokenHash, confirmationSignature) = confirmation;
            var ttlHours = configuration.GetValue<int>("ConfirmationLink:TokenTtlHours", 24);
            if (ttlHours <= 0) ttlHours = 24;

            var confirmationToken = new ConfirmationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                InvitationId = invitation.Id,
                TokenHash = confirmationTokenHash,
                ExpiresAtUtc = now.AddHours(ttlHours),
                CreatedAtUtc = now,
            };
            db.ConfirmationTokens.Add(confirmationToken);

            // Snapshot the new user's identity before any potential changes
            var userSnapshot = new TargetUserSnapshotDto(user.Email, $"{firstName} {lastName}".Trim(), user.Role);

            await db.SaveChangesAsync(cancellationToken);

            // Write audit event in same transaction
            await auditService.RecordAsync(
                AuditEventTypes.AccountCreated,
                user.OrganisationId,
                subjectUserId: user.Id,
                targetUserSnapshot: userSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["email"] = user.Email,
                    ["role"] = user.Role,
                    ["invitationId"] = invitation.Id,
                },
                cancellationToken: cancellationToken);

            // Record confirmation token creation in same transaction
            await auditService.RecordAsync(
                AuditEventTypes.ConfirmationTokenCreated,
                user.OrganisationId,
                subjectUserId: user.Id,
                targetUserSnapshot: userSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["confirmationTokenId"] = confirmationToken.Id,
                    ["email"] = user.Email,
                },
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Enqueue confirmation email via Hangfire (outside transaction — acceptable for async delivery)
            await SendConfirmationEmailAsync(
                confirmationToken.Id,
                rawConfirmationToken,
                confirmationSignature,
                user.Email,
                firstName,
                user.Id,
                user.OrganisationId,
                cancellationToken);

            logger.LogInformation(
                "User {UserId} created from invitation {InvitationId} (pending confirmation). Confirmation email enqueued.",
                user.Id, invitation.Id);

            return new AcceptInvitationResult(user.Email, invitation.Organisation.Name);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx,
                    "Rollback failed for invitation acceptance {InvitationId}.",
                    invitation.Id);
            }
            throw;
        }
    }

    /// <summary>Confirms email by consuming a confirmation token and activating the user.</summary>
    public async Task<ConfirmEmailResult> ConfirmEmailAsync(
        string rawToken,
        string signature,
        string? actorIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. HMAC signature verification
        if (!tokenService.ValidateSignature(rawToken, signature))
        {
            throw new ValidationException("Invalid confirmation link.");
        }

        // 2. SHA-256 hash and look up confirmation token
        var tokenHash = ComputeSha256Hex(rawToken);
        var confirmationToken = await db.ConfirmationTokens
            .Include(t => t.User)
            .Include(t => t.Invitation)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (confirmationToken is null)
        {
            throw new KeyNotFoundException("Confirmation link not found. Please contact your Director to request a new invitation.");
        }

        // 3. Check expiry
        if (confirmationToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new RegistrationConflictException("This confirmation link has expired. Contact your Director to request a new invitation.", errorCode: "CONFIRMATION_EXPIRED");
        }

        // 4. Check not already consumed
        if (confirmationToken.ConsumedAtUtc is not null)
        {
            throw new RegistrationConflictException("This confirmation link has already been used. You can log in with your credentials.", errorCode: "CONFIRMATION_ALREADY_USED");
        }

        return await ExecuteConfirmEmailAsync(confirmationToken, actorIpAddress, cancellationToken);
    }

    /// <summary>
    /// Executes the transactional part of email confirmation: atomic token consumption
    /// and user activation. Override in tests to bypass relational-provider requirements.
    /// </summary>
    protected virtual async Task<ConfirmEmailResult> ExecuteConfirmEmailAsync(
        ConfirmationToken confirmationToken, string? actorIpAddress = null, CancellationToken cancellationToken = default)
    {
        // Guard against orphaned FK — Include may resolve to null on corrupted data
        if (confirmationToken.User is null)
        {
            throw new KeyNotFoundException("User not found for confirmation token. Please contact your Director to request a new invitation.");
        }

        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Activate user
            var user = confirmationToken.User;
            user.IsActive = true;
            user.UpdatedAtUtc = DateTime.UtcNow;

            // Atomically consume confirmation token
            await ConsumeConfirmationTokenAtomicallyAsync(confirmationToken.Id, cancellationToken);

            // Update invitation confirmed_at if applicable
            if (confirmationToken.Invitation is not null)
            {
                confirmationToken.Invitation.ConfirmedAtUtc = DateTime.UtcNow;
            }

            // Snapshot the confirming user's identity
            var userSnapshot = new TargetUserSnapshotDto(user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.Role);

            await db.SaveChangesAsync(cancellationToken);

            // Write audit event in same transaction
            await auditService.RecordAsync(
                AuditEventTypes.EmailConfirmed,
                user.OrganisationId,
                subjectUserId: user.Id,
                targetUserSnapshot: userSnapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["confirmationTokenId"] = confirmationToken.Id,
                    ["email"] = user.Email,
                },
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Email confirmed for user {UserId} ({Email}). Account activated.",
                user.Id, user.Email);

            return new ConfirmEmailResult(user.Email);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx,
                    "Rollback failed for email confirmation token {TokenId}.",
                    confirmationToken.Id);
            }
            throw;
        }
    }

    /// <summary>Atomically consumes an invitation token via raw SQL.</summary>
    protected virtual async Task ConsumeInvitationTokenAtomicallyAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE invitations SET status = {InvitationStatus.Confirmed}, confirmed_at_utc = NOW() WHERE id = {invitationId} AND status = {InvitationStatus.Pending}",
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new RegistrationConflictException("This invitation has already been used. Please ask your Director to send a new invitation.", errorCode: "INVITATION_ALREADY_USED");
        }
    }

    /// <summary>Atomically consumes a confirmation token via raw SQL.</summary>
    protected virtual async Task ConsumeConfirmationTokenAtomicallyAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE confirmation_tokens SET consumed_at_utc = NOW() WHERE id = {tokenId} AND consumed_at_utc IS NULL",
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new RegistrationConflictException("This confirmation link has already been used.", errorCode: "CONFIRMATION_ALREADY_USED");
        }
    }

    /// <summary>Enqueues the confirmation email delivery job via Hangfire.</summary>
    protected virtual async Task SendConfirmationEmailAsync(
        Guid confirmationTokenId,
        string rawToken,
        string signature,
        string targetEmail,
        string userName,
        Guid userId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        BackgroundJob.Enqueue<ConfirmationEmailDeliveryJob>(
            j => j.ExecuteAsync(
                confirmationTokenId,
                rawToken,
                signature,
                targetEmail,
                userName,
                userId,
                orgId,
                CancellationToken.None));

        await Task.CompletedTask;
    }

    public static (bool IsValid, string[] Errors) ValidatePassword(string password)
    {
        var errors = new List<string>();
        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters long.");
        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");
        return (errors.Count == 0, [.. errors]);
    }

    public static (bool IsValid, string Error) ValidateFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (false, "Full name is required.");
        if (trimmed.Length < 2)
            return (false, "Full name must be at least 2 characters.");
        if (trimmed.Length > 256)
            return (false, "Full name must be 256 characters or fewer.");
        return (true, string.Empty);
    }

    /// <summary>
    /// Atomically consumes the activation token using a raw SQL UPDATE with
    /// <c>WHERE consumed_at_utc IS NULL</c>. Override in tests to bypass
    /// relational-provider requirements (InMemory does not support raw SQL).
    /// </summary>
    protected virtual async Task ConsumeTokenAtomicallyAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE activation_tokens SET consumed_at_utc = NOW() WHERE id = {tokenId} AND consumed_at_utc IS NULL",
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new ActivationConflictException("This activation link has already been used. Please contact the Vendor to request a new activation link.");
        }
    }

    private static string ComputeSha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private (string rawToken, string tokenHash, string signature) GenerateConfirmationToken()
    {
        // Reuse GenerateActivationToken — it generates cryptographically random bytes,
        // computes SHA-256 hash, and computes HMAC-SHA256 signature. The HMAC key is
        // resolved inside TokenService from ACTIVATION_LINK_SIGNING_KEY.
        // The naming "activation" is historic; the token format is generic.
        return tokenService.GenerateActivationToken();
    }

    protected internal static (string firstName, string lastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (string.Empty, string.Empty);

        var firstSpace = trimmed.IndexOf(' ');
        // Handle non-ASCII whitespace (e.g. NBSP, thin space)
        var nonAsciiSpace = -1;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (char.IsWhiteSpace(trimmed[i]))
            {
                nonAsciiSpace = i;
                break;
            }
        }
        var splitIndex = firstSpace >= 0 && (nonAsciiSpace < 0 || firstSpace <= nonAsciiSpace)
            ? firstSpace
            : nonAsciiSpace;

        if (splitIndex < 0)
            return (trimmed, string.Empty);

        return (trimmed[..splitIndex].Trim(), trimmed[(splitIndex + 1)..].Trim());
    }
}

public sealed record ValidateLinkResult(bool IsValid, string? Email, string? OrganisationName);

public sealed record ActivationResult(Guid UserId, Guid OrganisationId, string OrganisationName);

public sealed record ValidateInvitationLinkResult(bool IsValid, string? Email, string? OrganisationName, string? Role);

public sealed record AcceptInvitationResult(string Email, string OrganisationName);

public sealed record ConfirmEmailResult(string Email);
