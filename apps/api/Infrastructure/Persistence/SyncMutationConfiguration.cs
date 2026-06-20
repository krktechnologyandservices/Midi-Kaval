using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class SyncMutationConfiguration : IEntityTypeConfiguration<SyncMutation>
{
    public void Configure(EntityTypeBuilder<SyncMutation> builder)
    {
        builder.ToTable("sync_mutations");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MutationType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(m => m.PayloadJson)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.ServerMessage)
            .HasMaxLength(2000);

        builder.HasIndex(m => new { m.OrganisationId, m.UserId, m.ClientMutationId })
            .IsUnique();
    }
}
