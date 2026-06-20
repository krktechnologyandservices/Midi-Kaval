using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseNoteConfiguration : IEntityTypeConfiguration<CaseNote>
{
    public void Configure(EntityTypeBuilder<CaseNote> builder)
    {
        builder.ToTable("case_notes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.NoteType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(n => n.BodyText)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(n => n.ActionRequired)
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(n => new { n.CaseId, n.CreatedAtUtc });

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(n => n.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
