using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Engagement;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Engagement;

/// <summary>
/// SEC-20 / SEC-21 / SEC-22 — engagement access control and existence checks.
/// Requirement: engagement read/write only for existing notifications; list is data-scoped by notification id
/// (tenant enforcement is at API layer; service enforces existence for writes).
/// </summary>
public class EngagementSecurityTests
{
    [Fact]
    public async Task TC_SEC_020_ListByNotification_ReturnsOnlyMatchingRows()
    {
        // Arrange
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var svc = new EngagementService(db, store, NullLogger<EngagementService>.Instance);
        var id = Guid.NewGuid();
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Sent,
            TenantId = "tenant-a"
        });

        await svc.TrackAsync(new EngagementEvent
        {
            NotificationId = id,
            EventType = EngagementEventTypes.Open,
            Channel = "email"
        }, requireExistingNotification: true);

        // Act
        var list = await svc.ListByNotificationAsync(id);

        // Assert
        list.Should().HaveCount(1);
        list[0].EventType.Should().Be(EngagementEventTypes.Open);
    }

    [Fact]
    public async Task TC_SEC_021_Track_RequiresExistingNotification_WhenFlagTrue()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var svc = new EngagementService(db, store, NullLogger<EngagementService>.Instance);

        var result = await svc.TrackAsync(new EngagementEvent
        {
            NotificationId = Guid.NewGuid(),
            EventType = EngagementEventTypes.Open,
            Channel = "email"
        }, requireExistingNotification: true);

        result.Should().BeNull();
        db.EngagementEvents.Count().Should().Be(0);
    }

    [Fact]
    public async Task TC_SEC_022_Track_Persists_WhenNotificationExists()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var svc = new EngagementService(db, store, NullLogger<EngagementService>.Instance);
        var id = Guid.NewGuid();
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "user@example.com",
            Status = DeliveryStatus.Sent,
            TenantId = "t1"
        });

        var result = await svc.TrackAsync(new EngagementEvent
        {
            NotificationId = id,
            EventType = EngagementEventTypes.Click,
            Channel = "email",
            Url = "https://example.com"
        }, requireExistingNotification: true);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be("t1");
        result.Recipient.Should().Be("user@example.com");
        db.EngagementEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task TC_SEC_022_Track_MissingNotificationId_ReturnsNull_WhenRequired()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var svc = new EngagementService(db, store, NullLogger<EngagementService>.Instance);

        var result = await svc.TrackAsync(new EngagementEvent
        {
            NotificationId = null,
            EventType = EngagementEventTypes.Open
        }, requireExistingNotification: true);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TC_E_SEC_022_Track_CanBypassExistence_WhenExplicitlyAllowed()
    {
        // Edge: internal paths may allow optional tracking without status (not used by public /t)
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var svc = new EngagementService(db, store, NullLogger<EngagementService>.Instance);

        var result = await svc.TrackAsync(new EngagementEvent
        {
            NotificationId = Guid.NewGuid(),
            EventType = EngagementEventTypes.Open,
            Channel = "email"
        }, requireExistingNotification: false);

        result.Should().NotBeNull();
    }
}
