using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Preferences;

/// <summary>F10 embed + F11 weekly schedule / critical.</summary>
public class PreferenceScheduleTests
{
    [Fact]
    public async Task TC_F_PREF_010_EmbedModel_Schema()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "u1",
            ChannelOptIn = new Dictionary<string, bool> { ["email"] = true, ["sms"] = false }
        });
        var embed = await sut.GetEmbedModelAsync("u1");
        embed.SchemaVersion.Should().Be("1.0");
        embed.Channels.Should().Contain(c => c.Channel == "sms" && c.Enabled == false);
    }

    [Fact]
    public async Task TC_F_PREF_011_Critical_BypassesQuietHours()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "u1",
            QuietHoursStart = "00:00",
            QuietHoursEnd = "23:59",
            TimeZoneId = "UTC"
        });
        var (blocked, _) = await sut.CanSendAsync("u1", "email", null, null, isCritical: false);
        blocked.Should().BeFalse();
        var (allowed, _) = await sut.CanSendAsync("u1", "email", null, null, isCritical: true);
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task TC_F_PREF_012_Critical_DoesNotBypassOptOut()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PreferenceService(db);
        await sut.SaveAsync(new UserPreference
        {
            UserId = "u1",
            ChannelOptIn = new Dictionary<string, bool> { ["email"] = false }
        });
        var (allowed, reason) = await sut.CanSendAsync("u1", "email", null, null, isCritical: true);
        allowed.Should().BeFalse();
        reason.Should().Contain("opted out");
    }
}
