using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class RefreshTokenStore(IConnectionMultiplexer redis, IOptions<RefreshTokenOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly LuaScript ConsumeScript = LuaScript.Prepare(@"
local revoked = redis.call('GET', @revokedKey)
if revoked then
    return { 2, revoked }
end
local token = redis.call('GET', @tokenKey)
if not token then
    return { 0 }
end
redis.call('DEL', @tokenKey)
redis.call('SET', @revokedKey, @userId, 'EX', @expirySeconds)
return { 1, token }
");

    private readonly IDatabase _db = redis.GetDatabase();
    private readonly RefreshTokenOptions _options = options.Value;

    private int TokenExpirySeconds => _options.ExpiryDays * 24 * 60 * 60;

    public async Task<string> IssueAsync(
        Guid userId,
        int tokenVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = GenerateToken();
        var hash = HashToken(token);
        var record = new RefreshTokenRecord { UserId = userId, TokenVersion = tokenVersion };
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var expiry = TimeSpan.FromDays(_options.ExpiryDays);

        await _db.StringSetAsync(GetTokenKey(hash), json, expiry);
        await _db.SetAddAsync(GetUserSetKey(userId), hash);
        await _db.ListRightPushAsync(GetUserOrderKey(userId), hash);

        await EnforceMaxActiveAsync(userId, cancellationToken);
        return token;
    }

    public async Task<RefreshTokenConsumeOutcome> TryPeekAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = HashToken(token);
        return await ReadTokenStateAsync(hash);
    }

    public async Task<RefreshTokenConsumeOutcome> TryConsumeAsync(
        string token,
        Guid expectedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = HashToken(token);

        var result = (RedisResult[]?)await _db.ScriptEvaluateAsync(
            ConsumeScript,
            new
            {
                tokenKey = (RedisKey)GetTokenKey(hash),
                revokedKey = (RedisKey)GetRevokedKey(hash),
                userId = expectedUserId.ToString("D"),
                expirySeconds = TokenExpirySeconds,
            });

        if (result is null || result.Length == 0)
        {
            return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.NotFound };
        }

        var status = (int)result[0];
        if (status == 0)
        {
            return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.NotFound };
        }

        if (status == 2)
        {
            var revokedUserId = result[1].ToString();
            if (!Guid.TryParse(revokedUserId, out var userId))
            {
                return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.ReuseDetected };
            }

            return new RefreshTokenConsumeOutcome
            {
                Result = RefreshTokenConsumeResult.ReuseDetected,
                Record = new RefreshTokenRecord { UserId = userId, TokenVersion = 0 },
            };
        }

        var json = result[1].ToString();
        var parsed = Deserialize(json);
        if (parsed is null)
        {
            return new RefreshTokenConsumeOutcome
            {
                Result = RefreshTokenConsumeResult.ReuseDetected,
                Record = new RefreshTokenRecord { UserId = expectedUserId, TokenVersion = 0 },
            };
        }

        if (parsed.UserId != expectedUserId)
        {
            return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.NotFound };
        }

        await RemoveFromUserIndexAsync(parsed.UserId, hash);

        return new RefreshTokenConsumeOutcome
        {
            Result = RefreshTokenConsumeResult.Success,
            Record = parsed,
        };
    }

    public async Task<RefreshTokenRecord?> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = HashToken(token);
        var json = await _db.StringGetDeleteAsync(GetTokenKey(hash));
        if (json.IsNullOrEmpty)
        {
            return null;
        }

        var record = Deserialize(json!);
        if (record is not null)
        {
            await RemoveFromUserIndexAsync(record.UserId, hash);
        }

        return record;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hashes = await _db.SetMembersAsync(GetUserSetKey(userId));
        foreach (var hash in hashes)
        {
            if (hash.IsNullOrEmpty)
            {
                continue;
            }

            await _db.KeyDeleteAsync(GetTokenKey(hash!));
            await _db.KeyDeleteAsync(GetRevokedKey(hash!));
        }

        await _db.KeyDeleteAsync(GetUserSetKey(userId));
        await _db.KeyDeleteAsync(GetUserOrderKey(userId));
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<RefreshTokenConsumeOutcome> ReadTokenStateAsync(string hash)
    {
        var revokedUserId = await _db.StringGetAsync(GetRevokedKey(hash));
        if (!revokedUserId.IsNullOrEmpty && Guid.TryParse(revokedUserId.ToString(), out var revokedUser))
        {
            return new RefreshTokenConsumeOutcome
            {
                Result = RefreshTokenConsumeResult.ReuseDetected,
                Record = new RefreshTokenRecord { UserId = revokedUser, TokenVersion = 0 },
            };
        }

        var json = await _db.StringGetAsync(GetTokenKey(hash));
        if (json.IsNullOrEmpty)
        {
            return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.NotFound };
        }

        var parsed = Deserialize(json!);
        if (parsed is null)
        {
            return new RefreshTokenConsumeOutcome { Result = RefreshTokenConsumeResult.NotFound };
        }

        return new RefreshTokenConsumeOutcome
        {
            Result = RefreshTokenConsumeResult.Success,
            Record = parsed,
        };
    }

    private async Task EnforceMaxActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var max = _options.MaxActivePerUser;
        if (max <= 0)
        {
            return;
        }

        while (true)
        {
            var length = await _db.ListLengthAsync(GetUserOrderKey(userId));
            if (length <= max)
            {
                break;
            }

            var oldest = await _db.ListLeftPopAsync(GetUserOrderKey(userId));
            if (oldest.IsNullOrEmpty)
            {
                break;
            }

            var hash = oldest.ToString();
            await _db.KeyDeleteAsync(GetTokenKey(hash));
            await _db.SetRemoveAsync(GetUserSetKey(userId), hash);
        }
    }

    private async Task RemoveFromUserIndexAsync(Guid userId, string hash)
    {
        await _db.SetRemoveAsync(GetUserSetKey(userId), hash);
        await _db.ListRemoveAsync(GetUserOrderKey(userId), hash, 1);
    }

    private static RefreshTokenRecord? Deserialize(RedisValue json)
    {
        if (json.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RefreshTokenRecord>(json.ToString(), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GenerateToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes);
    }

    private static RedisKey GetTokenKey(string hash) => $"refresh:token:{hash}";

    private static RedisKey GetRevokedKey(string hash) => $"refresh:revoked:{hash}";

    private static RedisKey GetUserSetKey(Guid userId) => $"refresh:user:{userId:D}";

    private static RedisKey GetUserOrderKey(Guid userId) => $"refresh:user:order:{userId:D}";
}
