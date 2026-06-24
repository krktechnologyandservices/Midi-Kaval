using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Seed;

public class FieldWorkerUserSeeder(
    AppDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    ILogger<FieldWorkerUserSeeder> logger,
    IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["Seed:FieldWorker:Email"];
        var password = configuration["Seed:FieldWorker:Password"]?.Trim();
        var role = configuration["Seed:FieldWorker:Role"]?.Trim();
        var organisationIdValue = configuration["Seed:OrganisationId"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || !Guid.TryParse(organisationIdValue, out var organisationId))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Skipping field worker seed: set Seed:FieldWorker:Email, Seed:FieldWorker:Password, and Seed:OrganisationId in configuration or user secrets.");
            }

            return;
        }

        if (role is not (UserRoles.SocialWorker or UserRoles.CaseWorker))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Skipping field worker seed: Seed:FieldWorker:Role must be SocialWorker or CaseWorker (was {Role}).",
                    role ?? "(missing)");
            }

            return;
        }

        // Ensure the organisation exists before seeding — supports the FK constraint.
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
            logger.LogInformation("Seeded pilot organisation {OrganisationId} for field worker account.", organisationId);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await db.Users.SingleOrDefaultAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail,
            cancellationToken);

        if (existing is not null)
        {
            var now = DateTime.UtcNow;
            existing.Role = role;
            existing.IsActive = true;
            existing.PasswordHash = passwordHasher.HashPassword(existing, password);
            existing.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Updated field worker user {Email} to role {Role}.",
                normalizedEmail,
                role);
            return;
        }

        var createdAt = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = normalizedEmail,
            Role = role,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded field worker user {Email} with role {Role}.", normalizedEmail, role);
    }
}
