using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;

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
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.BeneficiaryContact)
            .HasMaxLength(32);

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

        builder.Property(c => c.CurrentStage)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.IsFirstTimeOffender)
            .HasDefaultValue(true);

        builder.Property(c => c.VisitCount)
            .HasDefaultValue(0);

        builder.Property(c => c.Latitude)
            .HasPrecision(9, 6);

        builder.Property(c => c.Longitude)
            .HasPrecision(9, 6);

        builder.Property(c => c.Landmark)
            .HasMaxLength(500);

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
