using System.Collections.Concurrent;

namespace MidiKaval.Api.Infrastructure.Notifications;

public sealed class FakeUserNotificationRateLimiter : IUserNotificationRateLimiter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    public int MaxPerDay { get; set; } = 3;

    private static string Key(Guid userId, string notificationType)
        => $"{userId:N}:{notificationType}:{DateTime.UtcNow:yyyy-MM-dd}";

    public Task<bool> CanSendAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        var count = _counts.GetValueOrDefault(Key(userId, notificationType), 0);
        return Task.FromResult(count < MaxPerDay);
    }

    public Task RecordSendAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        _counts.AddOrUpdate(Key(userId, notificationType), 1, (_, c) => c + 1);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _counts.Clear();
    }
}
