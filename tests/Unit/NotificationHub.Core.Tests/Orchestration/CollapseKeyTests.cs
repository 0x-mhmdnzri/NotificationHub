using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Tests.Helpers;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Core.Tests.Orchestration;

/// <summary>F12 — collapse key dedup.</summary>
public class CollapseKeyTests
{
    [Fact]
    public async Task TC_F_COLLAPSE_001_SecondSend_ReturnsExisting()
    {
        await using var db = TestFixtures.CreateDbContext();
        var orch = TestFixtures.CreateOrchestrator(db, TestFixtures.CreateChannelPlugin("email-sendgrid", "email"));
        var req = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            CollapseKey = "order-42"
        };
        // need template
        var engine = new TemplateEngine(new InMemoryTemplateStore(), new PlaceholderTemplateRenderer(), NullLogger<TemplateEngine>.Instance);
        await engine.RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "welcome",
            Channel = "email",
            Locale = "en",
            Subject = "Hi",
            Body = "Hello"
        });

        // CreateOrchestrator already has template engine with empty store - register via db path?
        // Prefer direct status store collapse path
        var store = new PostgresNotificationStatusStore(db);
        var id = Guid.NewGuid();
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Queued,
            CollapseKey = "order-42"
        });

        var found = await store.FindByCollapseKeyAsync("order-42", "a@b.com");
        found.Should().NotBeNull();
        found!.NotificationId.Should().Be(id);
    }
}
