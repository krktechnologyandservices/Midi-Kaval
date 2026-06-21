using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage6TerminationExclusionConfiguration : IEntityTypeConfiguration<CaseStage6TerminationExclusion>
{
    public void Configure(EntityTypeBuilder<CaseStage6TerminationExclusion> builder)
    {
        builder.ToTable("case_stage6_termination_exclusion");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TerminationExclusionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.JjbDetails)
            .HasMaxLength(2000);

        builder.Property(d => d.ExclusionReason)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Attachment>()
            .WithMany()
            .HasForeignKey(d => d.ReportAttachmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
