using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class TravelClaimConfiguration : IEntityTypeConfiguration<TravelClaim>
{
    public void Configure(EntityTypeBuilder<TravelClaim> builder)
    {
        builder.ToTable("travel_claims");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClaimDate)
            .IsRequired();

        builder.Property(c => c.StartLocation)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.Destination)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.TransportMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(c => c.AutoNumber)
            .HasMaxLength(32);

        builder.Property(c => c.Notes)
            .HasMaxLength(2000);

        builder.Property(c => c.DecisionComment)
            .HasMaxLength(2000);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(c => new { c.OrganisationId, c.ClaimantUserId, c.ClaimDate });
        builder.HasIndex(c => new { c.OrganisationId, c.Status, c.ClaimDate });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.ClaimantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
