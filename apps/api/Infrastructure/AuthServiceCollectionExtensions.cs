using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddMidiKavalAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddOptions<OtpOptions>()
            .Bind(configuration.GetSection(OtpOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OtpOptions>, OtpOptionsValidator>();

        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        var redisConnection = configuration.GetConnectionString("Redis");
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnection);

        var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddStackExchangeRedisCache(options =>
        {
            options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
        });

        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<OtpChallengeStore>();
        services.AddSingleton<AuthVerifiedStore>();
        services.AddSingleton<RefreshTokenStore>();
        services.AddSingleton<PasswordResetTokenStore>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<AuthService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, InactiveUserAuthorizationMiddlewareResultHandler>();
        services.AddSingleton<IAuthorizationHandler, ActiveUserAuthorizationHandler>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateTokenVersionAndUserStatusAsync,
                };
            });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveUserRequirement())
                .Build();

            AddActiveUserRolePolicy(options, Policies.DirectorOnly, UserRoles.Director);
            AddActiveUserRolePolicy(
                options,
                Policies.CoordinatorOrAbove,
                UserRoles.Director,
                UserRoles.Coordinator);
            AddActiveUserRolePolicy(
                options,
                Policies.FieldWorker,
                UserRoles.SocialWorker,
                UserRoles.CaseWorker);
            AddActiveUserRolePolicy(options, Policies.Director, UserRoles.Director);
            AddActiveUserRolePolicy(options, Policies.Coordinator, UserRoles.Coordinator);
            AddActiveUserRolePolicy(options, Policies.SocialWorker, UserRoles.SocialWorker);
            AddActiveUserRolePolicy(options, Policies.CaseWorker, UserRoles.CaseWorker);
        });

        var rateLimitOptions = configuration.GetSection(AuthRateLimitOptions.SectionName).Get<AuthRateLimitOptions>()
            ?? new AuthRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Too many authentication attempts. Please try again later.",
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                };

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    cancellationToken);
            };

            options.AddPolicy("auth-login", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-verify", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-refresh", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-logout", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-forgot-password", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-reset-password", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-step-up", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-verify-step-up", CreateAuthRateLimitPartition(rateLimitOptions));
        });

        return services;
    }

    private static void AddActiveUserRolePolicy(
        AuthorizationOptions options,
        string policyName,
        params string[] roles)
    {
        options.AddPolicy(policyName, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(roles)
                .AddRequirements(new ActiveUserRequirement()));
    }

    private static async Task ValidateTokenVersionAndUserStatusAsync(TokenValidatedContext context)
    {
        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub);

        var tokenVersionClaim = context.Principal?.FindFirst(AuthClaimTypes.TokenVersion);

        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Fail("Invalid token subject.");
            return;
        }

        await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            context.Fail("User not found.");
            return;
        }

        if (!user.IsActive)
        {
            context.HttpContext.Items[InactiveUserAuthConstants.InactiveUserItemKey] = true;
            return;
        }

        if (tokenVersionClaim is null
            || !int.TryParse(tokenVersionClaim.Value, out var tokenVersion)
            || tokenVersion != user.TokenVersion)
        {
            context.Fail("Token version mismatch.");
            return;
        }
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateAuthRateLimitPartition(
        AuthRateLimitOptions rateLimitOptions) =>
        httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? httpContext.Connection.Id;
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.RateLimitPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.RateLimitWindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
        };
}
