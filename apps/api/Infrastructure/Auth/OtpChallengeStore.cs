using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class OtpChallengeStore(IConnectionMultiplexer redis, IOptions<OtpOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly OtpOptions _options = options.Value;

    public async Task<Guid> CreateAsync(OtpChallenge challenge, CancellationToken cancellationToken = default)
    {
        await RevokeActiveForUserAsync(challenge.UserId, cancellationToken);

        var challengeId = Guid.NewGuid();
        var key = GetKey(challengeId);
        var json = JsonSerializer.Serialize(challenge, JsonOptions);
        var expiry = TimeSpan.FromMinutes(_options.ExpiryMinutes);

        await _db.StringSetAsync(key, json, expiry);
        await _db.StringSetAsync(GetUserActiveKey(challenge.UserId), challengeId.ToString("D"), expiry);

        return challengeId;
    }

    public async Task<OtpChallenge?> GetAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await _db.StringGetAsync(GetKey(challengeId));
        return Deserialize(json);
    }

    public async Task<bool> RecordFailedAttemptAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = await GetAsync(challengeId, cancellationToken);
        if (challenge is null)
        {
            return false;
        }

        challenge.FailedAttempts++;
        if (challenge.FailedAttempts >= _options.MaxAttempts)
        {
            await RemoveAsync(challengeId, cancellationToken);
        }
        else
        {
            await UpdateAsync(challengeId, challenge, cancellationToken);
        }

        return true;
    }

    public async Task<bool> TryFinalizeAsync(
        Guid challengeId,
        string expectedOtpHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = await GetAsync(challengeId, cancellationToken);
        if (challenge is null || !string.Equals(challenge.OtpHash, expectedOtpHash, StringComparison.Ordinal))
        {
            return false;
        }

        var consumedJson = await _db.StringGetDeleteAsync(GetKey(challengeId));
        if (consumedJson.IsNullOrEmpty)
        {
            return false;
        }

        var consumed = Deserialize(consumedJson!);
        if (consumed is null)
        {
            return false;
        }

        await _db.KeyDeleteAsync(GetUserActiveKey(consumed.UserId));
        return true;
    }

    public async Task RemoveAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = await GetAsync(challengeId, cancellationToken);
        await _db.KeyDeleteAsync(GetKey(challengeId));

        if (challenge is not null)
        {
            await _db.KeyDeleteAsync(GetUserActiveKey(challenge.UserId));
        }
    }

    public int ExpirySeconds => _options.ExpiryMinutes * 60;

    private async Task RevokeActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingId = await _db.StringGetAsync(GetUserActiveKey(userId));
        if (existingId.IsNullOrEmpty || !Guid.TryParse(existingId.ToString(), out var challengeId))
        {
            return;
        }

        await RemoveAsync(challengeId, cancellationToken);
    }

    private async Task UpdateAsync(
        Guid challengeId,
        OtpChallenge challenge,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(challenge, JsonOptions);
        var expiry = TimeSpan.FromMinutes(_options.ExpiryMinutes);
        await _db.StringSetAsync(GetKey(challengeId), json, expiry);
    }

    private static OtpChallenge? Deserialize(RedisValue json)
    {
        if (json.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OtpChallenge>(json.ToString(), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RedisKey GetKey(Guid challengeId) => $"otp:challenge:{challengeId:D}";

    private static RedisKey GetUserActiveKey(Guid userId) => $"otp:active:{userId:D}";
}
