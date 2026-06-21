using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage4PlacementConfiguration : IEntityTypeConfiguration<CaseStage4Placement>
{
    public void Configure(EntityTypeBuilder<CaseStage4Placement> builder)
    {
        builder.ToTable("case_stage4_placement");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.PlacementType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.InstitutionName)
            .HasMaxLength(500);

        builder.Property(d => d.Address)
            .HasMaxLength(2000);

        builder.Property(d => d.StartDate)
            .IsRequired()
            .HasColumnType("date");

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
