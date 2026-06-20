using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class PasswordResetRecord
{
    public Guid UserId { get; set; }
}

public enum PasswordResetTokenState
{
    NotFound,
    ReuseDetected,
    Success,
}

public sealed class PasswordResetPeekOutcome
{
    public PasswordResetTokenState State { get; init; }
    public PasswordResetRecord? Record { get; init; }
}

public sealed class PasswordResetConsumeOutcome
{
    public PasswordResetTokenState State { get; init; }
    public PasswordResetRecord? Record { get; init; }
}

public sealed class PasswordResetTokenStore(
    IConnectionMultiplexer redis,
    IOptions<PasswordResetOptions> options)
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
redis.call('DEL', @userKey)
redis.call('SET', @revokedKey, @userId, 'EX', @expirySeconds)
return { 1, token }
");

    private readonly IDatabase _db = redis.GetDatabase();
    private readonly PasswordResetOptions _options = options.Value;

    private int TokenExpirySeconds => _options.ExpiryMinutes * 60;

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await RevokeActiveForUserAsync(userId, cancellationToken);

        var token = GenerateToken();
        var hash = RefreshTokenStore.HashToken(token);
        var record = new PasswordResetRecord { UserId = userId };
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var expiry = TimeSpan.FromMinutes(_options.ExpiryMinutes);

        await _db.StringSetAsync(GetTokenKey(hash), json, expiry);
        await _db.StringSetAsync(GetUserKey(userId), hash, expiry);

        return token;
    }

    public async Task<PasswordResetPeekOutcome> TryPeekAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = RefreshTokenStore.HashToken(token);
        return await ReadTokenStateAsync(hash);
    }

    public async Task<PasswordResetConsumeOutcome> TryConsumeAsync(
        string token,
        Guid expectedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = RefreshTokenStore.HashToken(token);

        var result = (RedisResult[]?)await _db.ScriptEvaluateAsync(
            ConsumeScript,
            new
            {
                tokenKey = (RedisKey)GetTokenKey(hash),
                revokedKey = (RedisKey)GetRevokedKey(hash),
                userKey = (RedisKey)GetUserKey(expectedUserId),
                userId = expectedUserId.ToString("D"),
                expirySeconds = TokenExpirySeconds,
            });

        if (result is null || result.Length == 0)
        {
            return new PasswordResetConsumeOutcome { State = PasswordResetTokenState.NotFound };
        }

        var status = (int)result[0];
        if (status == 0)
        {
            return new PasswordResetConsumeOutcome { State = PasswordResetTokenState.NotFound };
        }

        if (status == 2)
        {
            return new PasswordResetConsumeOutcome
            {
                State = PasswordResetTokenState.ReuseDetected,
                Record = new PasswordResetRecord { UserId = expectedUserId },
            };
        }

        var json = result[1].ToString();
        var parsed = Deserialize(json);
        if (parsed is null || parsed.UserId != expectedUserId)
        {
            return new PasswordResetConsumeOutcome { State = PasswordResetTokenState.NotFound };
        }

        return new PasswordResetConsumeOutcome
        {
            State = PasswordResetTokenState.Success,
            Record = parsed,
        };
    }

    private async Task RevokeActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activeHash = await _db.StringGetAsync(GetUserKey(userId));
        if (activeHash.IsNullOrEmpty)
        {
            return;
        }

        var hash = activeHash.ToString();
        await _db.KeyDeleteAsync(GetTokenKey(hash));
        await _db.KeyDeleteAsync(GetUserKey(userId));
    }

    private async Task<PasswordResetPeekOutcome> ReadTokenStateAsync(string hash)
    {
        var revokedUserId = await _db.StringGetAsync(GetRevokedKey(hash));
        if (!revokedUserId.IsNullOrEmpty)
        {
            if (Guid.TryParse(revokedUserId.ToString(), out var revokedUser))
            {
                return new PasswordResetPeekOutcome
                {
                    State = PasswordResetTokenState.ReuseDetected,
                    Record = new PasswordResetRecord { UserId = revokedUser },
                };
            }

            return new PasswordResetPeekOutcome { State = PasswordResetTokenState.ReuseDetected };
        }

        var json = await _db.StringGetAsync(GetTokenKey(hash));
        if (json.IsNullOrEmpty)
        {
            return new PasswordResetPeekOutcome { State = PasswordResetTokenState.NotFound };
        }

        var parsed = Deserialize(json!);
        if (parsed is null)
        {
            return new PasswordResetPeekOutcome { State = PasswordResetTokenState.NotFound };
        }

        return new PasswordResetPeekOutcome
        {
            State = PasswordResetTokenState.Success,
            Record = parsed,
        };
    }

    private static PasswordResetRecord? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PasswordResetRecord>(json, JsonOptions);
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

    private static RedisKey GetTokenKey(string hash) => $"password-reset:token:{hash}";

    private static RedisKey GetRevokedKey(string hash) => $"password-reset:revoked:{hash}";

    private static RedisKey GetUserKey(Guid userId) => $"password-reset:user:{userId:D}";
}
