using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseRelatedCaseConfiguration : IEntityTypeConfiguration<CaseRelatedCase>
{
    public void Configure(EntityTypeBuilder<CaseRelatedCase> builder)
    {
        builder.ToTable("case_related_cases", t => t.HasCheckConstraint("ck_case_related_cases_ordered_pair", "case_id_a < case_id_b"));

        builder.HasKey(d => d.Id);

        builder.Property(d => d.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseIdA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseIdB)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CaseIdA, d.CaseIdB })
            .IsUnique();
    }
}
