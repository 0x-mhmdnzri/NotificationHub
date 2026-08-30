using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Compliance;

public class ConsentAndRetentionTests
{
    [Fact]
    public async Task TC_F_090_Consent_GrantThenRevoke_BlocksSendPurpose()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ConsentService(db);

        await sut.RecordAsync(new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing",
            Channel = "email",
            Granted = true
        });
        var allowed = await sut.EvaluateAsync("user-1", "marketing", "email");
        allowed.Allowed.Should().BeTrue();

        await sut.RecordAsync(new ConsentRecord
        {
            SubjectId = "user-1",
            Purpose = "marketing",
            Channel = "email",
            Granted = false
        });
        var denied = await sut.EvaluateAsync("user-1", "marketing", "email");
        denied.Allowed.Should().BeFalse();
        denied.Reason.Should().Contain("revoked");
    }

    [Fact]
    public async Task TC_F_091_Transactional_DefaultAllowedWithoutConsent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ConsentService(db);
        var decision = await sut.EvaluateAsync("user-x", "transactional", "sms");
        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task TC_F_092_Marketing_DefaultDeniedWithoutConsent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new ConsentService(db);
        var decision = await sut.EvaluateAsync("user-x", "marketing", "email");
        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task TC_F_093_Retention_DeletesOldNotifications()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = oldId,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Sent
        });
        await store.SaveAsync(new NotificationStatus
        {
            NotificationId = newId,
            Channel = "email",
            Recipient = "b@b.com",
            Status = DeliveryStatus.Sent
        });

        var old = db.NotificationStatuses.First(x => x.Id == oldId);
        old.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
        await db.SaveChangesAsync();

        var retention = new RetentionService(db, Options.Create(new RetentionOptions
        {
            Enabled = true,
            NotificationDays = 90,
            AuditDays = 180,
            TimelineDays = 90,
            ConsentDays = 730
        }), NullLogger<RetentionService>.Instance);

        var result = await retention.SweepAsync();
        result.NotificationsDeleted.Should().Be(1);
        db.NotificationStatuses.Count().Should().Be(1);
    }

    [Fact]
    public async Task TC_F_094_Retention_DeletesOldOutboxAndInbox()
    {
        await using var db = TestFixtures.CreateDbContext();
        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            NotificationId = Guid.NewGuid(),
            PayloadJson = "{}",
            Status = "published",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });
        db.InboxMessages.Add(new InboxMessageEntity
        {
            MessageId = "old-msg",
            ProcessedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });
        await db.SaveChangesAsync();

        var retention = new RetentionService(db, Options.Create(new RetentionOptions
        {
            Enabled = true,
            NotificationDays = 90,
            AuditDays = 180,
            TimelineDays = 90,
            ConsentDays = 730,
            OutboxPublishedDays = 7,
            InboxDays = 14
        }), NullLogger<RetentionService>.Instance);

        var result = await retention.SweepAsync();
        result.OutboxDeleted.Should().Be(1);
        result.InboxDeleted.Should().Be(1);
    }
}
