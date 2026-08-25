using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Throttle;

public sealed class ThrottleService : IThrottleService
{
    private readonly NotificationDbContext _db;
    public ThrottleService(NotificationDbContext db) => _db = db;

    public async Task<ThrottlePolicy> SavePolicyAsync(ThrottlePolicy policy, CancellationToken ct = default)
    {
        var e = await _db.ThrottlePolicies.FirstOrDefaultAsync(x => x.Key == policy.Key && x.TenantId == policy.TenantId, ct);
        if (e is null)
        {
            e = new ThrottlePolicyEntity { Id = policy.Id == Guid.Empty ? Guid.NewGuid() : policy.Id };
            _db.ThrottlePolicies.Add(e);
        }
        e.Key = policy.Key;
        e.TenantId = policy.TenantId;
        e.Channel = policy.Channel;
        e.MaxCount = Math.Max(1, policy.MaxCount);
        e.WindowMinutes = Math.Clamp(policy.WindowMinutes, 1, 24 * 60);
        e.IsActive = policy.IsActive;
        await _db.SaveChangesAsync(ct);
        return new ThrottlePolicy
        {
            Id = e.Id, Key = e.Key, TenantId = e.TenantId, Channel = e.Channel,
            MaxCount = e.MaxCount, WindowMinutes = e.WindowMinutes, IsActive = e.IsActive
        };
    }

    public async Task<(bool Allowed, string? Reason)> CheckAndIncrementAsync(string recipient, string? channel, string? tenantId, CancellationToken ct = default)
    {
        var policies = await _db.ThrottlePolicies.AsNoTracking()
            .Where(x => x.IsActive && (x.TenantId == null || x.TenantId == tenantId))
            .ToListAsync(ct);

        foreach (var p in policies)
        {
            if (!string.IsNullOrEmpty(p.Channel) && !string.Equals(p.Channel, channel, StringComparison.OrdinalIgnoreCase))
                continue;

            var windowStart = AlignWindow(DateTimeOffset.UtcNow, p.WindowMinutes);
            var counter = await _db.ThrottleCounters.FirstOrDefaultAsync(x =>
                x.PolicyKey == p.Key && x.Recipient == recipient && x.WindowStart == windowStart
                && x.Channel == p.Channel && x.TenantId == tenantId, ct);

            if (counter is null)
            {
                counter = new ThrottleCounterEntity
                {
                    PolicyKey = p.Key,
                    Recipient = recipient,
                    TenantId = tenantId,
                    Channel = p.Channel,
                    WindowStart = windowStart,
                    Count = 0
                };
                _db.ThrottleCounters.Add(counter);
            }

            if (counter.Count >= p.MaxCount)
                return (false, $"Throttle policy '{p.Key}' exceeded ({p.MaxCount}/{p.WindowMinutes}m)");

            counter.Count++;
            await _db.SaveChangesAsync(ct);
        }

        return (true, null);
    }

    private static DateTimeOffset AlignWindow(DateTimeOffset now, int windowMinutes)
    {
        var minutes = (now.UtcDateTime.Hour * 60 + now.UtcDateTime.Minute) / windowMinutes * windowMinutes;
        return new DateTimeOffset(now.UtcDateTime.Date.AddMinutes(minutes), TimeSpan.Zero);
    }
}
