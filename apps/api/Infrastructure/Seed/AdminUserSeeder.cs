using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Seed;

public class AdminUserSeeder(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<AdminUserSeeder> logger,
    IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"]?.Trim();
        var organisationIdValue = configuration["Seed:OrganisationId"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || !Guid.TryParse(organisationIdValue, out var organisationId))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Skipping Director seed: set Seed:Admin:Email, Seed:Admin:Password, and Seed:OrganisationId in configuration or user secrets.");
            }

            return;
        }

        // Ensure the organisation exists before seeding a Director user.
        // This is needed because the new FK constraint (fk_users_organisations_organisation_id)
        // requires a matching organisations row.
        var orgExists = await db.Organisations.AnyAsync(o => o.Id == organisationId, cancellationToken);
        if (!orgExists)
        {
            var now = DateTime.UtcNow;
            var org = new Organisation
            {
                Id = organisationId,
                Name = "Pilot Organisation",
                IsActive = true,
                CreatedAtUtc = now,
            };
            db.Organisations.Add(org);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded pilot organisation {OrganisationId} for Director account.", organisationId);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = normalizedEmail,
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
