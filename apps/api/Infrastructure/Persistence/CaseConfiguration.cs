using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Entities.Legends;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Encryption;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CrimeNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.StNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.BeneficiaryName)
            .HasConversion(new SearchablePiiEncryptionConverter())
            .HasColumnType("bytea");

        builder.Property(c => c.BeneficiaryContact)
            .HasConversion(new PiiEncryptionConverter())
            .HasColumnType("bytea")
            .HasMaxLength(2048);

        builder.Property(c => c.TypeOfOffence)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.OffenceClassification)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Domicile)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Gender)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(c => c.FamilyType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.EconomicStatus)
            .HasConversion<string>()
            .HasMaxLength(16);

        // FK references to Legend tables (from Story 9.1)
        builder.Property(c => c.OccupationId);

        builder.HasOne(c => c.Occupation)
            .WithMany()
            .HasForeignKey(c => c.OccupationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.EducationLevelId);

        builder.HasOne(c => c.EducationLevel)
            .WithMany()
            .HasForeignKey(c => c.EducationLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Simple scalar fields for Recidivism and Family History (Story 11.3)
        builder.Property(c => c.FamilyHistoryOfCrime);

        builder.Property(c => c.RecidivismBeforeCount);

        builder.Property(c => c.RecidivismAfterCount);

        builder.Property(c => c.CurrentStage)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.IsFirstTimeOffender)
            .HasDefaultValue(true);

        builder.Property(c => c.VisitCount)
            .HasDefaultValue(0);

        builder.Property(c => c.Latitude)
            .HasConversion(new GpsEncryptionConverter())
            .HasColumnType("bytea");

        builder.Property(c => c.Longitude)
            .HasConversion(new GpsEncryptionConverter())
            .HasColumnType("bytea");

        builder.Property(c => c.Landmark)
            .HasConversion(new PiiEncryptionConverter())
            .HasColumnType("bytea")
            .HasMaxLength(4096);

        builder.Property(c => c.GpsVerified)
            .HasDefaultValue(false);

        builder.Property(c => c.SensitivityLevel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(SensitivityLevel.Standard)
            .IsRequired();

        builder.Property(c => c.CourtMissFlaggedAtUtc);

        builder.HasIndex(c => new { c.OrganisationId, c.CrimeNumber })
            .IsUnique();

        builder.HasIndex(c => new { c.OrganisationId, c.StNumber })
            .IsUnique();

        builder.HasIndex(c => new { c.OrganisationId, c.UpdatedAtUtc });

        builder.HasIndex(c => new { c.OrganisationId, c.AssignedWorkerId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.AssignedWorkerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
