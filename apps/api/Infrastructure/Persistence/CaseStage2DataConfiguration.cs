using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class CaseStage2DataConfiguration : IEntityTypeConfiguration<CaseStage2Data>
{
    public void Configure(EntityTypeBuilder<CaseStage2Data> builder)
    {
        builder.ToTable("case_stage2_data");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.BioPsychoSocialAssessment)
            .HasMaxLength(4000);

        builder.Property(d => d.IcpRecords)
            .HasMaxLength(4000);

        builder.Property(d => d.LifeSkillTraining)
            .HasMaxLength(4000);

        builder.Property(d => d.ParentManagement)
            .HasMaxLength(4000);

        builder.Property(d => d.GroupWork)
            .HasMaxLength(4000);

        builder.Property(d => d.CommunityProgramAttendance)
            .HasMaxLength(4000);

        builder.Property(d => d.PmaStatus)
            .HasMaxLength(4000);

        builder.Property(d => d.OverallProgress)
            .HasMaxLength(4000);

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

        builder.HasIndex(d => d.CaseId)
            .IsUnique();
    }
}
