using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("in_app_notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.EventType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(n => n.ResourceType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(n => n.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc, n.CreatedAtUtc });

        builder.HasIndex(n => new { n.ResourceType, n.ResourceId, n.EventType });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
