using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Throttle;

public interface IThrottleService
{
    Task<ThrottlePolicy> SavePolicyAsync(ThrottlePolicy policy, CancellationToken ct = default);
    Task<(bool Allowed, string? Reason)> CheckAndIncrementAsync(string recipient, string? channel, string? tenantId, CancellationToken ct = default);
}
