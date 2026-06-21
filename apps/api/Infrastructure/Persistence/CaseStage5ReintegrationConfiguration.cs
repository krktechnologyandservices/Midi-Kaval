using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage5ReintegrationConfiguration : IEntityTypeConfiguration<CaseStage5Reintegration>
{
    public void Configure(EntityTypeBuilder<CaseStage5Reintegration> builder)
    {
        builder.ToTable("case_stage5_reintegration");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.ReintegrationLevel)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.InstitutionDetails)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
