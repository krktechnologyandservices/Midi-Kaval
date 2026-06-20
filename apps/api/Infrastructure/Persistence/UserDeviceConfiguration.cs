using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("user_devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceInstallId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.Platform)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(d => d.PushToken)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc)
            .IsRequired();

        builder.Property(d => d.LastRegisteredAtUtc)
            .IsRequired();

        builder.HasIndex(d => new { d.UserId, d.DeviceInstallId })
            .IsUnique();

        builder.HasIndex(d => d.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
