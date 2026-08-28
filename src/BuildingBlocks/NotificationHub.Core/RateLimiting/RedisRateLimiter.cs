using StackExchange.Redis;

namespace NotificationHub.Core.RateLimiting;

/// <summary>Distributed fixed-window rate limiter (SEC-24 / F28). Falls back not used — registered only when Redis is configured.</summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _mux;
    private readonly string _prefix;

    public RedisRateLimiter(IConnectionMultiplexer mux, string prefix = "nh:rl:")
    {
        _mux = mux;
        _prefix = prefix;
    }

    public async Task<bool> IsAllowedAsync(string key, int limitPerMinute = 60, CancellationToken ct = default)
    {
        var db = _mux.GetDatabase();
        var redisKey = $"{_prefix}{key}:{DateTimeOffset.UtcNow:yyyyMMddHHmm}";
        var count = await db.StringIncrementAsync(redisKey);
        if (count == 1)
            await db.KeyExpireAsync(redisKey, TimeSpan.FromMinutes(2));
        return count <= limitPerMinute;
    }
}
