using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("organisations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(false);

        builder.Property(x => x.HasPendingRecovery)
            .HasColumnName("has_pending_recovery")
            .HasDefaultValue(false);

        builder.Property(x => x.Require2fa)
            .HasColumnName("require_2fa")
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("NOW()")
            .IsRequired();
    }
}
