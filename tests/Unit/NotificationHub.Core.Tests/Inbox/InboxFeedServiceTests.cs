using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Inbox;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Inbox;

/// <summary>F01 — Inbox feed behavior.</summary>
public class InboxFeedServiceTests
{
    [Fact]
    public async Task TC_F_INBOX_001_PushAndGetFeed()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new InboxFeedService(db, new InMemoryInboxEventBus());
        await sut.PushAsync(new InboxItem { UserId = "u1", TenantId = "t1", Title = "Hi", Body = "Welcome" });

        var feed = await sut.GetFeedAsync("u1", "t1");
        feed.Items.Should().HaveCount(1);
        feed.UnreadCount.Should().Be(1);
        feed.Items[0].Title.Should().Be("Hi");
    }

    [Fact]
    public async Task TC_F_INBOX_002_MarkRead_AndArchive()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new InboxFeedService(db, new InMemoryInboxEventBus());
        var item = await sut.PushAsync(new InboxItem { UserId = "u1", Title = "A", Body = "B" });

        (await sut.MarkReadAsync(item.Id, "u1", null)).Should().BeTrue();
        (await sut.GetFeedAsync("u1", null)).UnreadCount.Should().Be(0);

        (await sut.ArchiveAsync(item.Id, "u1", null)).Should().BeTrue();
        (await sut.GetFeedAsync("u1", null, includeArchived: false)).Items.Should().BeEmpty();
        (await sut.GetFeedAsync("u1", null, includeArchived: true)).Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task TC_ERR_INBOX_003_MarkRead_WrongUser_Fails()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new InboxFeedService(db, new InMemoryInboxEventBus());
        var item = await sut.PushAsync(new InboxItem { UserId = "u1", Title = "A", Body = "B" });
        (await sut.MarkReadAsync(item.Id, "other", null)).Should().BeFalse();
    }

    [Fact]
    public async Task TC_F_INBOX_004_MarkAllRead()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new InboxFeedService(db, new InMemoryInboxEventBus());
        await sut.PushAsync(new InboxItem { UserId = "u1", Title = "1", Body = "x" });
        await sut.PushAsync(new InboxItem { UserId = "u1", Title = "2", Body = "y" });
        var n = await sut.MarkAllReadAsync("u1", null);
        n.Should().Be(2);
        (await sut.GetFeedAsync("u1", null)).UnreadCount.Should().Be(0);
    }
}
