using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class ConfirmationTokenConfiguration : IEntityTypeConfiguration<ConfirmationToken>
{
    public void Configure(EntityTypeBuilder<ConfirmationToken> builder)
    {
        builder.ToTable("confirmation_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.ConsumedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.LastDeliveryAttemptAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.DeliveryAttempts)
            .HasDefaultValue(0);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Invitation)
            .WithMany()
            .HasForeignKey(t => t.InvitationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.TokenHash)
            .HasDatabaseName("ix_confirmation_tokens_token_hash");

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("ix_confirmation_tokens_user_id_pending")
            .HasFilter("\"consumed_at_utc\" IS NULL")
            .IsUnique();
    }
}
