using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class InterventionConfiguration : IEntityTypeConfiguration<Intervention>
{
    public void Configure(EntityTypeBuilder<Intervention> builder)
    {
        builder.ToTable("interventions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Direction)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.CategoryName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(i => i.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.Outcome)
            .HasMaxLength(2000);

        builder.Property(i => i.CreatedAtUtc)
            .IsRequired();

        builder.Property(i => i.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(i => new { i.CaseId, i.CreatedAtUtc, i.Id });

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(i => i.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.AssignedStaffUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
