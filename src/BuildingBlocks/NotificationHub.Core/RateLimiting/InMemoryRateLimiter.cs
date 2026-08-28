using System.Collections.Concurrent;

namespace NotificationHub.Core.RateLimiting;

public sealed class InMemoryRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public Task<bool> IsAllowedAsync(string key, int limitPerMinute = 60, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var window = _windows.AddOrUpdate(key,
            _ => new Window(now, 1),
            (_, existing) =>
            {
                if ((now - existing.Start).TotalMinutes >= 1)
                    return new Window(now, 1);
                return existing with { Count = existing.Count + 1 };
            });

        return Task.FromResult(window.Count <= limitPerMinute);
    }

    private sealed record Window(DateTimeOffset Start, int Count);
}
