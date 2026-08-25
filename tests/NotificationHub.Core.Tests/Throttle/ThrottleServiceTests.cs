using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Tests.Helpers;
using NotificationHub.Core.Throttle;

namespace NotificationHub.Core.Tests.Throttle;

/// <summary>F03 — frequency cap.</summary>
public class ThrottleServiceTests
{
    [Fact]
    public async Task TC_F_TH_001_Allows_UntilMax()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ThrottleService(db);
        await sut.SavePolicyAsync(new ThrottlePolicy { Key = "email-hourly", Channel = "email", MaxCount = 2, WindowMinutes = 60 });

        (await sut.CheckAndIncrementAsync("user@x.com", "email", null)).Allowed.Should().BeTrue();
        (await sut.CheckAndIncrementAsync("user@x.com", "email", null)).Allowed.Should().BeTrue();
        var third = await sut.CheckAndIncrementAsync("user@x.com", "email", null);
        third.Allowed.Should().BeFalse();
        third.Reason.Should().Contain("email-hourly");
    }

    [Fact]
    public async Task TC_F_TH_002_DifferentChannel_Independent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ThrottleService(db);
        await sut.SavePolicyAsync(new ThrottlePolicy { Key = "email-only", Channel = "email", MaxCount = 1, WindowMinutes = 60 });

        (await sut.CheckAndIncrementAsync("u", "email", null)).Allowed.Should().BeTrue();
        (await sut.CheckAndIncrementAsync("u", "email", null)).Allowed.Should().BeFalse();
        (await sut.CheckAndIncrementAsync("u", "sms", null)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task TC_F_TH_003_NoPolicies_AlwaysAllow()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ThrottleService(db);
        (await sut.CheckAndIncrementAsync("u", "email", null)).Allowed.Should().BeTrue();
    }
}
