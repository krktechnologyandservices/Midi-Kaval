using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class BudgetLineItemConfiguration : IEntityTypeConfiguration<BudgetLineItem>
{
    public void Configure(EntityTypeBuilder<BudgetLineItem> builder)
    {
        builder.ToTable("budget_line_items");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.BudgetHead)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.AmountAllocated)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.AmountUtilized)
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<ProjectBudget>()
            .WithMany(pb => pb.BudgetLineItems)
            .HasForeignKey(d => d.ProjectBudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.ProjectBudgetId, d.BudgetHead })
            .IsUnique();

        builder.HasIndex(d => d.ProjectBudgetId);
    }
}
