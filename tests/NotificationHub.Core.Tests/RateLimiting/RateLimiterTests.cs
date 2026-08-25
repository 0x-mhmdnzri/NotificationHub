using FluentAssertions;
using NotificationHub.Core.RateLimiting;

namespace NotificationHub.Core.Tests.RateLimiting;

/// <summary>
/// SEC-23 related behavior of fixed-window limiter (in-memory).
/// Requirement: limit N requests per key per minute window.
/// </summary>
public class RateLimiterTests
{
    [Fact]
    public async Task TC_F_RL_001_Allows_UpToLimit()
    {
        var rl = new InMemoryRateLimiter();
        for (var i = 0; i < 5; i++)
            (await rl.IsAllowedAsync("k", 5)).Should().BeTrue();
    }

    [Fact]
    public async Task TC_E_RL_002_Blocks_OverLimit()
    {
        var rl = new InMemoryRateLimiter();
        for (var i = 0; i < 3; i++)
            await rl.IsAllowedAsync("auth-fail:ip:1.2.3.4", 3);
        (await rl.IsAllowedAsync("auth-fail:ip:1.2.3.4", 3)).Should().BeFalse();
    }

    [Fact]
    public async Task TC_F_RL_003_DifferentKeys_Independent()
    {
        var rl = new InMemoryRateLimiter();
        for (var i = 0; i < 2; i++)
            await rl.IsAllowedAsync("a", 2);
        (await rl.IsAllowedAsync("a", 2)).Should().BeFalse();
        (await rl.IsAllowedAsync("b", 2)).Should().BeTrue();
    }
}
