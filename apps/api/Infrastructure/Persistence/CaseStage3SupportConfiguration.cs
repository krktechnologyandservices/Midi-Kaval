using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage3SupportConfiguration : IEntityTypeConfiguration<CaseStage3Support>
{
    public void Configure(EntityTypeBuilder<CaseStage3Support> builder)
    {
        builder.ToTable("case_stage3_supports");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SupportType)
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.ProviderName)
            .HasMaxLength(200);

        builder.Property(d => d.Notes)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.Property(d => d.RowVersion)
            .IsRowVersion();

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.CaseId);

        builder.HasIndex(d => d.CreatedByUserId);
    }
}
