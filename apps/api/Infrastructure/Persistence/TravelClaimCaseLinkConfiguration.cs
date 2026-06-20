using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MidiKaval.Api.Domain.Entities;

namespace MidiKaval.Api.Infrastructure.Persistence;

public sealed class TravelClaimCaseLinkConfiguration : IEntityTypeConfiguration<TravelClaimCaseLink>
{
    public void Configure(EntityTypeBuilder<TravelClaimCaseLink> builder)
    {
        builder.ToTable("travel_claim_cases");

        builder.HasKey(l => new { l.TravelClaimId, l.CaseId });

        builder.HasIndex(l => new { l.OrganisationId, l.CaseId });

        builder.HasOne<TravelClaim>()
            .WithMany()
            .HasForeignKey(l => l.TravelClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(l => l.CaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
