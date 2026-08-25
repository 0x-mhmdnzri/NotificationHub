using FluentAssertions;
using NotificationHub.Core.RateLimiting;

namespace NotificationHub.Core.Tests.RateLimiting;

public class RateLimiterTests
{
    [Fact]
    public async Task TC_F_030_WithinLimit_Allows()
    {
        var sut = new InMemoryRateLimiter();
        var allowed = await sut.IsAllowedAsync("tenant:a:email", limitPerMinute: 3);
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task TC_E_030_ExceedsLimit_Blocks()
    {
        var sut = new InMemoryRateLimiter();
        await sut.IsAllowedAsync("tenant:b:sms", 2);
        await sut.IsAllowedAsync("tenant:b:sms", 2);
        var third = await sut.IsAllowedAsync("tenant:b:sms", 2);
        third.Should().BeFalse();
    }

    [Fact]
    public async Task TC_F_031_DifferentKeys_Independent()
    {
        var sut = new InMemoryRateLimiter();
        await sut.IsAllowedAsync("k1", 1);
        var blocked = await sut.IsAllowedAsync("k1", 1);
        var other = await sut.IsAllowedAsync("k2", 1);

        blocked.Should().BeFalse();
        other.Should().BeTrue();
    }
}
