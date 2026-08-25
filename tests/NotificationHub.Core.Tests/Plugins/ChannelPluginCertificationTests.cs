using FluentAssertions;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Plugins.Chat.Discord;
using NotificationHub.Plugins.Chat.Telegram;
using NotificationHub.Plugins.Push.Expo;

namespace NotificationHub.Core.Tests.Plugins;

/// <summary>
/// F21 — plugin certification checklist: contract surface, safe helpers, channel ids.
/// Requirement: every channel plugin exposes Id, Channel, Version and safe config behavior.
/// </summary>
public class ChannelPluginCertificationTests
{
    [Theory]
    [InlineData(typeof(TelegramPlugin), "chat-telegram", "telegram")]
    [InlineData(typeof(DiscordPlugin), "chat-discord", "discord")]
    [InlineData(typeof(ExpoPushPlugin), "push-expo", "push")]
    public void TC_F_PLUG_001_Contract_Identity(Type pluginType, string id, string channel)
    {
        var plugin = (IChannelPlugin)Activator.CreateInstance(pluginType)!;
        plugin.Id.Should().Be(id);
        plugin.Channel.Should().Be(channel);
        plugin.Version.Should().Be(new Version(1, 0, 0));
        plugin.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public void TC_F_PLUG_002_Telegram_HtmlEscape()
    {
        TelegramPlugin.EscapeHtml("a<b>&c").Should().Be("a&lt;b&gt;&amp;c");
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/1/2", true)]
    [InlineData("http://discord.com/api/webhooks/1/2", false)]
    [InlineData("https://evil.example/hook", false)]
    public void TC_SEC_PLUG_003_Discord_WebhookSafety(string url, bool ok)
    {
        DiscordPlugin.IsSafeHttps(url).Should().Be(ok);
    }

    [Fact]
    public async Task TC_ERR_PLUG_004_Unconfigured_Send_FailsGracefully()
    {
        var plugin = new TelegramPlugin();
        // not initialized → missing token
        var result = await plugin.SendAsync(new RenderedNotification
        {
            NotificationId = Guid.NewGuid(),
            Recipient = "123",
            Subject = "Hi",
            Body = "Body",
            Channel = "telegram"
        });
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CONFIG_MISSING");
    }
}
