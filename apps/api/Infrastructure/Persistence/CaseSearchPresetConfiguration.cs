using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseSearchPresetConfiguration : IEntityTypeConfiguration<CaseSearchPreset>
{
    public void Configure(EntityTypeBuilder<CaseSearchPreset> builder)
    {
        builder.ToTable("case_search_presets");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.FiltersJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(p => new { p.OrganisationId, p.UserId, p.Name })
            .IsUnique();
    }
}
