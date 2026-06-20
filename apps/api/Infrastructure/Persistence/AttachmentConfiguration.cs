using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ResourceType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.BlobName)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(a => a.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(a => new { a.ResourceType, a.ResourceId });
        builder.HasIndex(a => new { a.OrganisationId, a.Status });
    }
}
