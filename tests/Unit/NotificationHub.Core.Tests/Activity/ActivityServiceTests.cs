using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Activity;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Activity;

/// <summary>F06 — admin activity feed.</summary>
public class ActivityServiceTests
{
    [Fact]
    public async Task TC_F_ACT_001_ListsNotificationsAsActivity()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = Guid.NewGuid(),
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Sent,
            TenantId = "t1"
        });

        var sut = new ActivityService(db);
        var items = await sut.ListAsync("t1", 20);
        items.Should().Contain(x => x.Kind == "notification");
    }
}
