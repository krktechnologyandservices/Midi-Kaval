using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities.Legends;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class EducationLevelConfiguration : IEntityTypeConfiguration<EducationLevel>
{
    public void Configure(EntityTypeBuilder<EducationLevel> builder)
    {
        builder.ToTable("legend_education_levels");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.OrganisationId).IsRequired();
        builder.HasIndex(e => new { e.OrganisationId, e.Name }).IsUnique();
    }
}
