using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("report_export_jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.ReportType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(j => j.Format)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(j => j.BlobPath)
            .HasMaxLength(1024);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(j => j.CreatedAtUtc)
            .IsRequired();

        builder.Property(j => j.CompletedAtUtc);

        builder.Property(j => j.FromDate);

        builder.Property(j => j.ToDate);

        builder.Property(j => j.Year);

        builder.HasIndex(j => new { j.OrganisationId, j.Status, j.CreatedAtUtc });
        builder.HasIndex(j => new { j.OrganisationId, j.CreatedByUserId, j.CreatedAtUtc });
    }
}
