using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class AuditDigestEntryConfiguration : IEntityTypeConfiguration<AuditDigestEntry>
{
    public void Configure(EntityTypeBuilder<AuditDigestEntry> builder)
    {
        builder.ToTable("audit_digest_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AuditEventId)
            .IsRequired();

        builder.Property(e => e.OrganisationId)
            .IsRequired();

        builder.Property(e => e.DigestSentAtUtc)
            .IsRequired();

        builder.Property(e => e.DigestBatchId)
            .IsRequired();

        builder.HasIndex(e => e.AuditEventId)
            .IsUnique();

        builder.HasIndex(e => new { e.OrganisationId, e.DigestSentAtUtc });

        builder.HasOne(e => e.Organisation)
            .WithMany()
            .HasForeignKey(e => e.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
