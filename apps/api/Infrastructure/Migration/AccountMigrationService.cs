using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Migration;

public sealed class AccountMigrationService(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<AccountMigrationService> logger)
{
    public static readonly Guid VendorOrganisationId = new("00000000-0000-0000-0000-000000000001");

    public sealed record MigrationSummary(int Created, int Updated, int Skipped);

    public async Task<MigrationSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var organisationIdValue = configuration["Seed:OrganisationId"];
        if (!Guid.TryParse(organisationIdValue, out var organisationId))
        {
            logger.LogWarning("Seed:OrganisationId is missing or invalid. No accounts will be migrated.");
            return new MigrationSummary(0, 0, 0);
        }

        await EnsureOrganisationAsync(organisationId, "Primary Organisation", cancellationToken);
        await EnsureOrganisationAsync(VendorOrganisationId, "Vendor System", cancellationToken);

        var created = 0;
        var updated = 0;
        var skipped = 0;

        // Wrap the entire migration in a DB transaction so that a crash mid-way
        // does not leave inconsistent state (e.g. org created but no users).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (adminResult, adminSkipped) = await MigrateAdminAsync(organisationId, cancellationToken);
        created += adminResult;
        skipped += adminSkipped;

        var (fwCreated, fwUpdated, fwSkipped) = await MigrateFieldWorkerAsync(organisationId, cancellationToken);
        created += fwCreated;
        updated += fwUpdated;
        skipped += fwSkipped;

        var (vendorResult, vendorSkipped) = await MigrateVendorAsync(cancellationToken);
        created += vendorResult;
        skipped += vendorSkipped;

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Account migration complete. {Created} accounts created, {Updated} updated, {Skipped} skipped.",
            created, updated, skipped);

        return new MigrationSummary(created, updated, skipped);
    }

    private async Task EnsureOrganisationAsync(Guid orgId, string name, CancellationToken ct)
    {
        var exists = await db.Organisations.AnyAsync(o => o.Id == orgId, ct);
        if (!exists)
        {
            var org = new Organisation
            {
                Id = orgId,
                Name = name,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Organisations.Add(org);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created organisation {OrganisationId} ({Name}).", orgId, name);
        }
    }

    private async Task<(int created, int skipped)> MigrateAdminAsync(Guid organisationId, CancellationToken ct)
    {
        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:Admin:Email or Seed:Admin:Password is missing — skipping admin account migration.");
            return (0, 1);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail, ct);

        if (exists)
        {
            logger.LogInformation("Admin account {Email} already exists — skipping.", normalizedEmail);
            return (0, 1);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = normalizedEmail,
            FirstName = "",
            LastName = "",
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Migrated admin account: {Email} (Director).", normalizedEmail);
        return (1, 0);
    }

    private async Task<(int created, int updated, int skipped)> MigrateFieldWorkerAsync(Guid organisationId, CancellationToken ct)
    {
        var email = configuration["Seed:FieldWorker:Email"];
        var password = configuration["Seed:FieldWorker:Password"]?.Trim();
        var role = configuration["Seed:FieldWorker:Role"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:FieldWorker:Email or Seed:FieldWorker:Password is missing — skipping field worker account migration.");
            return (0, 0, 1);
        }

        // Normalize case for comparison: "SocialWorker", "socialworker", "SOCIALWORKER" all match.
        if (!string.Equals(role, UserRoles.SocialWorker, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, UserRoles.CaseWorker, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Seed:FieldWorker:Role must be SocialWorker or CaseWorker (was {Role}) — skipping.",
                role ?? "(missing)");
            return (0, 0, 1);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var existing = await db.Users.SingleOrDefaultAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail, ct);

        if (existing is not null)
        {
            existing.Role = role;
            existing.PasswordHash = passwordHasher.HashPassword(existing, password);
            existing.IsActive = true; // restore in case user was deactivated
            existing.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Updated field worker account: {Email} (role={Role}).", normalizedEmail, role);
            return (0, 1, 0); // count as updated, not skipped
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = normalizedEmail,
            FirstName = "",
            LastName = "",
            Role = role,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Migrated field worker account: {Email} (role={Role}).", normalizedEmail, role);
        return (1, 0, 0);
    }

    private async Task<(int created, int skipped)> MigrateVendorAsync(CancellationToken ct)
    {
        var email = configuration["Seed:Vendor:Email"];
        var password = configuration["Seed:Vendor:Password"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:Vendor:Email or Seed:Vendor:Password is missing — skipping vendor account migration.");
            return (0, 1);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(
            u => u.OrganisationId == VendorOrganisationId && u.Email == normalizedEmail, ct);

        if (exists)
        {
            logger.LogInformation("Vendor account {Email} already exists — skipping.", normalizedEmail);
            return (0, 1);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = VendorOrganisationId,
            Email = normalizedEmail,
            FirstName = "",
            LastName = "",
            Role = UserRoles.Vendor,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Migrated vendor account: {Email} (Vendor).", normalizedEmail);
        return (1, 0);
    }
}
