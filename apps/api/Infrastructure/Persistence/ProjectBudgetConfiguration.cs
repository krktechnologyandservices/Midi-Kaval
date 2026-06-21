using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> builder)
    {
        builder.ToTable("project_budgets");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Source)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(d => d.FinancialYearStart)
            .IsRequired();

        builder.Property(d => d.FinancialYearEnd)
            .IsRequired();

        builder.Property(d => d.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasMaxLength(2000);

        builder.Property(d => d.DecisionComment)
            .HasMaxLength(500);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.OrganisationId, d.FinancialYearStart, d.Source })
            .IsUnique();

        builder.HasIndex(d => d.OrganisationId);
    }
}
