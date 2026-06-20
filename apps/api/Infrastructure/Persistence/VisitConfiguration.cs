using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.LastRescheduleReason)
            .HasMaxLength(500);

        builder.HasIndex(v => new { v.OrganisationId, v.AssigneeUserId, v.ScheduledAtUtc });
        builder.HasIndex(v => new { v.CaseId, v.ScheduledAtUtc });
    }
}
