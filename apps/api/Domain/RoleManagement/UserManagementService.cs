using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Admin;

namespace MidiKaval.Api.Domain.RoleManagement;

public class UserManagementService(AppDbContext db)
{
    public async Task<AdminUserListResult> GetUserListAsync(
        Guid organisationId,
        int page = 1,
        int pageSize = 25,
        string? searchTerm = null,
        string? roles = null,
        string? statusFilter = null,
        string? sortBy = null,
        bool sortDescending = true,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<User> query = db.Users
            .AsNoTracking()
            .Where(u => u.OrganisationId == organisationId);

        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = EscapeLikePattern(searchTerm);
            query = ApplySearchFilter(query, term);
        }

        // Role filter (comma-separated, case-insensitive)
        if (!string.IsNullOrWhiteSpace(roles))
        {
            var roleList = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roleList.Length > 0)
            {
                var loweredRoles = roleList.Select(r => r.ToLowerInvariant()).ToList();
                query = query.Where(u => loweredRoles.Contains(u.Role.ToLower()));
            }
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = statusFilter.ToLowerInvariant() switch
            {
                "active" => query.Where(u => u.IsActive && !u.IsSuspended),
                "suspended" => query.Where(u => u.IsSuspended),
                var unknown => throw new ArgumentOutOfRangeException(
                    nameof(statusFilter),
                    $"Unknown status filter: '{unknown}'. Expected 'active' or 'suspended'.")
            };
        }

        // Dynamic sort with tiebreaker for deterministic pagination
        IOrderedQueryable<User> orderedQuery = (sortBy, sortDescending) switch
        {
            ("name", false) => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id),
            ("name", true) => query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName).ThenBy(u => u.Id),
            ("email", false) => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            ("email", true) => query.OrderByDescending(u => u.Email).ThenBy(u => u.Id),
            ("role", false) => query.OrderBy(u => u.Role).ThenBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("role", true) => query.OrderByDescending(u => u.Role).ThenByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("status", false) => query.OrderBy(u => u.IsSuspended).ThenBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            ("status", true) => query.OrderByDescending(u => u.IsSuspended).ThenByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            _ when sortDescending => query.OrderByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id),
            _ => query.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
        };

        var totalCount = await orderedQuery.CountAsync(ct);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummary(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.IsActive,
                u.IsSuspended,
                u.CreatedAtUtc))
            .ToListAsync(ct);

        return new AdminUserListResult(items, totalCount, page, pageSize);
    }

    protected virtual IQueryable<User> ApplySearchFilter(IQueryable<User> query, string term)
    {
        return query.Where(u =>
            EF.Functions.ILike(u.Email, $"%{term}%", @"\") ||
            EF.Functions.ILike(u.FirstName + " " + u.LastName, $"%{term}%", @"\"));
    }

    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
