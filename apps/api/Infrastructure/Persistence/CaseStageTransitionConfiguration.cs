using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStageTransitionConfiguration : IEntityTypeConfiguration<CaseStageTransition>
{
    public void Configure(EntityTypeBuilder<CaseStageTransition> builder)
    {
        builder.ToTable("case_stages");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStage)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.ToStage)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.Notes)
            .HasMaxLength(2000);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => new { t.CaseId, t.CreatedAtUtc });
    }
}
