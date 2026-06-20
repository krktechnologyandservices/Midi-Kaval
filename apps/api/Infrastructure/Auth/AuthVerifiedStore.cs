using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class AuthVerifiedStore(IConnectionMultiplexer redis, IOptions<OtpOptions> options)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(options.Value.ExpiryMinutes);

    public Task SetLoginVerifiedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _db.StringSetAsync(GetLoginKey(userId), "1", _ttl);
    }

    public Task SetStepUpVerifiedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _db.StringSetAsync(GetStepUpKey(userId), "1", _ttl);
    }

    public async Task<bool> HasRecentVerificationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await _db.KeyExistsAsync(GetLoginKey(userId)))
        {
            return true;
        }

        return await _db.KeyExistsAsync(GetStepUpKey(userId));
    }

    public Task ClearVerificationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.WhenAll(
            _db.KeyDeleteAsync(GetLoginKey(userId)),
            _db.KeyDeleteAsync(GetStepUpKey(userId)));
    }

    private static RedisKey GetLoginKey(Guid userId) => $"auth:otp-verified:{userId:D}";

    private static RedisKey GetStepUpKey(Guid userId) => $"auth:step-up-verified:{userId:D}";
}
