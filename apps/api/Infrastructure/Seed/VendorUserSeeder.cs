using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Seed;

public class VendorUserSeeder(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<VendorUserSeeder> logger,
    IHostEnvironment environment)
{
    public static readonly Guid VendorOrganisationId = new("00000000-0000-0000-0000-000000000001");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["Seed:Vendor:Email"];
        var password = configuration["Seed:Vendor:Password"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Skipping Vendor seed: set Seed:Vendor:Email and Seed:Vendor:Password in configuration or user secrets.");
            }

            return;
        }

        // Self-heal: ensure the vendor system organisation exists before seeding a Vendor user.
        // This is needed because the FK constraint (fk_users_organisations_organisation_id)
        // requires a matching organisations row.
        var orgExists = await db.Organisations.AnyAsync(o => o.Id == VendorOrganisationId, cancellationToken);
        if (!orgExists)
        {
            var now = DateTime.UtcNow;
            var org = new Organisation
            {
                Id = VendorOrganisationId,
                Name = "Vendor System",
                IsActive = true,
                CreatedAtUtc = now,
            };
            db.Organisations.Add(org);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded vendor system organisation {OrganisationId} for Vendor account.", VendorOrganisationId);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(
            u => u.OrganisationId == VendorOrganisationId && u.Email == normalizedEmail,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = VendorOrganisationId,
            Email = normalizedEmail,
            Role = UserRoles.Vendor,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded Vendor user: {Email}", normalizedEmail);
    }
}
