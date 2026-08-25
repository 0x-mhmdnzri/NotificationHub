using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Routing;

namespace NotificationHub.Core.Tests.Routing;

public class ProviderHealthAndRouterTests
{
    [Fact]
    public void TC_F_080_Health_RecordsSuccessAndFailureRates()
    {
        var tracker = new InMemoryProviderHealthTracker(Options.Create(new ProviderHealthOptions { MinSamples = 2, WindowSize = 10 }));
        tracker.RecordSuccess("sms-kavenegar", "sms");
        tracker.RecordFailure("sms-kavenegar", "sms", "TIMEOUT");
        var snap = tracker.GetHealth("sms-kavenegar", "sms");
        snap.TotalSamples.Should().Be(2);
        snap.SuccessRate.Should().Be(0.5);
        snap.LastErrorCode.Should().Be("TIMEOUT");
    }

    [Fact]
    public void TC_F_081_Router_DeprioritizesUnhealthyProvider()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        var primary = CreatePlugin("email-sendgrid", "email");
        var secondary = CreatePlugin("email-smtp", "email");
        loader.Register(primary);
        loader.Register(secondary);

        var health = new InMemoryProviderHealthTracker(Options.Create(new ProviderHealthOptions
        {
            MinSamples = 3,
            UnhealthyThreshold = 0.5,
            DeprioritizeUnhealthy = true
        }));
        // Make sendgrid unhealthy
        health.RecordFailure("email-sendgrid", "email");
        health.RecordFailure("email-sendgrid", "email");
        health.RecordFailure("email-sendgrid", "email");

        var router = new HealthAwareProviderRouter(
            loader,
            health,
            Options.Create(new ProviderOptions
            {
                PreferredEmailProvider = "email-sendgrid",
                EmailFallbackOrder = ["email-sendgrid", "email-smtp"]
            }),
            Options.Create(new ProviderHealthOptions { MinSamples = 3, UnhealthyThreshold = 0.5, DeprioritizeUnhealthy = true }),
            NullLogger<HealthAwareProviderRouter>.Instance);

        var ordered = router.Resolve("email", null, allowFallback: true);
        ordered.First().Id.Should().Be("email-smtp");
        ordered.Last().Id.Should().Be("email-sendgrid");
    }

    [Fact]
    public void TC_F_082_Router_RespectsPreferredWhenHealthy()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        loader.Register(CreatePlugin("sms-kavenegar", "sms"));
        loader.Register(CreatePlugin("sms-smsir", "sms"));
        var health = new InMemoryProviderHealthTracker(Options.Create(new ProviderHealthOptions()));
        var router = new HealthAwareProviderRouter(
            loader, health,
            Options.Create(new ProviderOptions { PreferredSmsProvider = "sms-kavenegar", SmsFallbackOrder = ["sms-kavenegar", "sms-smsir"] }),
            Options.Create(new ProviderHealthOptions()),
            NullLogger<HealthAwareProviderRouter>.Instance);

        var ordered = router.Resolve("sms", null, true);
        ordered.First().Id.Should().Be("sms-kavenegar");
    }

    private static IChannelPlugin CreatePlugin(string id, string channel)
    {
        var mock = new Mock<IChannelPlugin>();
        mock.SetupGet(x => x.Id).Returns(id);
        mock.SetupGet(x => x.Channel).Returns(channel);
        mock.SetupGet(x => x.Name).Returns(id);
        mock.SetupGet(x => x.Version).Returns(new Version(1, 0));
        mock.SetupGet(x => x.Capabilities).Returns([new PluginCapability("channel", channel)]);
        return mock.Object;
    }
}
