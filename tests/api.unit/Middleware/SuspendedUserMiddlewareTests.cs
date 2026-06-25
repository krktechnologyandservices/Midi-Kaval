using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Middleware;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.UnitTests.Middleware;

public class SuspendedUserMiddlewareTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = user,
        };
        httpContext.Request.Path = "/api/v1/admin/users";
        return httpContext;
    }

    private static DefaultHttpContext CreateUnauthenticatedContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/admin/users";
        return httpContext;
    }

    private static async Task<Guid> SeedUser(AppDbContext db, bool isSuspended)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organisations.Add(org);
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            Email = "test@example.org",
            FirstName = "Test",
            LastName = "User",
            Role = UserRoles.Director,
            TokenVersion = 0,
            IsActive = true,
            IsSuspended = isSuspended,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Returns403_WhenUserIsSuspended()
    {
        using var db = CreateContext();
        var userId = await SeedUser(db, isSuspended: true);
        var httpContext = CreateAuthenticatedContext(userId);

        var middleware = new SuspendedUserMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task PassesThrough_WhenUserIsNotSuspended()
    {
        using var db = CreateContext();
        var userId = await SeedUser(db, isSuspended: false);
        var httpContext = CreateAuthenticatedContext(userId);

        var wasInvoked = false;
        var middleware = new SuspendedUserMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
    }

    [Fact]
    public async Task PassesThrough_ForUnauthenticatedRequests()
    {
        using var db = CreateContext();
        var httpContext = CreateUnauthenticatedContext();

        var wasInvoked = false;
        var middleware = new SuspendedUserMiddleware(_ =>
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
        var middleware = new SuspendedUserMiddleware(_ =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(httpContext, db, default);

        Assert.True(wasInvoked);
    }
}
