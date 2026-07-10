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
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
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

        services.AddOptions<DualAuthOptions>()
            .Bind(configuration.GetSection(DualAuthOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<TotpOptions>()
            .Bind(configuration.GetSection(TotpOptions.SectionName))
            .ValidateOnStart();

        var redisConnection = configuration.GetConnectionString("Redis");
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnection);

        // AbortOnConnectFail defaults to true, which crashes the whole process if Redis
        // isn't reachable at the exact instant the app boots (e.g. a Render cold-start
        // race where the API container comes up before the Redis service is ready).
        // Disabling it lets the multiplexer keep retrying in the background instead.
        var redisOptions = ConfigurationOptions.Parse(redisConnection);
        redisOptions.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
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
        services.AddScoped<TwoFactorService>();
        services.AddScoped<BackupCodeService>();
        services.AddScoped<AdminTwoFactorService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IUserNotificationRateLimiter, RedisUserNotificationRateLimiter>();
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
            AddActiveUserRolePolicy(
                options,
                Policies.AccountantOrAbove,
                UserRoles.Director,
                UserRoles.Accountant);
            AddActiveUserRolePolicy(
                options,
                Policies.BudgetViewer,
                UserRoles.Director,
                UserRoles.Coordinator,
                UserRoles.Accountant);
            AddActiveUserRolePolicy(
                options,
                Policies.VendorOnly,
                UserRoles.Vendor);
            AddActiveUserRolePolicy(
                options,
                Policies.DirectorOrVendor,
                UserRoles.Director,
                UserRoles.Vendor);
        });

        var rateLimitOptions = configuration.GetSection(AuthRateLimitOptions.SectionName).Get<AuthRateLimitOptions>()
            ?? new AuthRateLimitOptions();
        var dataRateLimitOptions = configuration.GetSection(DataRateLimitOptions.SectionName).Get<DataRateLimitOptions>()
            ?? new DataRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                // Use a generic message since OnRejectedContext does not expose the policy name
                const string detail = "Too many requests. Please try again later.";

                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = detail,
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                };

                context.HttpContext.Response.ContentType = "application/problem+json";
                context.HttpContext.Response.Headers.RetryAfter = "60";
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
            options.AddPolicy("auth-activate", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-activate-read", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-enroll-totp", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-verify-totp", CreateAuthRateLimitPartition(rateLimitOptions));
            options.AddPolicy("auth-verify-backup-code", CreateAuthRateLimitPartition(rateLimitOptions));

            options.AddPolicy("data-read", CreateDataReadRateLimitPartition(dataRateLimitOptions));
            options.AddPolicy("data-write", CreateDataWriteRateLimitPartition(dataRateLimitOptions));

            options.AddPolicy("vendor-create", httpContext =>
            {
                // Partition by authenticated user identity so one vendor cannot exhaust another's budget
                var partitionKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy("vendor-read", httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy("admin-bypass-code", httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 2,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    });
            });
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
                ?? "unknown";
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

    private static Func<HttpContext, RateLimitPartition<string>> CreateDataReadRateLimitPartition(
        DataRateLimitOptions dataOptions) =>
        httpContext =>
        {
            if (httpContext.User.IsInRole(UserRoles.Director))
            {
                return RateLimitPartition.GetNoLimiter("director-bypass");
            }

            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = dataOptions.ReadPermitLimit,
                    Window = TimeSpan.FromSeconds(dataOptions.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
        };

    private static Func<HttpContext, RateLimitPartition<string>> CreateDataWriteRateLimitPartition(
        DataRateLimitOptions dataOptions) =>
        httpContext =>
        {
            if (httpContext.User.IsInRole(UserRoles.Director))
            {
                return RateLimitPartition.GetNoLimiter("director-bypass");
            }

            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = dataOptions.WritePermitLimit,
                    Window = TimeSpan.FromSeconds(dataOptions.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
        };
}
