using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Notifications;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class UserDeviceService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private const int MaxDeviceInstallIdLength = 64;
    private const int MaxPushTokenLength = 512;

    private static readonly HashSet<string> AllowedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android",
        "ios",
    };

    public async Task<UserDeviceDto> RegisterAsync(
        RegisterUserDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRegisterRequest(request);

        var (organisationId, userId) = ResolveActorContext();
        var now = DateTime.UtcNow;
        var normalizedPlatform = request.Platform.Trim().ToLowerInvariant();
        var deviceInstallId = request.DeviceInstallId.Trim();
        var pushToken = request.PushToken.Trim();

        var staleTokenRows = await db.UserDevices
            .Where(d => d.PushToken == pushToken && d.UserId != userId)
            .ToListAsync(cancellationToken);

        if (staleTokenRows.Count > 0)
        {
            db.UserDevices.RemoveRange(staleTokenRows);
        }

        var existing = await db.UserDevices.SingleOrDefaultAsync(
            d => d.UserId == userId && d.DeviceInstallId == deviceInstallId,
            cancellationToken);

        if (existing is null)
        {
            existing = new UserDevice
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                UserId = userId,
                DeviceInstallId = deviceInstallId,
                Platform = normalizedPlatform,
                PushToken = pushToken,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastRegisteredAtUtc = now,
            };

            db.UserDevices.Add(existing);
        }
        else
        {
            existing.Platform = normalizedPlatform;
            existing.PushToken = pushToken;
            existing.UpdatedAtUtc = now;
            existing.LastRegisteredAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return MapToDto(existing);
    }

    public async Task RemoveForUserAsync(
        Guid userId,
        string deviceInstallId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceInstallId))
        {
            return;
        }

        var normalizedDeviceInstallId = deviceInstallId.Trim();
        var device = await db.UserDevices.SingleOrDefaultAsync(
            d => d.UserId == userId && d.DeviceInstallId == normalizedDeviceInstallId,
            cancellationToken);

        if (device is not null)
        {
            db.UserDevices.Remove(device);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var devices = await db.UserDevices
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return;
        }

        db.UserDevices.RemoveRange(devices);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRegisterRequest(RegisterUserDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceInstallId)
            || string.IsNullOrWhiteSpace(request.Platform)
            || string.IsNullOrWhiteSpace(request.PushToken))
        {
            throw new UserDeviceValidationException("deviceInstallId, platform, and pushToken are required.");
        }

        if (!AllowedPlatforms.Contains(request.Platform.Trim()))
        {
            throw new UserDeviceValidationException("platform must be android or ios.");
        }

        if (request.DeviceInstallId.Trim().Length > MaxDeviceInstallIdLength)
        {
            throw new UserDeviceValidationException(
                $"deviceInstallId must be at most {MaxDeviceInstallIdLength} characters.");
        }

        if (request.PushToken.Trim().Length > MaxPushTokenLength)
        {
            throw new UserDeviceValidationException(
                $"pushToken must be at most {MaxPushTokenLength} characters.");
        }
    }

    private static UserDeviceDto MapToDto(UserDevice device) =>
        new()
        {
            Id = device.Id,
            DeviceInstallId = device.DeviceInstallId,
            Platform = device.Platform,
            LastRegisteredAtUtc = device.LastRegisteredAtUtc,
        };

    private (Guid OrganisationId, Guid UserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, userId);
    }
}

public sealed class UserDeviceValidationException(string message) : Exception(message);
