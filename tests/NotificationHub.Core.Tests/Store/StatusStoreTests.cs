using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Store;

public class StatusStoreTests
{
    [Fact]
    public async Task TC_F_020_SaveAndGet_ById()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PostgresNotificationStatusStore(db);
        var id = Guid.NewGuid();

        await sut.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Queued
        });

        var loaded = await sut.GetAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(DeliveryStatus.Queued);
        loaded.Recipient.Should().Be("a@b.com");
    }

    [Fact]
    public async Task TC_F_021_IdempotencyKey_Lookup()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PostgresNotificationStatusStore(db);
        var id = Guid.NewGuid();

        await sut.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "sms",
            Recipient = "+98",
            Status = DeliveryStatus.Queued,
            IdempotencyKey = "otp-1",
            TenantId = "t1"
        });

        var loaded = await sut.GetByIdempotencyKeyAsync("otp-1", "t1");
        loaded.Should().NotBeNull();
        loaded!.NotificationId.Should().Be(id);
    }

    [Fact]
    public async Task TC_ST_001_UpdateStatus_QueuedToSent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PostgresNotificationStatusStore(db);
        var id = Guid.NewGuid();

        await sut.SaveAsync(new NotificationStatus
        {
            NotificationId = id,
            Channel = "email",
            Recipient = "a@b.com",
            Status = DeliveryStatus.Queued
        });

        await sut.UpdateStatusAsync(id, DeliveryStatus.Sent, providerMessageId: "sg-1", attemptCount: 1);
        var loaded = await sut.GetAsync(id);

        loaded!.Status.Should().Be(DeliveryStatus.Sent);
        loaded.ProviderMessageId.Should().Be("sg-1");
        loaded.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task TC_E_020_Get_UnknownId_ReturnsNull()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new PostgresNotificationStatusStore(db);

        var loaded = await sut.GetAsync(Guid.NewGuid());
        loaded.Should().BeNull();
    }
}
