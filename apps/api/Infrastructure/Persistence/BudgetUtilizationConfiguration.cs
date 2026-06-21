using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class BudgetUtilizationConfiguration : IEntityTypeConfiguration<BudgetUtilization>
{
    public void Configure(EntityTypeBuilder<BudgetUtilization> builder)
    {
        builder.ToTable("budget_utilizations");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AmountUtilized)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.UtilizationDate)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedAtUtc).IsRequired();

        // Prevent deleting line items that have utilization records
        builder.HasOne(d => d.BudgetLineItem)
            .WithMany()
            .HasForeignKey(d => d.BudgetLineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Case)
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.BudgetLineItemId, d.UtilizationDate });
        builder.HasIndex(d => d.DeletedAtUtc);
    }
}
