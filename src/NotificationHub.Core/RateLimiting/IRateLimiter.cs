namespace NotificationHub.Core.RateLimiting;

public interface IRateLimiter
{
    Task<bool> IsAllowedAsync(string key, int limitPerMinute = 60, CancellationToken ct = default);
}
