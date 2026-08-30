using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Engagement;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Engagement;

public class EngagementServiceTests
{
    [Fact]
    public async Task TC_F_100_TrackOpen_PersistsEvent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var id = Guid.NewGuid();
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Sent,
            TenantId = "t1"
        });

        var sut = new EngagementService(db, store, NullLogger<EngagementService>.Instance);
        var evt = await sut.TrackAsync(new EngagementEvent
        {
            NotificationId = id,
            EventType = EngagementEventTypes.Open
        });

        evt.Should().NotBeNull();
        evt!.EventType.Should().Be("open");
        evt.Recipient.Should().Be("a@b.com");
        evt.TenantId.Should().Be("t1");

        var list = await sut.ListByNotificationAsync(id);
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task TC_F_101_TrackClick_CountsInAggregate()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var sut = new EngagementService(db, store, NullLogger<EngagementService>.Instance);

        // SEC-22: without existing status, default requireExisting skips — use explicit false for aggregate unit test of CountAsync
        await sut.TrackAsync(new EngagementEvent { NotificationId = Guid.NewGuid(), EventType = "open", TenantId = "t1" }, requireExistingNotification: false);
        await sut.TrackAsync(new EngagementEvent { NotificationId = Guid.NewGuid(), EventType = "open", TenantId = "t1" }, requireExistingNotification: false);
        await sut.TrackAsync(new EngagementEvent { NotificationId = Guid.NewGuid(), EventType = "click", TenantId = "t1", Url = "https://example.com" }, requireExistingNotification: false);

        var (opens, clicks) = await sut.CountAsync(null, null, "t1");
        opens.Should().Be(2);
        clicks.Should().Be(1);
    }
}
