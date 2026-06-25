using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.RoleManagement;

namespace MidiKaval.Api.Domain.RoleManagement;

public sealed class ActivationConflictException(string message) : Exception(message);

public class RegistrationService(
    AppDbContext db,
    TokenService tokenService,
    IPasswordHasher<User> passwordHasher,
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
        return await ExecuteActivationAsync(token, fullName, password, cancellationToken);
    }

    /// <summary>
    /// Executes the transactional part of activation: atomic token consumption,
    /// user creation, and organisation activation. Override in tests to bypass
    /// relational-provider requirements (InMemory does not support transactions
    /// or raw SQL).
    /// </summary>
    protected virtual async Task<ActivationResult> ExecuteActivationAsync(
        ActivationToken token, string fullName, string password, CancellationToken cancellationToken)
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
        return (errors.Count == 0, errors.ToArray());
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
