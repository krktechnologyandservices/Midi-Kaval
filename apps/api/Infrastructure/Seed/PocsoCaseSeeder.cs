using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Seed;

public class PocsoCaseSeeder(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<PocsoCaseSeeder> logger,
    IHostEnvironment environment)
{
    public const string SeedCrimeNumber = "POCSO-DEV-001";
    public const string SeedStNumber = "ST-POCSO-001";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var fieldWorkerEmail = configuration["Seed:FieldWorker:Email"];
        var organisationIdValue = configuration["Seed:OrganisationId"];

        if (string.IsNullOrWhiteSpace(fieldWorkerEmail)
            || !Guid.TryParse(organisationIdValue, out var organisationId))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Skipping POCSO case seed: set Seed:FieldWorker:Email and Seed:OrganisationId.");
            }

            return;
        }

        var normalizedEmail = fieldWorkerEmail.Trim().ToLowerInvariant();
        var worker = await db.Users.SingleOrDefaultAsync(
            u => u.OrganisationId == organisationId && u.Email == normalizedEmail,
            cancellationToken);

        if (worker is null)
        {
            logger.LogWarning(
                "Skipping POCSO case seed: field worker {Email} not found.",
                normalizedEmail);
            return;
        }

        var existing = await db.Cases.SingleOrDefaultAsync(
            c => c.OrganisationId == organisationId && c.CrimeNumber == SeedCrimeNumber,
            cancellationToken);

        if (existing is not null)
        {
            existing.SensitivityLevel = SensitivityLevel.POCSO;
            existing.AssignedWorkerId = worker.Id;
            existing.AssignedAtUtc ??= DateTime.UtcNow;
            existing.BeneficiaryName = "Ravi Kumar";
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Updated POCSO dev case {CrimeNumber}.", SeedCrimeNumber);
            return;
        }

        var coordinator = await db.Users
            .Where(u => u.OrganisationId == organisationId && u.Role == UserRoles.Coordinator)
            .OrderBy(u => u.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (coordinator is null)
        {
            logger.LogWarning("Skipping POCSO case seed: no coordinator user found.");
            return;
        }

        var now = DateTime.UtcNow;
        var caseId = Guid.NewGuid();
        db.Cases.Add(new Case
        {
            Id = caseId,
            OrganisationId = organisationId,
            CrimeNumber = SeedCrimeNumber,
            StNumber = SeedStNumber,
            BeneficiaryName = "Ravi Kumar",
            BeneficiaryAge = 14,
            BeneficiaryContact = "9876543210",
            TypeOfOffence = "POCSO",
            OffenceClassification = OffenceClassification.Heinous,
            Domicile = Domicile.Urban,
            SensitivityLevel = SensitivityLevel.POCSO,
            CurrentStage = CaseStage.ProcessInitiation,
            VisitCount = 0,
            CreatedByUserId = coordinator.Id,
            AssignedWorkerId = worker.Id,
            AssignedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded POCSO dev case {CrimeNumber} assigned to {Email}.",
            SeedCrimeNumber,
            normalizedEmail);
    }
}
