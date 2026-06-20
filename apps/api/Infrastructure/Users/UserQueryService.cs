using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Users;

public sealed class UserQueryService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public async Task<IReadOnlyList<FieldWorkerUserDto>> ListFieldWorkersAsync(
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveOrganisationId();

        return await db.Users
            .Where(u =>
                u.OrganisationId == organisationId
                && u.IsActive
                && (u.Role == UserRoles.SocialWorker || u.Role == UserRoles.CaseWorker))
            .OrderBy(u => u.Email)
            .Select(u => new FieldWorkerUserDto
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role,
            })
            .ToListAsync(cancellationToken);
    }

    private Guid ResolveOrganisationId()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var organisationClaim = httpContext.User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (!Guid.TryParse(organisationClaim, out var organisationId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return organisationId;
    }
}
