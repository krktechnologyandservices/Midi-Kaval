using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CourtSittingConfiguration : IEntityTypeConfiguration<CourtSitting>
{
    public void Configure(EntityTypeBuilder<CourtSitting> builder)
    {
        builder.ToTable("court_sittings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScheduledAtUtc)
            .IsRequired();

        builder.Property(s => s.CourtName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(s => s.Purpose)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(4000);

        builder.Property(s => s.Outcome)
            .HasMaxLength(2000);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        builder.Property(s => s.ReminderSentAtUtc);

        builder.Property(s => s.MissEscalatedAtUtc);

        builder.HasIndex(s => new { s.OrganisationId, s.CaseId, s.ScheduledAtUtc });

        builder.HasIndex(s => new { s.OrganisationId, s.Status, s.ScheduledAtUtc });

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(s => s.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
