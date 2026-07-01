using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.TargetUserSnapshot)
            .HasColumnType("jsonb");

        builder.Property(e => e.ActorIpAddress)
            .HasMaxLength(64);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => new { e.OrganisationId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.EventType, e.CreatedAtUtc });

        builder.HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.SubjectUser)
            .WithMany()
            .HasForeignKey(e => e.SubjectUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.DigestEntries)
            .WithOne()
            .HasForeignKey(e => e.AuditEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
