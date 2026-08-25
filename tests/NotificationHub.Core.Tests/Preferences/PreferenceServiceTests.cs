using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Preferences;

public class PreferenceServiceTests
{
    [Fact]
    public async Task TC_F_010_CanSend_NoPreference_Allows()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);

        var (allowed, reason) = await sut.CanSendAsync("user-1", "sms", null, null);

        allowed.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public async Task TC_F_011_CanSend_ChannelOptOut_Blocks()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "user-1",
            ChannelOptIn = new Dictionary<string, bool> { ["sms"] = false }
        });

        var (allowed, reason) = await sut.CanSendAsync("user-1", "sms", null, null);

        allowed.Should().BeFalse();
        reason.Should().Contain("opted out");
    }

    [Fact]
    public async Task TC_F_012_CanSend_CategoryOptOut_Blocks()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "user-1",
            CategoryOptIn = new Dictionary<string, bool> { ["marketing"] = false }
        });

        var (allowed, reason) = await sut.CanSendAsync("user-1", "email", "marketing", null);

        allowed.Should().BeFalse();
        reason.Should().Contain("category");
    }

    [Fact]
    public async Task TC_F_013_SaveAndGet_RoundTrip()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "user-2",
            TenantId = "t1",
            PreferredChannel = "sms",
            MaxPerDay = 5
        });

        var loaded = await sut.GetAsync("user-2", "t1");

        loaded.Should().NotBeNull();
        loaded!.PreferredChannel.Should().Be("sms");
        loaded.MaxPerDay.Should().Be(5);
    }
}
