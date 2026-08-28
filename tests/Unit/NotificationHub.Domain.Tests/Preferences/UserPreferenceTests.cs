using FluentAssertions;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Preferences;
using NotificationHub.Domain.Preferences.ValueObjects;

namespace NotificationHub.Domain.Tests.Preferences;

public class UserPreferenceTests
{
    [Fact]
    public void TC_DDD_P01_Hard_opt_out_blocks_even_critical()
    {
        var p = UserPreference.Create(PreferenceId.New(), UserId.Create("u1"), null, DateTimeOffset.UtcNow);
        p.SetChannelOptIn("sms", false, DateTimeOffset.UtcNow);
        p.AllowsChannel("sms", isCritical: true).Should().BeFalse();
        p.AllowsChannel("email", isCritical: false).Should().BeTrue();
    }

    [Fact]
    public void TC_DDD_P02_MaxPerDay_cannot_be_negative()
    {
        var p = UserPreference.Create(PreferenceId.New(), UserId.Create("u1"), null, DateTimeOffset.UtcNow);
        var act = () => p.SetMaxPerDay(-1, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }
}
