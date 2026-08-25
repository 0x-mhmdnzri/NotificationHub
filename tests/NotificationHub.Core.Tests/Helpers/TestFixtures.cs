using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Core.Tests.Helpers;

public static class TestFixtures
{
    public static NotificationDbContext CreateDbContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }

    public static IChannelPlugin CreateChannelPlugin(
        string id,
        string channel,
        bool success = true,
        string? errorCode = null,
        int failTimes = 0)
    {
        var attempts = 0;
        var mock = new Mock<IChannelPlugin>();
        mock.SetupGet(x => x.Id).Returns(id);
        mock.SetupGet(x => x.Channel).Returns(channel);
        mock.SetupGet(x => x.Name).Returns(id);
        mock.SetupGet(x => x.Version).Returns(new Version(1, 0, 0));
        mock.SetupGet(x => x.Capabilities).Returns([new PluginCapability("channel", channel)]);
        mock.Setup(x => x.SendAsync(It.IsAny<RenderedNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                var ok = success && attempts > failTimes;
                return new DeliveryResult
                {
                    Success = ok,
                    ProviderId = id,
                    ProviderMessageId = ok ? $"msg-{id}-{attempts}" : null,
                    ErrorCode = ok ? null : errorCode ?? "FAIL",
                    ErrorMessage = ok ? null : "provider failed",
                    AttemptNumber = attempts
                };
            });
        return mock.Object;
    }

    public static NotificationOrchestrator CreateOrchestrator(
        NotificationDbContext db,
        params IChannelPlugin[] plugins)
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        foreach (var plugin in plugins)
            loader.Register(plugin);

        var health = new InMemoryProviderHealthTracker(Options.Create(new ProviderHealthOptions()));
        var providerOptions = Options.Create(new ProviderOptions
        {
            PreferredEmailProvider = "email-sendgrid",
            PreferredSmsProvider = "sms-kavenegar",
            EmailFallbackOrder = ["email-sendgrid", "email-smtp"],
            SmsFallbackOrder = ["sms-kavenegar", "sms-smsir"]
        });
        var healthOptions = Options.Create(new ProviderHealthOptions());
        var router = new HealthAwareProviderRouter(loader, health, providerOptions, healthOptions, NullLogger<HealthAwareProviderRouter>.Instance);
        return new NotificationOrchestrator(
            loader,
            new TemplateEngine(new InMemoryTemplateStore(), new PlaceholderTemplateRenderer(), NullLogger<TemplateEngine>.Instance),
            new PostgresNotificationStatusStore(db),
            new PreferenceService(db),
            new ConsentService(db),
            new AuditService(db),
            new NoopWebhookDispatcher(),
            router,
            health,
            NullLogger<NotificationOrchestrator>.Instance);
    }
}

public sealed class NoopWebhookDispatcher : IWebhookDispatcher
{
    public Task DispatchAsync(string eventName, object payload, string? tenantId = null, CancellationToken ct = default)
        => Task.CompletedTask;
}
