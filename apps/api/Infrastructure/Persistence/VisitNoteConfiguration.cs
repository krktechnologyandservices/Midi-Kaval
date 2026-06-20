using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class VisitNoteConfiguration : IEntityTypeConfiguration<VisitNote>
{
    public void Configure(EntityTypeBuilder<VisitNote> builder)
    {
        builder.ToTable("visit_notes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.BodyText)
            .HasMaxLength(4000)
            .IsRequired();

        builder.HasIndex(n => n.VisitId)
            .IsUnique();
    }
}
