using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Audit;
using OtpNet;

namespace MidiKaval.Api.Domain.RoleManagement;

public sealed record TotpProvisioningResult(string ProvisioningUri, string SecretBase32);
public sealed record TotpEnrollmentResult(bool Success, string? ErrorMessage);

public class TwoFactorService(
    AppDbContext db,
    IOptions<TotpOptions> options,
    IAuditService auditService,
    ILogger<TwoFactorService> logger)
{
    public virtual async Task<TotpProvisioningResult> GenerateProvisioningAsync(Guid userId, string? actorIpAddress = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        // Don't overwrite an already-enrolled secret
        if (user.TotpEnrolledAt is not null)
        {
            throw new InvalidOperationException("Two-factor authentication is already enrolled.");
        }

        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secret);

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user.TotpSecret = base32Secret;
            await db.SaveChangesAsync(ct);

            var snapshot = new TargetUserSnapshotDto(user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.Role);

            await auditService.RecordAsync(
                AuditEventTypes.TwoFactorProvisioned,
                user.OrganisationId,
                actorUserId: userId,
                subjectUserId: userId,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                cancellationToken: ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        var uri = new OtpUri(OtpType.Totp, base32Secret, user.Email, options.Value.Issuer)
            .ToString();

        return new TotpProvisioningResult(uri, base32Secret);
    }

    public virtual async Task<TotpEnrollmentResult> EnrollAsync(Guid userId, string code, string? actorIpAddress = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            return new TotpEnrollmentResult(false, "No TOTP secret found. Please initiate enrollment first.");
        }

        return await EnrollCoreAsync(user, code, actorIpAddress, ct);
    }

    protected virtual async Task<TotpEnrollmentResult> EnrollCoreAsync(User user, string code, string? actorIpAddress = null, CancellationToken ct = default)
    {
        var secret = Base32Encoding.ToBytes(user.TotpSecret!);
        var totp = new Totp(secret, step: options.Value.StepSeconds, totpSize: options.Value.CodeLength);
        var verified = totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));

        if (!verified)
        {
            return new TotpEnrollmentResult(false, "Invalid code. Please try again.");
        }

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            user.TotpEnrolledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var snapshot = new TargetUserSnapshotDto(user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.Role);

            await auditService.RecordAsync(
                AuditEventTypes.TwoFactorEnrolled,
                user.OrganisationId,
                actorUserId: user.Id,
                subjectUserId: user.Id,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                cancellationToken: ct);

            await transaction.CommitAsync(ct);

            return new TotpEnrollmentResult(true, null);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public virtual async Task<bool> VerifyTotpCodeAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            return false;
        }

        var secret = Base32Encoding.ToBytes(user.TotpSecret);
        var totp = new Totp(secret, step: options.Value.StepSeconds, totpSize: options.Value.CodeLength);
        return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
    }

    public virtual async Task ResetTwoFactorAsync(Guid actorUserId, Guid targetUserId, Guid? organisationId = null, string? actorIpAddress = null, CancellationToken ct = default)
    {
        User? targetUser;
        if (organisationId is not null)
        {
            targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganisationId == organisationId, ct);
        }
        else
        {
            targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
        }

        if (targetUser is null)
            throw new KeyNotFoundException("User not found.");

        var snapshot = new TargetUserSnapshotDto(targetUser.Email, $"{targetUser.FirstName} {targetUser.LastName}".Trim(), targetUser.Role);

        using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            targetUser.TotpSecret = null;
            targetUser.TotpEnrolledAt = null;
            targetUser.TokenVersion++;
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                AuditEventTypes.TwoFactorReset,
                targetUser.OrganisationId,
                actorUserId: actorUserId,
                subjectUserId: targetUserId,
                targetUserSnapshot: snapshot,
                actorIpAddress: actorIpAddress,
                metadata: new Dictionary<string, object?>
                {
                    ["resetBy"] = actorUserId.ToString("D"),
                },
                cancellationToken: ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public virtual async Task<bool> IsEnrolledAsync(Guid userId, CancellationToken ct)
    {
        var enrolledAt = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.TotpEnrolledAt)
            .FirstOrDefaultAsync(ct);

        return enrolledAt is not null;
    }
}
