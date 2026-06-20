using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseAssignmentConfiguration : IEntityTypeConfiguration<CaseAssignment>
{
    public void Configure(EntityTypeBuilder<CaseAssignment> builder)
    {
        builder.ToTable("case_assignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PriorActions)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.OpenItems)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.NextVisitPurpose)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(a => new { a.OrganisationId, a.CaseId });
        builder.HasIndex(a => new { a.OrganisationId, a.ToWorkerId });
    }
}
