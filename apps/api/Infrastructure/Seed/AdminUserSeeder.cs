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

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = normalizedEmail,
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
