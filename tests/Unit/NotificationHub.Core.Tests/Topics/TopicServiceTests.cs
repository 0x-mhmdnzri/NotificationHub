using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Tests.Helpers;
using NotificationHub.Core.Topics;

namespace NotificationHub.Core.Tests.Topics;

/// <summary>F04 — topics and subscribers.</summary>
public class TopicServiceTests
{
    [Fact]
    public async Task TC_F_TOPIC_001_SaveSubscribeList()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new TopicService(db);
        await sut.SaveTopicAsync(new TopicDefinition { Key = "orders", Name = "Orders" });
        await sut.SubscribeAsync("orders", "u1", null, "email", "a@b.com");
        await sut.SubscribeAsync("orders", "u2", null, "sms", "+1000");

        var subs = await sut.ListSubscribersAsync("orders", null);
        subs.Should().HaveCount(2);
    }

    [Fact]
    public async Task TC_F_TOPIC_002_Unsubscribe()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new TopicService(db);
        await sut.SaveTopicAsync(new TopicDefinition { Key = "news" });
        await sut.SubscribeAsync("news", "u1", "t1", null, null);
        await sut.UnsubscribeAsync("news", "u1", "t1");
        (await sut.ListSubscribersAsync("news", "t1")).Should().BeEmpty();
    }

    [Fact]
    public async Task TC_E_TOPIC_003_DoubleSubscribe_Idempotent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new TopicService(db);
        await sut.SaveTopicAsync(new TopicDefinition { Key = "x" });
        await sut.SubscribeAsync("x", "u", null, null, null);
        await sut.SubscribeAsync("x", "u", null, null, null);
        (await sut.ListSubscribersAsync("x", null)).Should().HaveCount(1);
    }
}
