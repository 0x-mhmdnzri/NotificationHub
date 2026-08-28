using FluentAssertions;
using NotificationHub.Core.Inbox;
using NotificationHub.Core.Sync;
using NotificationHub.Core.Tests.Helpers;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Tests.Sync;

/// <summary>F13 — cross-channel read sync.</summary>
public class CrossChannelReadSyncTests
{
    [Fact]
    public async Task TC_F_SYNC_001_MarksRelatedInboxRead()
    {
        await using var db = TestFixtures.CreateDbContext();
        var inbox = new InboxFeedService(db, new InMemoryInboxEventBus());
        var nid = Guid.NewGuid();
        await inbox.PushAsync(new InboxItem { UserId = "u1", Title = "T", Body = "B", NotificationId = nid });
        var sync = new CrossChannelReadSync(db);
        var n = await sync.SyncReadAsync(nid, "u1", null);
        n.Should().Be(1);
        (await inbox.GetFeedAsync("u1", null)).UnreadCount.Should().Be(0);
    }
}
