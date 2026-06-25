using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Middleware;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.UnitTests.Middleware;

public class TokenVersionMiddlewareTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (DefaultHttpContext httpContext, ClaimsPrincipal user) CreateAuthenticatedContext(
        Guid userId, int jwtTokenVersion)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(AuthClaimTypes.TokenVersion, jwtTokenVersion.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = user,
        };
        httpContext.Request.Path = "/api/v1/admin/users";

        return (httpContext, user);
    }

    private static DefaultHttpContext CreateUnauthenticatedContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/admin/users";
        return httpContext;
    }

    private static async Task SeedUser(AppDbContext db, Guid userId, int tokenVersion)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        db.Users.Add(new User
        {
            Id = userId,
            OrganisationId = org.Id,
            Email = "test@example.org",
            FirstName = "Test",
            LastName = "User",
            Role = UserRoles.Director,
            TokenVersion = tokenVersion,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns401_WhenJwtTokenVersionIsStale()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        await SeedUser(db, userId, tokenVersion: 5);

        var (httpContext, _) = CreateAuthenticatedContext(userId, jwtTokenVersion: 3);

        var middleware = new TokenVersionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task PassesThrough_WhenTokenVersionMatches()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        await SeedUser(db, userId, tokenVersion: 3);

        var (httpContext, _) = CreateAuthenticatedContext(userId, jwtTokenVersion: 3);

        var wasInvoked = false;
        var middleware = new TokenVersionMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task PassesThrough_WhenJwtTokenVersionIsGreater()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        await SeedUser(db, userId, tokenVersion: 3);

        var (httpContext, _) = CreateAuthenticatedContext(userId, jwtTokenVersion: 5);

        var wasInvoked = false;
        var middleware = new TokenVersionMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task PassesThrough_ForUnauthenticatedRequests()
    {
        using var db = CreateContext();
        var httpContext = CreateUnauthenticatedContext();

        var wasInvoked = false;
        var middleware = new TokenVersionMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
    }

    [Fact]
    public async Task SkipsExcludedPaths()
    {
        using var db = CreateContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/health";

        var wasInvoked = false;
        var middleware = new TokenVersionMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
    }
}
