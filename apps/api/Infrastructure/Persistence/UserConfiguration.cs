using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(u => u.Role)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.TokenVersion)
            .HasDefaultValue(0);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.IsSuspended)
            .HasDefaultValue(false);

        builder.HasOne(u => u.Organisation)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.OrganisationId, u.Email })
            .IsUnique();
    }
}
