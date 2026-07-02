using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Auth;

public class BackupCodeService(
    AppDbContext db,
    IAuditService auditService,
    ILogger<BackupCodeService> logger)
{
    private const int DefaultCodeCount = 8;
    private const int CodeLength = 10; // Dash-separated groups: A3K9-X7M2-P1

    public virtual async Task<List<string>> GenerateAsync(Guid userId, int count = DefaultCodeCount, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(userId.ToString());

        var plaintextCodes = new List<string>();
        var backupCodes = new List<BackupCode>();
        var now = DateTime.UtcNow;

        for (var i = 0; i < count; i++)
        {
            var plaintext = GenerateCode();
            var hash = ComputeSha256Hex(plaintext);

            plaintextCodes.Add(plaintext);
            backupCodes.Add(new BackupCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = hash,
                Used = false,
                CreatedAtUtc = now,
            });
        }

        db.BackupCodes.AddRange(backupCodes);
        await db.SaveChangesAsync(ct);

        // Load user for organisation context
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.OrganisationId })
            .FirstOrDefaultAsync(ct);

        await auditService.RecordAsync(
            AuditEventTypes.TwoFactorBypassGenerated,
            user?.OrganisationId ?? Guid.Empty,
            subjectUserId: userId,
            metadata: new Dictionary<string, object?>
            {
                ["backupCodesGenerated"] = count,
                ["backupCodesRemainingCount"] = await GetRemainingCountAsync(userId, ct),
            },
            cancellationToken: ct);

        return plaintextCodes;
    }

    public virtual async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(code);

        var normalized = NormalizeCode(code);
        var hash = ComputeSha256Hex(normalized);

        var backupCode = await db.BackupCodes
            .Where(bc => bc.UserId == userId && bc.CodeHash == hash && !bc.Used)
            .FirstOrDefaultAsync(ct);

        if (backupCode is null)
            return false;

        // Load user for organisation context
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.OrganisationId })
            .FirstOrDefaultAsync(ct);

        backupCode.Used = true;
        backupCode.UsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await auditService.RecordAsync(
            AuditEventTypes.TwoFactorBackupUsed,
            user?.OrganisationId ?? Guid.Empty,
            subjectUserId: userId,
            metadata: new Dictionary<string, object?> { ["backupCodeId"] = backupCode.Id },
            cancellationToken: ct);

        return true;
    }

    public virtual async Task<int> GetRemainingCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.BackupCodes
            .CountAsync(bc => bc.UserId == userId && !bc.Used, ct);
    }

    public virtual async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        await db.BackupCodes
            .Where(bc => bc.UserId == userId && !bc.Used)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(bc => bc.Used, true)
                .SetProperty(bc => bc.UsedAtUtc, DateTime.UtcNow), ct);
    }

    /// <summary>Atomically revokes all unused backup codes and generates a new set.</summary>
    public virtual async Task<List<string>> RegenerateAsync(Guid userId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await RevokeAllAsync(userId, ct);
        var codes = await GenerateAsync(userId, DefaultCodeCount, ct);

        await tx.CommitAsync(ct);
        return codes;
    }

    private static string GenerateCode()
    {
        var bytes = new byte[6];
        RandomNumberGenerator.Fill(bytes);

        // Map 6 random bytes to 10 alphanumeric characters (0-9, A-Z excluding I,O,0,1)
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var codeChars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            codeChars[i] = chars[bytes[i] % chars.Length];
        }

        // Format as dashed groups: A3K9-X7M2-P1
        return $"{new string(codeChars, 0, 4)}-{new string(codeChars, 4, 4)}-{new string(codeChars, 8, 2)}";
    }

    private static string NormalizeCode(string code)
    {
        // Strip non-alphanumeric and uppercase — handles user-typed dashes or spaces
        var sanitized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        // Re-insert dashes at canonical positions
        if (sanitized.Length == 10)
        {
            return $"{sanitized[..4]}-{sanitized.Substring(4, 4)}-{sanitized[8..]}";
        }

        return sanitized;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
