using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class BackupCodeConfiguration : IEntityTypeConfiguration<BackupCode>
{
    public void Configure(EntityTypeBuilder<BackupCode> builder)
    {
        builder.ToTable("backup_codes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeHash)
            .HasColumnName("code_hash")
            .IsRequired();

        builder.Property(x => x.Used)
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(x => x.UsedAtUtc)
            .HasColumnName("used_at_utc");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_backup_codes_user_id");

        builder.HasIndex(x => new { x.UserId, x.Used })
            .HasDatabaseName("ix_backup_codes_user_id_unused")
            .HasFilter("\"used\" = false");

        builder.HasOne(x => x.User)
            .WithMany(x => x.BackupCodes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
