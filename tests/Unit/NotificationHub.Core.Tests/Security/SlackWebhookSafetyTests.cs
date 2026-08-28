using FluentAssertions;
using NotificationHub.Plugins.Chat.Slack;

namespace NotificationHub.Core.Tests.Security;

/// <summary>SEC-29 — Slack webhook URL must be https public host.</summary>
public class SlackWebhookSafetyTests
{
    [Theory]
    [InlineData("https://hooks.slack.com/services/T00/B00/XXX", true)]
    [InlineData("http://hooks.slack.com/services/T00/B00/XXX", false)]
    [InlineData("https://127.0.0.1/hook", false)]
    [InlineData("https://localhost/hook", false)]
    [InlineData("https://192.168.1.1/hook", false)]
    [InlineData("https://10.0.0.5/hook", false)]
    [InlineData("not-a-url", false)]
    public void TC_SEC_029_IsSafeWebhook(string url, bool expected)
    {
        SlackPlugin.IsSafeWebhook(url).Should().Be(expected);
    }
}
