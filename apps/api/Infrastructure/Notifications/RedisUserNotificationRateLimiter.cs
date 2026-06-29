using StackExchange.Redis;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class RedisUserNotificationRateLimiter(IConnectionMultiplexer multiplexer) : IUserNotificationRateLimiter
{
    private const int MaxPerDay = 3;

    private static string Key(Guid userId, string notificationType)
        => $"user_notification:{userId:N}:{notificationType}:{DateTime.UtcNow:yyyy-MM-dd}";

    // Lua script atomically increments the counter and sets TTL on first creation.
    // Returns the new count. This avoids the TOCTOU race between check and increment
    // and the non-atomic INCR + KeyExpire sequence.
    private const string TryConsumeScript = @"
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], 86400)
        end
        return count
    ";

    public async Task<bool> CanSendAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        var db = multiplexer.GetDatabase();
        var key = Key(userId, notificationType);
        var count = (long)await db.ScriptEvaluateAsync(TryConsumeScript, new RedisKey[] { key });

        // Slot is atomically reserved. If under limit, caller may proceed.
        return count <= MaxPerDay;
    }

    public Task RecordSendAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        // Slot was already reserved atomically in CanSendAsync — no additional Redis call needed.
        return Task.CompletedTask;
    }
}
