using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TargetEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.Role)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(InvitationStatus.Pending);

        builder.Property(i => i.ExpiresAtUtc)
            .IsRequired();

        builder.Property(i => i.ConfirmedAtUtc)
            .IsRequired(false);

        builder.HasOne(i => i.Organisation)
            .WithMany()
            .HasForeignKey(i => i.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.InvitedByUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.OrganisationId, i.TargetEmail, i.Status })
            .IsUnique()
            .HasFilter("status = 'pending'");
    }
}
